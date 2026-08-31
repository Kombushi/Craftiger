using System.Text.Json;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Repositories;

public sealed class PlannerArtifactRepository(
    IFactoryArtifactReader factoryReader,
    IGarageLegalityService legality,
    ILogger<PlannerArtifactRepository> logger) : IPlannerArtifactRepository
{
    /// <summary>The artifact contract this build reads; anything else is refused loudly.</summary>
    public const int SupportedSchemaVersion = 15;

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
                "SELECT id, name_en AS Name, oredict, is_fluid AS IsFluid, leaf_class AS LeafClass, atlas_idx AS AtlasIdx, max_stack AS MaxStack FROM items")
            .ToDictionary(
                row => row.Id,
                row => new ArtifactItem(
                    row.Id, row.Name, row.Oredict, row.IsFluid != 0, row.LeafClass, row.AtlasIdx, (int?)row.MaxStack,
                    aliases.TryGetValue(row.Id, out var names)
                        ? [.. names.Where(name => name != row.Name).Distinct().Order(StringComparer.Ordinal)]
                        : []));

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

        var (index, recipeData, factoryRecipes, machines) = LoadRecipes(connectionString, leaves.Values);
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
                legality.IsAlwaysOwned(pair.Key),
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
            factoryReader.Read(db, meta, factoryRecipes),
            meta.GetValueOrDefault("pack_version") ?? "unknown",
            meta.GetValueOrDefault("build_id")
                ?? throw new InvalidOperationException("planner.sqlite carries no build_id; rebuild it with the current builder"),
            JsonSerializer.Deserialize<List<string>>(meta.GetValueOrDefault("tier_names") ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<long>>(meta.GetValueOrDefault("tier_voltages") ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<CoilDto>>(meta.GetValueOrDefault("coils") ?? "[]") ?? [],
            machineDtos,
            meta.TryGetValue("atlas_width", out var value)
                ? new AtlasDto(int.Parse(value), int.Parse(meta["atlas_height"]), int.Parse(meta["atlas_cell"]))
                : null,
            path);

        logger.LogInformation(
            "loaded {Items:N0} items, {Recipes:N0} recipes, {Machines:N0} machines from {Path}",
            items.Count, index.RecipeCount, machineDtos.Count, path);
        return artifact;
    }

    /// <summary>Streams recipes, inputs, outputs and grid cells — cursors ordered alike by recipe row — straight into the index builder; the row order inside a slot fixes which alternative wins ties, so it must be stable.</summary>
    private static (SolverIndex Index, ArtifactRecipeData Recipes, FactoryRecipeData FactoryRecipes, Dictionary<string, (bool MultiTier, bool Heat)> Machines) LoadRecipes(
        string connectionString, IEnumerable<SolverItem> leaves)
    {
        var builder = new SolverIndexBuilder(leaves);
        var durations = new List<long>();
        var euT = new List<long>();
        var amps = new List<long>();
        var overclocks = new List<OverclockMode>();
        var cleanrooms = new List<bool>();
        var lowGravities = new List<bool>();
        var catalystSlotStart = new List<int> { 0 };
        var catalystAlternativeStart = new List<int> { 0 };
        var catalystItemId = new List<string>();
        var catalystAmount = new List<long>();
        var catalystIds = new Dictionary<string, string>();
        var machines = new Dictionary<string, (bool MultiTier, bool Heat)>();

        var gridStart = new List<int> { 0 };
        var gridCell = new List<byte>();
        var gridSlot = new List<int>();

        using var recipesDb = new SqliteConnection(connectionString);
        using var inputsDb = new SqliteConnection(connectionString);
        using var outputsDb = new SqliteConnection(connectionString);
        using var gridDb = new SqliteConnection(connectionString);
        recipesDb.Open();
        inputsDb.Open();
        outputsDb.Open();
        gridDb.Open();
        using var grid = gridDb.Query<GridRow>(
            """
            SELECT g.recipe_id AS RecipeId, g.cell, g.slot
            FROM recipe_grid g JOIN recipes r ON r.id = g.recipe_id
            ORDER BY r.rowid, g.cell
            """, buffered: false).GetEnumerator();
        var gridRow = grid.MoveNext() ? grid.Current : null;
        using var inputs = inputsDb.Query<InputRow>(
            """
            SELECT i.recipe_id AS RecipeId, i.item_id AS ItemId, i.amount, i.slot, i.catalyst, i.tool
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
            "SELECT id, machine, tier, multi_tier AS MultiTier, heat, duration_ticks AS DurationTicks, eu_t AS EuT, amps, cleanroom, scope, low_gravity AS LowGravity, overclock FROM recipes ORDER BY rowid",
            buffered: false))
        {
            builder.BeginRecipe(
                recipe.Id, recipe.Machine, (int)recipe.Tier, (int?)recipe.MultiTier, (int?)recipe.Heat,
                recipe.Scope switch
                {
                    null => RecipeScope.None,
                    "FACTORY" => RecipeScope.Factory,
                    "FACTORY_MOB" => RecipeScope.FactoryMob,
                    "FACTORY_BRED" => RecipeScope.FactoryBred,
                    var other => throw new InvalidOperationException($"recipe {recipe.Id} names an unknown scope '{other}'"),
                });
            durations.Add(recipe.DurationTicks);
            euT.Add(recipe.EuT);
            amps.Add(recipe.Amps);
            cleanrooms.Add(recipe.Cleanroom != 0);
            lowGravities.Add(recipe.LowGravity != 0);
            overclocks.Add(recipe.Overclock switch
            {
                null => OverclockMode.Standard,
                "TREE_FARM" => OverclockMode.TreeFarm,
                "FIXED" => OverclockMode.Fixed,
                "EEC" => OverclockMode.EntityCrusher,
                var other => throw new InvalidOperationException($"recipe {recipe.Id} names an unknown overclock ladder '{other}'"),
            });
            var flags = machines.GetValueOrDefault(recipe.Machine);
            machines[recipe.Machine] = (flags.MultiTier || recipe.MultiTier is not null, flags.Heat || recipe.Heat is not null);

            long slot = -1;
            var catalyst = false;
            var catalystSlotOpen = false;
            var toolCounted = false;
            while (input is not null && input.RecipeId == recipe.Id)
            {
                var isCatalyst = input.Catalyst != 0;
                if (input.Slot != slot || isCatalyst != catalyst)
                {
                    slot = input.Slot;
                    catalyst = isCatalyst;
                    toolCounted = false;
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
                    // One wearing tool among the alternatives makes the slot a tool slot, once.
                    if (input.Tool != 0 && !toolCounted)
                    {
                        builder.AddToolSlot();
                        toolCounted = true;
                    }
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

            while (gridRow is not null && gridRow.RecipeId == recipe.Id)
            {
                gridCell.Add((byte)gridRow.Cell);
                gridSlot.Add((int)gridRow.Slot);
                gridRow = grid.MoveNext() ? grid.Current : null;
            }
            gridStart.Add(gridCell.Count);
        }

        return (
            builder.Build(),
            new ArtifactRecipeData(
                [.. durations],
                [.. euT],
                [.. catalystSlotStart],
                [.. catalystAlternativeStart],
                [.. catalystItemId],
                [.. catalystAmount],
                [.. gridStart],
                [.. gridCell],
                [.. gridSlot]),
            new FactoryRecipeData(
                [.. durations], [.. euT], [.. amps], [.. overclocks], [.. cleanrooms], [.. lowGravities]),
            machines);
    }
}
