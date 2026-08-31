using System.ComponentModel.DataAnnotations;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces.Bom;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Graph;
using Craftiger.Solver.Models.Bom;
using Craftiger.Solver.Models.Graph;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Services;

public sealed class PlannerQueryService(
    PlannerArtifact artifact,
    IGarageLegalityService legality,
    IBomService bom,
    IClosureService closure) : IPlannerQueryService
{
    private const int SearchLimit = 50;

    /// <summary>The trigram index needs three characters; shorter queries scan instead.</summary>
    private const int TrigramLength = 3;

    /// <summary>A one- or two-character query matches most of the pack; the scan stops here, which keeps it cheap but makes the set beyond arbitrary.</summary>
    private const int ShortQueryScanLimit = 500;

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

    /// <summary>Type-ahead over names and aliases: the trigram index answers three characters or more, shorter queries scan the same folded text; cheapest matches first, then by name.</summary>
    public IReadOnlyList<ItemSummaryDto> Search(SolveEntry? entry, string query)
    {
        using var db = new SqliteConnection($"Data Source={artifact.DbPath};Mode=ReadOnly");
        var folded = query.ToLowerInvariant();
        var ids = folded.Length >= TrigramLength
            ? db.Query<string>(
                "SELECT DISTINCT item_id FROM item_search WHERE item_search MATCH @Match",
                new { Match = $"\"{folded.Replace("\"", "\"\"")}\"" })
            : db.Query<string>(
                """
                SELECT DISTINCT item_id FROM item_search
                WHERE text LIKE @Pattern ESCAPE '\'
                LIMIT @Limit
                """,
                new
                {
                    Pattern = $"%{folded.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%",
                    Limit = ShortQueryScanLimit,
                });

        return ids
            .Where(artifact.Items.ContainsKey)
            .Select(id => artifact.Items[id])
            .Select(item => new ItemSummaryDto(
                item.Id, item.Name, item.AtlasIdx, entry?.Table.Cost(item.Id), item.Uncraftable))
            .OrderBy(item => item.Cost ?? double.PositiveInfinity)
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

        var index = artifact.Graph.Index;
        var recipes = new List<RecipeDto>();
        if (index.TryGetItem(itemId, out var position))
        {
            for (var p = index.ProducerStart[position]; p < index.ProducerStart[position + 1]; p++)
            {
                var recipe = index.ProducerRecipe[p];
                // Factory-scoped rows belong to rate planning alone; the crafting tab never lists them.
                if (index.ScopeOf(recipe) == RecipeScope.None && legality.IsLegal(index, recipe, entry.Garage))
                {
                    recipes.Add(ToDto(entry, recipe, itemId));
                }
            }
        }
        recipes = recipes
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
            entry.Table.BestRecipeId(itemId),
            recipes,
            Refs(entry, ids));
    }

    public IReadOnlyList<string> Machines(IEnumerable<string> targetIds) => closure.MachinesFor(artifact.Graph, targetIds);

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
        var index = artifact.Graph.Index;
        var recipe = index.RecipeIndex[node.RecipeId];
        // One representative stack per catalyst slot; the item detail lists the alternatives.
        var data = artifact.Recipes;
        var catalysts = new List<BomStack>(data.CatalystSlotCount(recipe));
        for (var s = 0; s < data.CatalystSlotCount(recipe); s++)
        {
            var at = data.CatalystAt(recipe, s, 0);
            catalysts.Add(new BomStack(data.CatalystItemId[at], data.CatalystAmount[at]));
        }
        return new BomNodeDto(
            node.ItemId, node.Amount, node.Runs, node.WholeAmount, node.WholeRuns, node.RecipeId,
            index.Machine[recipe], index.Tier[recipe], index.MultiTierOf(recipe), index.HeatOf(recipe),
            data.DurationTicks[recipe], data.EuT[recipe],
            node.InputsPerRun, catalysts, Outputs(recipe), node.Loop, node.Seed, data.Grid(recipe));
    }

    private List<OutputDto> Outputs(int recipe)
    {
        var index = artifact.Graph.Index;
        var outputs = new List<OutputDto>(index.OutputCount(recipe));
        for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
        {
            outputs.Add(new OutputDto(index.ItemIds[index.OutputItem[o]], index.OutputAmount[o], index.OutputChance[o]));
        }
        return outputs;
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
                    item.MaxStack,
                    item.Aliases.Count > 0 ? item.Aliases : null);
            });

    private RecipeDto ToDto(SolveEntry entry, int recipe, string itemId)
    {
        var index = artifact.Graph.Index;
        var data = artifact.Recipes;
        var candidate = entry.Table.Candidate(recipe, itemId);
        var slots = new List<IReadOnlyList<SlotAlternativeDto>>(index.SlotCount(recipe));
        for (var s = 0; s < index.SlotCount(recipe); s++)
        {
            var alternatives = new List<SlotAlternativeDto>(index.AlternativeCount(recipe, s));
            for (var a = 0; a < index.AlternativeCount(recipe, s); a++)
            {
                var at = index.AlternativeAt(recipe, s, a);
                var item = index.AlternativeItem[at];
                alternatives.Add(new SlotAlternativeDto(
                    index.ItemIds[item], index.AlternativeAmount[at],
                    entry.Table.TryCost(item, out var cost) ? cost * index.AlternativeAmount[at] : null));
            }
            slots.Add(alternatives);
        }
        var catalysts = new List<IReadOnlyList<SlotAlternativeDto>>(data.CatalystSlotCount(recipe));
        for (var s = 0; s < data.CatalystSlotCount(recipe); s++)
        {
            var alternatives = new List<SlotAlternativeDto>(data.CatalystAlternativeCount(recipe, s));
            for (var a = 0; a < data.CatalystAlternativeCount(recipe, s); a++)
            {
                var at = data.CatalystAt(recipe, s, a);
                alternatives.Add(new SlotAlternativeDto(data.CatalystItemId[at], data.CatalystAmount[at], null));
            }
            catalysts.Add(alternatives);
        }
        return new RecipeDto(
            index.RecipeIds[recipe], index.Machine[recipe], index.Tier[recipe],
            index.MultiTierOf(recipe), index.HeatOf(recipe),
            data.DurationTicks[recipe], data.EuT[recipe],
            double.IsPositiveInfinity(candidate) ? null : candidate,
            slots,
            entry.Table.InputsFor(itemId, recipe).Select(input => input.ItemId).ToList(),
            catalysts,
            Outputs(recipe),
            data.Grid(recipe));
    }
}
