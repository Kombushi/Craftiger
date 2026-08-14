using System.Diagnostics;
using System.Text.Json;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;

namespace Craftiger.Builder.Services;

public sealed class BuilderPipeline(
    IDumpRepository dumpRepository,
    IUnificationService unification,
    IRecipeTransformService recipeTransform,
    ILeafTaggingService leafTagging,
    IIngotTiersService ingotTiers,
    IAtlasBuilder atlasBuilder,
    IPlannerRepository plannerRepository,
    BuilderConfig config) : IBuilderPipeline
{
    public int Run(BuilderOptions options)
    {
        var total = Stopwatch.StartNew();

        var dump = Stage("read dump", () => dumpRepository.Read(options.DumpPath));
        Console.WriteLine($"  items {dump.Items.Count:N0}, fluids {dump.Fluids.Count:N0}, recipes {dump.Recipes.Count:N0}");

        var unified = Stage("unify", () => unification.Run(dump));
        Console.WriteLine($"  {unified.CanonicalByRawId.Count:N0} oredicted items in {unified.AliasesByCanonical.Count:N0} classes");

        var recipes = Stage("transform recipes", () => recipeTransform.Run(dump, unified));
        var solverRecipes = recipes.Where(r => !r.EraOnly).ToList();
        Console.WriteLine($"  kept {solverRecipes.Count:N0} recipes ({recipes.Count - solverRecipes.Count:N0} era-only)");

        var itemIds = CollectItemIds(solverRecipes);
        var leafClasses = Stage("tag leaves", () => leafTagging.Run(itemIds, dump, unified));
        Console.WriteLine($"  {leafClasses.Count:N0} leaves among {itemIds.Count:N0} items");

        var eraSolve = Stage("tier ingots", () => ingotTiers.Run(recipes, leafClasses, unified, dump));
        Console.WriteLine($"  {eraSolve.Tiers.Count:N0} materials tiered");

        if (options.ExplainItem is { } query)
        {
            Explain(dump, itemIds, eraSolve, query);
            return 0;
        }

        // The items table's atlas_idx and the atlas builder must agree on this order.
        var orderedItemIds = itemIds.Order(StringComparer.Ordinal).ToList();

        var maxTier = solverRecipes.Count == 0 ? 0 : solverRecipes.Max(r => r.Tier);
        var meta = new Dictionary<string, string>
        {
            ["pack_version"] = options.PackVersion,
            ["exporter_version"] = dump.ExporterVersion,
            ["dump_date"] = dump.ExportedAt.ToString("O"),
            ["tier_names"] = JsonSerializer.Serialize(TierLadder.Names.Take(Math.Min(maxTier + 1, TierLadder.Names.Count))),
            ["coils"] = JsonSerializer.Serialize(config.Coils.Select(c => new { c.Name, c.MaxHeat, c.Tier }))
        };

        if (File.Exists(options.ImagesPath))
        {
            var icons = orderedItemIds.Select(id => (id, dump.ImagePathOf(id))).ToList();
            var atlas = Stage("build atlas", () => atlasBuilder.Build(
                options.ImagesPath, icons,
                Path.Combine(options.OutputDir, "atlas.webp"),
                Path.Combine(options.OutputDir, "atlas-offsets.json")));
            Console.WriteLine($"  {atlas.Width}x{atlas.Height} px, cell {atlas.Cell}");
            meta["atlas_width"] = atlas.Width.ToString();
            meta["atlas_height"] = atlas.Height.ToString();
            meta["atlas_cell"] = atlas.Cell.ToString();
        }
        else
        {
            Console.WriteLine($"image.zip not found at {options.ImagesPath}; skipping atlas");
        }

        var plannerPath = Path.Combine(options.OutputDir, "planner.sqlite");
        Stage("write planner.sqlite", () =>
        {
            plannerRepository.Write(plannerPath, new PlannerData(
                dump, unified, solverRecipes, orderedItemIds, leafClasses, eraSolve.Tiers, meta));
            return 0;
        });

        Console.WriteLine($"Done in {total.Elapsed.TotalSeconds:F1}s -> {plannerPath}");
        return 0;
    }

    private void Explain(Dump dump, HashSet<string> itemIds, EraSolve eraSolve, string query)
    {
        var names = itemIds.ToDictionary(id => id, dump.NameOf);
        var target = names.FirstOrDefault(kv => kv.Value.Equals(query, StringComparison.OrdinalIgnoreCase)).Key;
        if (target is null)
        {
            Console.WriteLine($"--explain: no item named '{query}'");
        }
        else
        {
            ingotTiers.Explain(eraSolve, names, target);
        }
    }

    private static HashSet<string> CollectItemIds(List<PlannerRecipe> recipes)
    {
        var ids = new HashSet<string>();
        foreach (var recipe in recipes)
        {
            ids.UnionWith(recipe.Inputs.Keys);
            foreach (var output in recipe.Outputs)
            {
                ids.Add(output.ItemId);
            }
        }
        return ids;
    }

    private static T Stage<T>(string name, Func<T> run)
    {
        var sw = Stopwatch.StartNew();
        var result = run();
        Console.WriteLine($"[{sw.Elapsed.TotalSeconds,6:F1}s] {name}");
        return result;
    }
}
