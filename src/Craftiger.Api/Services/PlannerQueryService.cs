using System.ComponentModel.DataAnnotations;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using Craftiger.Solver.Services;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Services;

public sealed class PlannerQueryService(
    PlannerArtifact artifact,
    ICostSolverService solver,
    IGarageLegalityService legality,
    IBomService bom,
    IClosureService closure) : IPlannerQueryService
{
    private const int SearchLimit = 50;

    public MetaResponse Meta() => new(
        artifact.PackVersion, artifact.TierNames, artifact.Coils, artifact.Machines,
        artifact.Atlas);

    public ListResponse List(SolveEntry entry, int page, int pageSize, bool hideUnreachable)
    {
        var rows = hideUnreachable ? entry.Sorted.Take(entry.ReachableCount) : entry.Sorted;
        var total = hideUnreachable ? entry.ReachableCount : entry.Sorted.Count;
        var items = rows
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(rank =>
            {
                var item = artifact.Items[artifact.CraftListOrder[rank]];
                return new ItemSummaryDto(
                    item.Id, item.Name, item.AtlasIdx, entry.Table.Cost(item.Id), item.Uncraftable);
            })
            .ToList();
        return new ListResponse(items, total, page, pageSize);
    }

    /// <summary>Type-ahead over names and aliases straight off the artifact database; name
    /// prefix matches rank first, then the cheaper item wins.</summary>
    public IReadOnlyList<ItemSummaryDto> Search(SolveEntry? entry, string query)
    {
        using var db = new SqliteConnection($"Data Source={artifact.DbPath};Mode=ReadOnly");
        var pattern = $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var ids = db.Query<string>(
            """
            SELECT DISTINCT i.id FROM items i
            LEFT JOIN item_aliases a ON a.item_id = i.id
            WHERE i.name_en LIKE @Pattern ESCAPE '\' OR a.alias LIKE @Pattern ESCAPE '\'
            LIMIT @Limit
            """,
            new { Pattern = pattern, Limit = SearchLimit * 4 });

        return ids
            .Select(id => artifact.Items[id])
            .Select(item => new ItemSummaryDto(
                item.Id, item.Name, item.AtlasIdx,
                entry?.Table.Cost(item.Id),
                item.Uncraftable))
            .OrderBy(item => item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Cost ?? double.PositiveInfinity)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Take(SearchLimit)
            .ToList();
    }

    public ItemDetailResponse? ItemDetail(SolveEntry entry, string itemId)
    {
        if (!artifact.Items.TryGetValue(itemId, out var item))
        {
            return null;
        }

        var recipes = (artifact.Graph.Producers.GetValueOrDefault(itemId) ?? [])
            .Where(recipe => legality.IsLegal(recipe, entry.Garage))
            .Select(recipe => ToDto(entry, recipe, itemId))
            .OrderBy(recipe => recipe.CandidateCost ?? double.PositiveInfinity)
            .ThenBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ToList();

        var ids = new HashSet<string> { itemId };
        foreach (var recipe in recipes)
        {
            ids.UnionWith(recipe.Slots.SelectMany(slot => slot).Select(alternative => alternative.ItemId));
            ids.UnionWith(recipe.Catalysts.SelectMany(slot => slot).Select(alternative => alternative.ItemId));
            ids.UnionWith(recipe.Outputs.Select(output => output.ItemId));
        }

        return new ItemDetailResponse(
            item.Id, item.Name, item.AtlasIdx, item.LeafClass,
            entry.Table.Cost(itemId),
            item.Uncraftable,
            entry.Table.BestRecipe(itemId)?.Id,
            recipes,
            Refs(entry, ids));
    }

    public IReadOnlyList<string> Machines(IEnumerable<string> targetIds) =>
        closure.MachinesFor(artifact.Graph, targetIds);

    public BomResponse Bom(SolveEntry entry, BomRequest request)
    {
        foreach (var target in request.Targets)
        {
            if (target.Count <= 0)
            {
                throw new ValidationException($"the count of '{target.ItemId}' must be positive");
            }
        }
        var result = bom.Compute(
            artifact.Graph, entry.Table, entry.Garage,
            request.Targets.Select(target => new BomTarget(target.ItemId, target.Count)).ToList(),
            request.Pins ?? []);

        var nodes = result.Nodes.Select(ToDto).ToList();
        var ids = new HashSet<string>();
        ids.UnionWith(result.Targets.Select(target => target.ItemId));
        ids.UnionWith(result.Targets.SelectMany(target => target.Inputs).Select(input => input.ItemId));
        ids.UnionWith(result.Leaves.Select(leaf => leaf.ItemId));
        ids.UnionWith(result.Warnings.Select(warning => warning.ItemId));
        foreach (var node in nodes)
        {
            ids.Add(node.ItemId);
            ids.UnionWith(node.InputsPerRun.Select(input => input.ItemId));
            ids.UnionWith(node.Catalysts.Select(catalyst => catalyst.ItemId));
            ids.UnionWith(node.Outputs.Select(output => output.ItemId));
        }
        return new BomResponse(result.Targets, result.Leaves, result.Warnings, nodes, Refs(entry, ids));
    }

    private BomNodeDto ToDto(BomNode node)
    {
        var info = artifact.Recipes[node.RecipeId];
        var outputs = artifact.Graph.RecipesById[node.RecipeId].Outputs
            .Select(output => new OutputDto(output.ItemId, output.Amount, output.Chance))
            .ToList();
        // One representative stack per catalyst slot; the item detail lists the alternatives.
        var catalysts = info.Catalysts
            .Select(slot => new BomStack(slot.Alternatives[0].ItemId, slot.Alternatives[0].Amount))
            .ToList();
        return new BomNodeDto(
            node.ItemId, node.Amount, node.Runs, node.WholeAmount, node.WholeRuns, node.RecipeId,
            info.Machine, info.Tier, info.MultiTier, info.Heat, info.DurationTicks, info.EuT,
            node.InputsPerRun, catalysts, outputs, node.Loop, node.Seed);
    }

    private IReadOnlyDictionary<string, ItemRefDto> Refs(SolveEntry entry, IEnumerable<string> ids) =>
        ids.Where(artifact.Items.ContainsKey).ToDictionary(
            id => id,
            id =>
            {
                var item = artifact.Items[id];
                return new ItemRefDto(
                    item.Name, item.AtlasIdx, item.IsFluid, item.LeafClass,
                    entry.Table.Cost(id),
                    item.Uncraftable,
                    item.Aliases.Count > 0 ? item.Aliases : null);
            });

    private RecipeDto ToDto(SolveEntry entry, SolverRecipe recipe, string itemId)
    {
        var info = artifact.Recipes[recipe.Id];
        var candidate = solver.Candidate(entry.Table, recipe, itemId);
        return new RecipeDto(
            recipe.Id, info.Machine, info.Tier, info.MultiTier, info.Heat,
            info.DurationTicks, info.EuT,
            double.IsPositiveInfinity(candidate) ? null : candidate,
            recipe.Slots
                .Select(slot => (IReadOnlyList<SlotAlternativeDto>)slot.Alternatives
                    .Select(alternative => new SlotAlternativeDto(
                        alternative.ItemId, alternative.Amount,
                        entry.Table.Cost(alternative.ItemId) * alternative.Amount))
                    .ToList())
                .ToList(),
            SlotChoice.Inputs(entry.Table, itemId, recipe).Select(input => input.ItemId).ToList(),
            info.Catalysts
                .Select(slot => (IReadOnlyList<SlotAlternativeDto>)slot.Alternatives
                    .Select(alternative => new SlotAlternativeDto(alternative.ItemId, alternative.Amount, null))
                    .ToList())
                .ToList(),
            recipe.Outputs.Select(output => new OutputDto(output.ItemId, output.Amount, output.Chance)).ToList());
    }
}
