using System.Globalization;
using System.Text.RegularExpressions;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed partial class FuelExtractionService(
    IOptions<FuelsConfiguration> config,
    ILogger<FuelExtractionService> logger) : IFuelExtractionService
{
    [GeneratedRegex(@"^(.+?)\s+Boiler:\s*(.+)$")]
    private static partial Regex BoilerLine();

    /// <summary>RTG special values are burn years; a Minecraft day is 24,000 ticks.</summary>
    private const long TicksPerYear = 365L * 24000L;

    /// <summary>Solid fuels burn as 1000 mB worth of the special value, per GT5-Unofficial's generator math.</summary>
    private const long SolidUnitMb = 1000;

    /// <summary>Every run re-verifies one known value so the Standard family's unit reading can never drift silently.</summary>
    private const double BenzeneEuPerMb = 360.0;

    private readonly FuelsConfiguration _config = config.Value;

    public FuelData Run(Dump dump, UnifiedItems unified)
    {
        var recipesByMap = new Dictionary<string, List<DumpRecipe>>();
        var mapsByName = new Dictionary<string, DumpRecipeMap>();
        foreach (var recipe in dump.Recipes)
        {
            if (dump.RecipeMapByTypeId.TryGetValue(recipe.RecipeTypeId, out var map) && map.IsFuel)
            {
                mapsByName[map.UnlocalizedName] = map;
                (recipesByMap.TryGetValue(map.UnlocalizedName, out var list)
                    ? list
                    : recipesByMap[map.UnlocalizedName] = []).Add(recipe);
            }
        }
        foreach (var map in dump.RecipeMapByTypeId.Values.Where(m => m.IsFuel))
        {
            mapsByName.TryAdd(map.UnlocalizedName, map);
        }

        var unclassified = mapsByName.Keys.Where(m => !_config.MapFamilies.ContainsKey(m)).ToList();
        if (unclassified.Count > 0)
        {
            throw new InvalidOperationException(
                $"fuel maps without a configured family: {string.Join(", ", unclassified)}");
        }

        var fuels = new Dictionary<(string Map, string ItemId), PlannerFuel>();
        var boilerFuels = new Dictionary<(string ItemId, string Boiler), PlannerBoilerFuel>();
        foreach (var (mapName, map) in mapsByName)
        {
            var recipes = recipesByMap.GetValueOrDefault(mapName) ?? [];
            switch (_config.MapFamilies[mapName])
            {
                case "Excluded":
                    break;
                case "Empty":
                    if (recipes.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"fuel map {mapName} is classified Empty but has {recipes.Count} recipes");
                    }
                    break;
                case "Standard":
                    foreach (var recipe in recipes)
                    {
                        ExtractStandard(dump, unified, map, recipe, fuels);
                    }
                    break;
                case "Rtg":
                    foreach (var recipe in recipes)
                    {
                        ExtractRtg(dump, unified, map, recipe, fuels);
                    }
                    break;
                case "Timed":
                    foreach (var recipe in recipes)
                    {
                        ExtractTimed(dump, map, recipe, fuels);
                    }
                    break;
                case "Boiler":
                    foreach (var recipe in recipes)
                    {
                        ExtractBoiler(dump, unified, recipe, boilerFuels);
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"unknown fuel family '{_config.MapFamilies[mapName]}' for map {mapName}");
            }
        }

        AssertBenzene(dump, fuels);
        return new FuelData([.. fuels.Values], [.. boilerFuels.Values]);
    }

    private void ExtractStandard(
        Dump dump, UnifiedItems unified, DumpRecipeMap map, DumpRecipe recipe,
        Dictionary<(string, string), PlannerFuel> fuels)
    {
        var special = dump.GtByRecipeId.GetValueOrDefault(recipe.Id)?.SpecialValue;
        if (special is null or <= 0)
        {
            return;
        }

        foreach (var (fluidId, euPerUnit) in StandardUnits(dump, unified, recipe, special.Value))
        {
            Put(fuels, new PlannerFuel(map.Name, fluidId, 1, euPerUnit, null, null));
        }
    }

    /// <summary>A cell burns as its contained fluid at the special value per mB, a direct fluid likewise, a bare item as 1000 mB worth.</summary>
    private static IEnumerable<(string ItemId, double EuPerUnit)> StandardUnits(
        Dump dump, UnifiedItems unified, DumpRecipe recipe, long special)
    {
        foreach (var (_, groupId) in dump.ItemInputsOf(recipe.Id))
        {
            foreach (var stack in dump.StacksOf(groupId))
            {
                if (dump.ContainersByItemId.TryGetValue(stack.ItemId, out var container))
                {
                    yield return (container.FluidId, special);
                }
                else
                {
                    yield return (unified.Canonical(stack.ItemId), (double)special * SolidUnitMb);
                }
            }
        }
        foreach (var input in dump.FluidInputsOf(recipe.Id))
        {
            foreach (var (fluidId, _) in input.Members)
            {
                yield return (fluidId, special);
            }
        }
    }

    private void ExtractRtg(
        Dump dump, UnifiedItems unified, DumpRecipeMap map, DumpRecipe recipe,
        Dictionary<(string, string), PlannerFuel> fuels)
    {
        var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
        if (gt?.SpecialValue is null or <= 0 || gt.Voltage is null or <= 0)
        {
            logger.LogWarning("RTG fuel {Recipe} lacks burn years or EU/t; skipped", recipe.Id);
            return;
        }

        foreach (var (_, groupId) in dump.ItemInputsOf(recipe.Id))
        {
            foreach (var stack in dump.StacksOf(groupId))
            {
                Put(fuels, new PlannerFuel(
                    map.Name, unified.Canonical(stack.ItemId), stack.Size,
                    null, gt.Voltage, gt.SpecialValue.Value * TicksPerYear));
            }
        }
    }

    private void ExtractTimed(
        Dump dump, DumpRecipeMap map, DumpRecipe recipe,
        Dictionary<(string, string), PlannerFuel> fuels)
    {
        var gt = dump.GtByRecipeId.GetValueOrDefault(recipe.Id);
        if (gt?.SpecialValue is null or <= 0 || gt.Duration <= 0)
        {
            logger.LogWarning("timed fuel {Recipe} lacks total EU or duration; skipped", recipe.Id);
            return;
        }
        foreach (var input in dump.FluidInputsOf(recipe.Id))
        {
            foreach (var (fluidId, amount) in input.Members)
            {
                Put(fuels, new PlannerFuel(
                    map.Name, fluidId, amount,
                    null, gt.SpecialValue.Value / (double)gt.Duration, gt.Duration));
            }
        }
    }

    private void ExtractBoiler(
        Dump dump, UnifiedItems unified, DumpRecipe recipe,
        Dictionary<(string, string), PlannerBoilerFuel> boilerFuels)
    {
        var info = dump.GtByRecipeId.GetValueOrDefault(recipe.Id)?.AdditionalInfo;
        if (string.IsNullOrWhiteSpace(info))
        {
            logger.LogWarning("boiler fuel {Recipe} has no burn-time text; skipped", recipe.Id);
            return;
        }

        var burns = new List<(string Boiler, double Seconds)>();
        foreach (var line in info.Split('\n'))
        {
            var match = BoilerLine().Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }
            if (double.TryParse(
                match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var seconds))
            {
                burns.Add((match.Groups[1].Value, seconds));
            }
        }
        if (burns.Count == 0)
        {
            // "Not allowed" on every generation: the fuel exists but no boiler takes it.
            return;
        }

        foreach (var itemId in BoilerFuelIds(dump, unified, recipe))
        {
            foreach (var (boiler, seconds) in burns)
            {
                var fuel = new PlannerBoilerFuel(itemId, boiler, seconds);
                if (boilerFuels.TryGetValue((itemId, boiler), out var existing))
                {
                    if (existing != fuel)
                    {
                        logger.LogWarning(
                            "boiler fuel {Item} has two {Boiler} burn times; first kept",
                            itemId, boiler);
                    }
                    continue;
                }
                boilerFuels[(itemId, boiler)] = fuel;
            }
        }
    }

    private static IEnumerable<string> BoilerFuelIds(
        Dump dump, UnifiedItems unified, DumpRecipe recipe)
    {
        foreach (var (_, groupId) in dump.ItemInputsOf(recipe.Id))
        {
            foreach (var stack in dump.StacksOf(groupId))
            {
                yield return dump.ContainersByItemId.TryGetValue(stack.ItemId, out var container)
                    ? container.FluidId
                    : unified.Canonical(stack.ItemId);
            }
        }
        foreach (var input in dump.FluidInputsOf(recipe.Id))
        {
            foreach (var (fluidId, _) in input.Members)
            {
                yield return fluidId;
            }
        }
    }

    private void Put(Dictionary<(string, string), PlannerFuel> fuels, PlannerFuel fuel)
    {
        if (fuels.TryGetValue((fuel.Map, fuel.ItemId), out var existing))
        {
            if (existing != fuel)
            {
                logger.LogWarning(
                    "fuel {Item} appears twice in {Map} with different values; first kept",
                    fuel.ItemId, fuel.Map);
            }
            return;
        }
        fuels[(fuel.Map, fuel.ItemId)] = fuel;
    }

    private static void AssertBenzene(
        Dump dump, Dictionary<(string, string), PlannerFuel> fuels)
    {
        var benzene = fuels.Values.FirstOrDefault(f =>
            f.Map == "Gas Turbine Fuel" && dump.NameOf(f.ItemId) == "Benzene");
        if (benzene is null || benzene.EuPerUnit != BenzeneEuPerMb)
        {
            throw new InvalidOperationException(
                $"benzene gas-turbine fuel check failed: expected {BenzeneEuPerMb} EU/mB, "
                + $"got {benzene?.EuPerUnit.ToString() ?? "no row"} — fuel unit semantics drifted");
        }
    }
}
