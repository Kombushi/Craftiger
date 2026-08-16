using System.ComponentModel.DataAnnotations;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
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
            .Select(row =>
            {
                var item = artifact.Items[row.ItemId];
                return new ItemSummaryDto(item.Id, item.Name, item.AtlasIdx, row.Cost);
            })
            .ToList();
        return new ListResponse(items, total, page, pageSize);
    }

    /// <summary>Type-ahead over names and aliases straight off the artifact database; name
    /// prefix matches rank first, then the cheaper item wins.</summary>
    public IReadOnlyList<ItemSummaryDto> Search(SolveEntry entry, string query)
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
                entry.Table.Costs.TryGetValue(item.Id, out var cost) ? cost : null))
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

        return new ItemDetailResponse(
            item.Id, item.Name, item.AtlasIdx, item.LeafClass,
            entry.Table.Costs.TryGetValue(itemId, out var cost) ? cost : null,
            recipes);
    }

    public IReadOnlyList<string> Machines(IEnumerable<string> targetIds) =>
        closure.MachinesFor(artifact.Graph, targetIds);

    public BomResult Bom(SolveEntry entry, BomRequest request)
    {
        foreach (var target in request.Targets)
        {
            if (target.Count <= 0)
            {
                throw new ValidationException($"the count of '{target.ItemId}' must be positive");
            }
        }
        return bom.Compute(
            artifact.Graph, entry.Table, entry.Garage,
            request.Targets.Select(target => new BomTarget(target.ItemId, target.Count)).ToList(),
            request.Pins ?? []);
    }

    private RecipeDto ToDto(SolveEntry entry, SolverRecipe recipe, string itemId)
    {
        var info = artifact.Recipes[recipe.Id];
        var candidate = solver.Candidate(recipe, itemId, entry.Table.Costs);
        return new RecipeDto(
            recipe.Id, info.Machine, info.Tier, info.MultiTier, info.Heat,
            info.DurationTicks, info.EuT,
            double.IsPositiveInfinity(candidate) ? null : candidate,
            recipe.Slots
                .Select(slot => (IReadOnlyList<SlotAlternativeDto>)slot.Alternatives
                    .Select(alternative => new SlotAlternativeDto(
                        alternative.ItemId, alternative.Amount,
                        entry.Table.Costs.TryGetValue(alternative.ItemId, out var cost)
                            ? cost * alternative.Amount
                            : null))
                    .ToList())
                .ToList(),
            recipe.Outputs.Select(output => new OutputDto(output.ItemId, output.Amount, output.Chance)).ToList());
    }
}
