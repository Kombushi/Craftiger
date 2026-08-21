using System.Text.Json;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Repositories;

public sealed class PlannerArtifactRepository(
    GarageRules rules, ILogger<PlannerArtifactRepository> logger) : IPlannerArtifactRepository
{
    /// <summary>The artifact contract this build reads; anything else is refused loudly.</summary>
    public const int SupportedSchemaVersion = 6;

    public PlannerArtifact Load(string artifactsDir)
    {
        var path = Path.Combine(artifactsDir, "planner.sqlite");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"planner.sqlite not found in '{artifactsDir}'", path);
        }

        var connectionString = $"Data Source={path};Mode=ReadOnly";
        using var db = new SqliteConnection(connectionString);
        db.Open();

        var meta = db.Query<(string Key, string Value)>("SELECT key, value FROM meta")
            .ToDictionary(row => row.Key, row => row.Value);
        var version = meta.GetValueOrDefault("schema_version");
        if (version != SupportedSchemaVersion.ToString())
        {
            throw new InvalidOperationException(
                $"planner.sqlite carries schema_version {version ?? "(none)"}; this build reads {SupportedSchemaVersion}");
        }

        // Only display-name aliases ship to clients; oredict names never contain spaces.
        var aliases = db.Query<(string ItemId, string Alias)>(
                "SELECT item_id, alias FROM item_aliases WHERE alias LIKE '% %'")
            .GroupBy(row => row.ItemId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Alias).ToList());

        var items = db.Query<ItemRow>(
                "SELECT id, name_en AS Name, oredict, is_fluid AS IsFluid, leaf_class AS LeafClass, atlas_idx AS AtlasIdx FROM items")
            .ToDictionary(
                row => row.Id,
                row => new ArtifactItem(
                    row.Id, row.Name, row.Oredict, row.IsFluid != 0, row.LeafClass, row.AtlasIdx,
                    // Typed explicitly, or the empty branch would become a fresh list per item.
                    aliases.TryGetValue(row.Id, out var names)
                        ? (IReadOnlyList<string>)names.Where(name => name != row.Name).Distinct().Order(StringComparer.Ordinal).ToList()
                        : Array.Empty<string>()));

        var tiers = db.Query<(string ItemId, long Tier)>("SELECT item_id, tier FROM item_tiers")
            .ToDictionary(row => row.ItemId, row => (int)row.Tier);
        var weights = db.Query<(string ItemId, double Weight)>("SELECT item_id, weight FROM item_weights")
            .ToDictionary(row => row.ItemId, row => row.Weight);
        var parents = db.Query<(string ItemId, string ParentItemId, double Divisor)>(
                "SELECT item_id, parent_item_id, divisor FROM item_parents")
            .ToDictionary(row => row.ItemId, row => new ItemParentLink(row.ParentItemId, row.Divisor));

        var leaves = items.Values
            .Where(item => item.LeafClass is not null)
            .ToDictionary(
                item => item.Id,
                item => new SolverItem(
                    item.Id, item.LeafClass,
                    tiers.TryGetValue(item.Id, out var tier) ? tier : null,
                    weights.TryGetValue(item.Id, out var weight) ? weight : null,
                    parents.GetValueOrDefault(item.Id)));

        var (index, recipeData, machines) = LoadRecipes(connectionString, leaves.Values);
        var graph = new SolverGraph(leaves, index);
        items = items.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with
            {
                Uncraftable = !graph.IsLeaf(pair.Key)
                    && !(index.TryGetItem(pair.Key, out var item) && index.IsProduced(item)),
            });

        var machineEras = db.Query<(string Machine, long? Era, long Multiblock)>(
                "SELECT machine, era, multiblock FROM machine_eras")
            .ToDictionary(row => row.Machine, row => (Era: (int?)row.Era, Multiblock: row.Multiblock != 0));

        var machineDtos = machines
            .Select(pair => new MachineDto(
                pair.Key, pair.Value.MultiTier, pair.Value.Heat,
                rules.AlwaysOwnedMachines.Contains(pair.Key),
                machineEras.GetValueOrDefault(pair.Key).Era,
                machineEras.GetValueOrDefault(pair.Key).Multiblock))
            .OrderBy(machine => machine.Name, StringComparer.Ordinal)
            .ToList();

        // The craft list's tie order, fixed per artifact: a solve only ranks priced items by cost.
        var craftListOrder = items.Values
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => item.Id)
            .ToArray();

        var artifact = new PlannerArtifact(
            graph,
            items,
            craftListOrder,
            recipeData,
            meta.GetValueOrDefault("pack_version") ?? "unknown",
            meta.GetValueOrDefault("build_id")
                ?? throw new InvalidOperationException("planner.sqlite carries no build_id; rebuild it with the current builder"),
            JsonSerializer.Deserialize<List<string>>(meta.GetValueOrDefault("tier_names") ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<CoilDto>>(meta.GetValueOrDefault("coils") ?? "[]") ?? [],
            machineDtos,
            meta.ContainsKey("atlas_width")
                ? new AtlasDto(
                    int.Parse(meta["atlas_width"]), int.Parse(meta["atlas_height"]), int.Parse(meta["atlas_cell"]))
                : null,
            path);

        logger.LogInformation(
            "loaded {Items:N0} items, {Recipes:N0} recipes, {Machines:N0} machines from {Path}",
            items.Count, index.RecipeCount, machineDtos.Count, path);
        return artifact;
    }

    /// <summary>Streams recipes, their inputs and their outputs — three cursors ordered alike,
    /// by the recipe's row — straight into the index builder, so no recipe ever exists as an
    /// object. The row order inside a slot fixes which alternative wins ties, so it must be
    /// stable; catalyst rows never reach the solver, they are display-only tool slots.</summary>
    private static (SolverIndex Index, ArtifactRecipeData Recipes, Dictionary<string, (bool MultiTier, bool Heat)> Machines)
        LoadRecipes(string connectionString, IEnumerable<SolverItem> leaves)
    {
        var builder = new SolverIndexBuilder(leaves);
        var durations = new List<long>();
        var euT = new List<long>();
        var catalystSlotStart = new List<int> { 0 };
        var catalystAlternativeStart = new List<int> { 0 };
        var catalystItemId = new List<string>();
        var catalystAmount = new List<long>();
        var catalystIds = new Dictionary<string, string>();
        var machines = new Dictionary<string, (bool MultiTier, bool Heat)>();

        using var recipesDb = new SqliteConnection(connectionString);
        using var inputsDb = new SqliteConnection(connectionString);
        using var outputsDb = new SqliteConnection(connectionString);
        recipesDb.Open();
        inputsDb.Open();
        outputsDb.Open();
        using var inputs = inputsDb.Query<InputRow>(
            """
            SELECT i.recipe_id AS RecipeId, i.item_id AS ItemId, i.amount, i.slot, i.catalyst
            FROM recipe_inputs i JOIN recipes r ON r.id = i.recipe_id
            ORDER BY r.rowid, i.slot, i.rowid
            """, buffered: false).GetEnumerator();
        using var outputs = outputsDb.Query<OutputRow>(
            """
            SELECT o.recipe_id AS RecipeId, o.item_id AS ItemId, o.amount, o.chance
            FROM recipe_outputs o JOIN recipes r ON r.id = o.recipe_id
            ORDER BY r.rowid, o.rowid
            """, buffered: false).GetEnumerator();
        var input = inputs.MoveNext() ? inputs.Current : null;
        var output = outputs.MoveNext() ? outputs.Current : null;

        foreach (var recipe in recipesDb.Query<RecipeRow>(
            "SELECT id, machine, tier, multi_tier AS MultiTier, heat, duration_ticks AS DurationTicks, eu_t AS EuT FROM recipes ORDER BY rowid",
            buffered: false))
        {
            builder.BeginRecipe(recipe.Id, recipe.Machine, (int)recipe.Tier, (int?)recipe.MultiTier, (int?)recipe.Heat);
            durations.Add(recipe.DurationTicks);
            euT.Add(recipe.EuT);
            var flags = machines.GetValueOrDefault(recipe.Machine);
            machines[recipe.Machine] = (flags.MultiTier || recipe.MultiTier is not null, flags.Heat || recipe.Heat is not null);

            long slot = -1;
            var catalyst = false;
            var catalystSlotOpen = false;
            while (input is not null && input.RecipeId == recipe.Id)
            {
                var isCatalyst = input.Catalyst != 0;
                if (input.Slot != slot || isCatalyst != catalyst)
                {
                    slot = input.Slot;
                    catalyst = isCatalyst;
                    if (isCatalyst)
                    {
                        if (catalystSlotOpen)
                        {
                            catalystAlternativeStart.Add(catalystItemId.Count);
                        }
                        catalystSlotOpen = true;
                    }
                    else
                    {
                        builder.BeginSlot();
                    }
                }
                if (isCatalyst)
                {
                    if (!catalystIds.TryGetValue(input.ItemId, out var shared))
                    {
                        catalystIds[input.ItemId] = shared = input.ItemId;
                    }
                    catalystItemId.Add(shared);
                    catalystAmount.Add(input.Amount);
                }
                else
                {
                    builder.AddAlternative(input.ItemId, input.Amount);
                }
                input = inputs.MoveNext() ? inputs.Current : null;
            }
            if (catalystSlotOpen)
            {
                catalystAlternativeStart.Add(catalystItemId.Count);
            }
            catalystSlotStart.Add(catalystAlternativeStart.Count - 1);

            while (output is not null && output.RecipeId == recipe.Id)
            {
                builder.AddOutput(output.ItemId, output.Amount, output.Chance);
                output = outputs.MoveNext() ? outputs.Current : null;
            }
        }

        return (
            builder.Build(),
            new ArtifactRecipeData(
                durations.ToArray(), euT.ToArray(),
                catalystSlotStart.ToArray(), catalystAlternativeStart.ToArray(), catalystItemId.ToArray(), catalystAmount.ToArray()),
            machines);
    }

    private sealed record ItemRow(
        string Id, string Name, string? Oredict, long IsFluid, string? LeafClass, long AtlasIdx);

    private sealed record RecipeRow(
        string Id, string Machine, long Tier, long? MultiTier, long? Heat,
        long DurationTicks, long EuT);

    private sealed record InputRow(string RecipeId, string ItemId, long Amount, long Slot, long Catalyst);

    private sealed record OutputRow(string RecipeId, string ItemId, long Amount, double Chance);
}
