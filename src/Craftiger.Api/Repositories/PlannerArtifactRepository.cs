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
    public const int SupportedSchemaVersion = 2;

    public PlannerArtifact Load(string artifactsDir)
    {
        var path = Path.Combine(artifactsDir, "planner.sqlite");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"planner.sqlite not found in '{artifactsDir}'", path);
        }

        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();

        var meta = db.Query<(string Key, string Value)>("SELECT key, value FROM meta")
            .ToDictionary(row => row.Key, row => row.Value);
        var version = meta.GetValueOrDefault("schema_version");
        if (version != SupportedSchemaVersion.ToString())
        {
            throw new InvalidOperationException(
                $"planner.sqlite carries schema_version {version ?? "(none)"}; this build reads {SupportedSchemaVersion}");
        }

        var items = db.Query<ItemRow>(
                "SELECT id, name_en AS Name, oredict, is_fluid AS IsFluid, leaf_class AS LeafClass, atlas_idx AS AtlasIdx FROM items")
            .ToDictionary(
                row => row.Id,
                row => new ArtifactItem(row.Id, row.Name, row.Oredict, row.IsFluid != 0, row.LeafClass, row.AtlasIdx));

        var tiers = db.Query<(string ItemId, long Tier)>("SELECT item_id, tier FROM item_tiers")
            .ToDictionary(row => row.ItemId, row => (int)row.Tier);
        var weights = db.Query<(string ItemId, double Weight)>("SELECT item_id, weight FROM item_weights")
            .ToDictionary(row => row.ItemId, row => row.Weight);
        var parents = db.Query<(string ItemId, string ParentItemId, double Divisor)>(
                "SELECT item_id, parent_item_id, divisor FROM item_parents")
            .ToDictionary(row => row.ItemId, row => new ItemParentLink(row.ParentItemId, row.Divisor));

        var leaves = items.Values
            .Where(item => item.LeafClass is not null)
            .Select(item => new SolverItem(
                item.Id, item.LeafClass,
                tiers.TryGetValue(item.Id, out var tier) ? tier : null,
                weights.TryGetValue(item.Id, out var weight) ? weight : null,
                parents.GetValueOrDefault(item.Id)))
            .ToList();

        var recipeRows = db.Query<RecipeRow>(
                "SELECT id, machine, tier, multi_tier AS MultiTier, heat, duration_ticks AS DurationTicks, eu_t AS EuT FROM recipes")
            .ToList();

        // Row order inside a slot fixes which alternative wins ties, so it must be stable.
        var slotsByRecipe = new Dictionary<string, List<SolverSlot>>();
        foreach (var group in db.Query<(string RecipeId, string ItemId, long Amount, long Slot)>(
                "SELECT recipe_id, item_id, amount, slot FROM recipe_inputs ORDER BY recipe_id, slot, rowid")
            .GroupBy(row => (row.RecipeId, row.Slot)))
        {
            if (!slotsByRecipe.TryGetValue(group.Key.RecipeId, out var slots))
            {
                slotsByRecipe[group.Key.RecipeId] = slots = [];
            }
            slots.Add(new SolverSlot(group.Select(row => new SolverStack(row.ItemId, row.Amount)).ToList()));
        }

        var outputsByRecipe = new Dictionary<string, List<SolverOutput>>();
        foreach (var row in db.Query<(string RecipeId, string ItemId, long Amount, double Chance)>(
            "SELECT recipe_id, item_id, amount, chance FROM recipe_outputs"))
        {
            if (!outputsByRecipe.TryGetValue(row.RecipeId, out var outputs))
            {
                outputsByRecipe[row.RecipeId] = outputs = [];
            }
            outputs.Add(new SolverOutput(row.ItemId, row.Amount, row.Chance));
        }

        var solverRecipes = recipeRows.Select(row => new SolverRecipe(
            row.Id, row.Machine, (int)row.Tier, (int?)row.MultiTier, (int?)row.Heat,
            slotsByRecipe.GetValueOrDefault(row.Id) ?? [],
            outputsByRecipe.GetValueOrDefault(row.Id) ?? []));
        var graph = SolverGraph.Build(leaves, solverRecipes);

        var machines = recipeRows
            .GroupBy(row => row.Machine)
            .Select(byMachine => new MachineDto(
                byMachine.Key,
                byMachine.Any(row => row.MultiTier is not null),
                byMachine.Any(row => row.Heat is not null),
                rules.AlwaysOwnedMachines.Contains(byMachine.Key)))
            .OrderBy(machine => machine.Name, StringComparer.Ordinal)
            .ToList();

        var artifact = new PlannerArtifact(
            graph,
            items,
            recipeRows.ToDictionary(row => row.Id, row => new ArtifactRecipe(
                row.Id, row.Machine, (int)row.Tier, (int?)row.MultiTier, (int?)row.Heat,
                row.DurationTicks, row.EuT)),
            meta.GetValueOrDefault("pack_version") ?? "unknown",
            JsonSerializer.Deserialize<List<string>>(meta.GetValueOrDefault("tier_names") ?? "[]") ?? [],
            JsonSerializer.Deserialize<List<CoilDto>>(meta.GetValueOrDefault("coils") ?? "[]") ?? [],
            machines,
            meta.ContainsKey("atlas_width")
                ? new AtlasDto(
                    int.Parse(meta["atlas_width"]), int.Parse(meta["atlas_height"]), int.Parse(meta["atlas_cell"]))
                : null,
            path);

        logger.LogInformation(
            "loaded {Items:N0} items, {Recipes:N0} recipes, {Machines:N0} machines from {Path}",
            items.Count, recipeRows.Count, machines.Count, path);
        return artifact;
    }

    private sealed record ItemRow(
        string Id, string Name, string? Oredict, long IsFluid, string? LeafClass, long AtlasIdx);

    private sealed record RecipeRow(
        string Id, string Machine, long Tier, long? MultiTier, long? Heat,
        long DurationTicks, long EuT);
}
