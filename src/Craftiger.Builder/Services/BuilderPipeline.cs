using System.Diagnostics;
using System.Text.Json;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class BuilderPipeline(
    IDumpRepository dumpRepository,
    IUnificationService unification,
    IRecipeTransformService recipeTransform,
    IBlockBreakRecipeService blockBreak,
    IUndergroundFluidRecipeService undergroundFluid,
    ICropHarvestRecipeService cropHarvest,
    ILeafTaggingService leafTagging,
    IWorldgenErasService worldgenEras,
    IEraSolveService eraSolveService,
    IPriceCheckService priceCheck,
    IAtlasBuilder atlasBuilder,
    IPlannerRepository plannerRepository,
    IOptions<BuilderOptions> options,
    IOptions<BuilderConfig> config,
    ILogger<BuilderPipeline> logger) : IBuilderPipeline
{
    private readonly BuilderOptions _options = options.Value;
    private readonly BuilderConfig _config = config.Value;

    public int Run()
    {
        if (!File.Exists(_options.DumpPath))
        {
            logger.LogError("Dump not found: {DumpPath}", _options.DumpPath);
            return 1;
        }
        Directory.CreateDirectory(_options.OutputDir);

        var total = Stopwatch.StartNew();

        var dump = Stage("read dump", () => dumpRepository.Read(_options.DumpPath));
        logger.LogInformation("  items {Items:N0}, fluids {Fluids:N0}, recipes {Recipes:N0}", dump.Items.Count, dump.Fluids.Count, dump.Recipes.Count);

        var unified = Stage("unify", () => unification.Run(dump));
        logger.LogInformation("  {Oredicted:N0} oredicted items in {Classes:N0} classes", unified.CanonicalByRawId.Count, unified.AliasesByCanonical.Count);

        var recipes = Stage("transform recipes", () => recipeTransform.Run(dump, unified));
        recipes.AddRange(Stage("break blocks", () => blockBreak.Run(dump, unified)));
        recipes.AddRange(Stage("harvest crops", () => cropHarvest.Run(dump, unified)));
        var solverRecipes = recipes.Where(r => !r.EraOnly).ToList();
        logger.LogInformation("  kept {Kept:N0} recipes ({EraOnly:N0} era-only)", solverRecipes.Count, recipes.Count - solverRecipes.Count);

        var itemIds = CollectItemIds(solverRecipes);
        var produced = CollectProducedIds(solverRecipes);
        var leafClasses = Stage("tag leaves", () => leafTagging.Run(itemIds, produced, dump, unified));
        logger.LogInformation("  {Leaves:N0} leaves among {Items:N0} items", leafClasses.Count, itemIds.Count);

        var worldgen = Stage("resolve worldgen eras", () => worldgenEras.Run(dump, unified));
        recipes.AddRange(Stage("pump fluids", () => undergroundFluid.Run(dump, unified, worldgen)));

        var eraSolve = Stage("solve eras", () => eraSolveService.Run(recipes, leafClasses, unified, dump, worldgen));
        logger.LogInformation("  {Materials:N0} materials tiered", eraSolve.Tiers.Count);

        // Only now are tiers known, so only now can an unpriceable leaf be told apart.
        leafTagging.Prune(leafClasses, eraSolve.Tiers, unified, dump);
        logger.LogInformation("  {Leaves:N0} leaves kept", leafClasses.Count);
        var itemParents = leafTagging.Parents(leafClasses, eraSolve.Tiers, unified, dump);

        if (_options.ExplainItem is { } query)
        {
            Explain(dump, itemIds, eraSolve, query);
            return 0;
        }

        var leafWeights = leafTagging.Overrides(dump);
        var prices = Stage("check prices", () => priceCheck.Run(
            solverRecipes, leafClasses, eraSolve.Tiers, leafWeights, unified, dump));

        // The items table's atlas_idx and the atlas builder must agree on this order.
        var orderedItemIds = itemIds.Order(StringComparer.Ordinal).ToList();

        var maxTier = solverRecipes.Count == 0 ? 0 : solverRecipes.Max(r => r.SingleBlockTier);
        var meta = new Dictionary<string, string>
        {
            ["pack_version"] = _options.PackVersion,
            ["exporter_version"] = dump.ExporterVersion,
            ["dump_date"] = dump.ExportedAt.ToString("O"),
            ["tier_names"] = JsonSerializer.Serialize(TierLadder.Names.Take(Math.Min(maxTier + 1, TierLadder.Names.Count))),
            ["coils"] = JsonSerializer.Serialize(_config.Coils.Select(c => new { c.Name, c.MaxHeat, c.Tier })),
            ["price_leaks"] = prices.Undercut.ToString(),
            ["price_free_items"] = prices.Free.ToString(),
            ["price_converged"] = prices.Converged ? "1" : "0"
        };

        if (File.Exists(_options.ImagesPath))
        {
            var icons = orderedItemIds.Select(id => (id, dump.ImagePathOf(id))).ToList();
            var atlas = Stage("build atlas", () => atlasBuilder.Build(
                _options.ImagesPath, icons,
                Path.Combine(_options.OutputDir, "atlas.webp"),
                Path.Combine(_options.OutputDir, "atlas-offsets.json")));
            logger.LogInformation("  {Width}x{Height} px, cell {Cell}", atlas.Width, atlas.Height, atlas.Cell);
            meta["atlas_width"] = atlas.Width.ToString();
            meta["atlas_height"] = atlas.Height.ToString();
            meta["atlas_cell"] = atlas.Cell.ToString();
        }
        else
        {
            logger.LogWarning("image.zip not found at {ImagesPath}; skipping atlas", _options.ImagesPath);
        }

        var plannerPath = Path.Combine(_options.OutputDir, "planner.sqlite");
        Stage("write planner.sqlite", () =>
        {
            plannerRepository.Write(plannerPath, new PlannerData(
                dump, unified, solverRecipes, orderedItemIds, leafClasses, eraSolve.Tiers,
                itemParents, leafWeights, eraSolve.MachineEras, meta));
            return 0;
        });

        logger.LogInformation("Done in {Seconds:F1}s -> {PlannerPath}", total.Elapsed.TotalSeconds, plannerPath);
        return 0;
    }

    private void Explain(Dump dump, HashSet<string> itemIds, EraSolve eraSolve, string query)
    {
        var names = itemIds.ToDictionary(id => id, dump.NameOf);
        var target = names.FirstOrDefault(kv => kv.Value.Equals(query, StringComparison.OrdinalIgnoreCase)).Key;
        if (target is null)
        {
            logger.LogWarning("no item named {Query}", query);
        }
        else
        {
            eraSolveService.Explain(eraSolve, names, target);
        }
    }

    private static HashSet<string> CollectProducedIds(List<PlannerRecipe> recipes)
    {
        var ids = new HashSet<string>();
        foreach (var recipe in recipes)
        {
            foreach (var output in recipe.Outputs)
            {
                ids.Add(output.ItemId);
            }
        }
        return ids;
    }

    private static HashSet<string> CollectItemIds(List<PlannerRecipe> recipes)
    {
        var ids = new HashSet<string>();
        foreach (var recipe in recipes)
        {
            ids.UnionWith(recipe.Inputs.Keys);
            foreach (var choice in recipe.Choices)
            {
                ids.UnionWith(choice.Alternatives.Select(a => a.ItemId));
            }
            foreach (var catalyst in recipe.Catalysts)
            {
                ids.UnionWith(catalyst.Alternatives.Select(a => a.ItemId));
            }
            foreach (var output in recipe.Outputs)
            {
                ids.Add(output.ItemId);
            }
        }
        return ids;
    }

    private T Stage<T>(string name, Func<T> run)
    {
        var sw = Stopwatch.StartNew();
        var result = run();
        logger.LogInformation("[{Seconds,6:F1}s] {Stage}", sw.Elapsed.TotalSeconds, name);
        return result;
    }
}