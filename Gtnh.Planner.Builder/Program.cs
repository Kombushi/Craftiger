using System.Diagnostics;
using Gtnh.Planner.Builder;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Gtnh.Planner.Builder <dump.sqlite> [--output <dir>] [--pack-version <version>]");
    return 1;
}

var dumpPath = args[0];
var outputDir = ".";
var packVersion = "2.9.0-beta2";
for (var i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "--output") outputDir = args[i + 1];
    if (args[i] == "--pack-version") packVersion = args[i + 1];
}

if (!File.Exists(dumpPath))
{
    Console.Error.WriteLine($"Dump not found: {dumpPath}");
    return 1;
}
Directory.CreateDirectory(outputDir);

var config = BuilderConfig.Default;
var total = Stopwatch.StartNew();

var dump = Stage("read dump", () => DumpReader.Read(dumpPath));
Console.WriteLine($"  items {dump.Items.Count:N0}, fluids {dump.Fluids.Count:N0}, recipes {dump.Recipes.Count:N0}");

var unified = Stage("unify", () => Unification.Run(dump));
Console.WriteLine($"  {unified.CanonicalByRawId.Count:N0} oredicted items in {unified.AliasesByCanonical.Count:N0} classes");

var recipes = Stage("transform recipes", () => RecipeTransform.Run(dump, unified, config));
Console.WriteLine($"  kept {recipes.Count:N0} recipes");

var itemIds = PlannerWriter.CollectItemIds(recipes);
var leafClasses = Stage("tag leaves", () => LeafTagging.Run(itemIds, dump, unified, config));
Console.WriteLine($"  {leafClasses.Count:N0} leaves among {itemIds.Count:N0} items");

var ingotTiers = Stage("tier ingots", () => IngotTiers.Run(recipes, leafClasses, config));
Console.WriteLine($"  {ingotTiers.Count:N0} ingots tiered");

var plannerPath = Path.Combine(outputDir, "planner.sqlite");
Stage("write planner.sqlite", () =>
{
    PlannerWriter.Write(plannerPath, dump, unified, recipes, leafClasses, ingotTiers, config, packVersion);
    return 0;
});

Console.WriteLine($"Done in {total.Elapsed.TotalSeconds:F1}s -> {plannerPath}");
return 0;

static T Stage<T>(string name, Func<T> run)
{
    var sw = Stopwatch.StartNew();
    var result = run();
    Console.WriteLine($"[{sw.Elapsed.TotalSeconds,6:F1}s] {name}");
    return result;
}