using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Services;

// TODO: research if MinableBlockOredicts, FarmableOredictPrefixes can be replaced with the info from the dump

public sealed class EraSolveService(BuilderConfig config, ILogger<EraSolveService> logger) : IEraSolveService
{
    private static readonly HashSet<string> WorldOriginClasses = ["minable_block", "farmable", "log"];

    /// <summary>Leaf classes priced by production era rather than a flat weight.</summary>
    private static readonly HashSet<string> TieredClasses = ["ingot", "gem"];

    public EraSolve Run(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified,
        Dump dump, WorldgenEras worldgen)
    {
        var (era, seeds) = Seed(leafClasses, unified, dump, worldgen);
        var best = Propagate(recipes, era, seeds, unified, dump);
        var tiers = ExtractTiers(recipes, leafClasses, unified, era);
        return new EraSolve(tiers, era, best, seeds);
    }

    /// <summary>Seeds world-origin items. Order matters: the first seed of an item wins,
    /// and only seeds taken before the snapshot are immune to recipes.</summary>
    private (Dictionary<string, int> Era, HashSet<string> Seeds) Seed(
        Dictionary<string, string> leafClasses, UnifiedItems unified, Dump dump, WorldgenEras worldgen)
    {
        // Dusts are not era seeds: a dust obtainable only by macerating its own
        // metal must inherit the metal's era instead of granting era 0.
        var era = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass == "minable_block")
            {
                era[id] = MinableEra(id, unified);
            }
            else if (WorldOriginClasses.Contains(leafClass))
            {
                era[id] = 0;
            }
        }
        foreach (var (id, oredict) in unified.PrimaryOredictByCanonical)
        {
            if (!oredict.StartsWith("ore", StringComparison.Ordinal))
            {
                continue;
            }
            var seed = worldgen.OreBlocks.TryGetValue(id, out var blockEra)
                ? blockEra
                : worldgen.OredictSeed(oredict);
            if (seed is { } seedEra)
            {
                era.TryAdd(id, seedEra);
            }
        }
        foreach (var (id, blockEra) in worldgen.OreBlocks)
        {
            era.TryAdd(id, blockEra);
        }
        foreach (var fluid in dump.Fluids.Values)
        {
            // A null era means the fluid is pumped, and its own recipe decides when.
            if (config.WorldFluids.TryGetValue(fluid.InternalName, out var fluidEra) && fluidEra is { } free)
            {
                era.TryAdd(fluid.Id, free);
            }
        }
        var seeds = new HashSet<string>(era.Keys);

        // Mined small-ore drops start at their dimension era; recipes may still lower them.
        foreach (var (id, dropEra) in worldgen.Drops)
        {
            if (!era.TryGetValue(id, out var current) || dropEra < current)
            {
                era[id] = dropEra;
            }
        }

        logger.LogInformation("  {Seeds:N0} world-origin seeds, {Soft:N0} lowerable drops", seeds.Count, era.Count - seeds.Count);
        return (era, seeds);
    }

    /// <summary>The cheapest world a block can be mined in, by item id or any of its oredicts.</summary>
    private int MinableEra(string id, UnifiedItems unified)
    {
        var cheapest = config.MinableBlockEras.GetValueOrDefault(id, int.MaxValue);
        foreach (var oredict in unified.OredictsByCanonical.GetValueOrDefault(id) ?? [])
        {
            if (config.MinableBlockEras.TryGetValue(oredict, out var era) && era < cheapest)
            {
                cheapest = era;
            }
        }
        return cheapest == int.MaxValue ? 0 : cheapest;
    }

    /// <summary>Runs the min-of-max fixpoint to exhaustion, improving eras strictly.</summary>
    private Dictionary<string, PlannerRecipe> Propagate(
        List<PlannerRecipe> recipes, Dictionary<string, int> era, HashSet<string> seeds,
        UnifiedItems unified, Dump dump)
    {
        var cleanroomIds = dump.Items.Values
            .Where(i => i.Name == config.CleanroomItemName)
            .Select(i => unified.Canonical(i.Id))
            .ToHashSet();
        var consumers = BuildConsumers(recipes, cleanroomIds);
        var machineVoltage = MachineVoltages(unified, dump);

        var best = new Dictionary<string, PlannerRecipe>();
        var queue = new Queue<PlannerRecipe>(recipes);
        var queued = new HashSet<string>(recipes.Select(r => r.Id));
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);

            var candidate = RecipeEra(recipe, era, cleanroomIds, machineVoltage, dump);
            if (candidate == int.MaxValue)
            {
                continue;
            }

            foreach (var output in recipe.Outputs)
            {
                if (seeds.Contains(output.ItemId))
                {
                    continue;
                }
                var floored = cleanroomIds.Contains(output.ItemId)
                    ? Math.Max(candidate, config.CleanroomMinEra)
                    : candidate;
                if (era.TryGetValue(output.ItemId, out var current) && current <= floored)
                {
                    continue;
                }
                era[output.ItemId] = floored;
                best[output.ItemId] = recipe;
                foreach (var consumer in consumers.GetValueOrDefault(output.ItemId) ?? [])
                {
                    if (queued.Add(consumer.Id))
                    {
                        queue.Enqueue(consumer);
                    }
                }
            }
        }

        logger.LogInformation("  {Reachable:N0} items reachable", era.Count);
        return best;
    }

    /// <summary>The era a recipe can first run at, or int.MaxValue while any of its
    /// inputs, machines or the cleanroom is still unreachable.</summary>
    private int RecipeEra(
        PlannerRecipe recipe, Dictionary<string, int> era, HashSet<string> cleanroomIds,
        Dictionary<string, int> machineVoltage, Dump dump)
    {
        var steam = recipe.Tier == 1 && recipe.Heat is null && HasSteamHandler(recipe, era, dump);
        var candidate = MachineEra(recipe, era, machineVoltage, steam);
        if (candidate == int.MaxValue)
        {
            return int.MaxValue;
        }

        if (recipe.RequiresCleanroom)
        {
            var cleanroomEra = CheapestEra(cleanroomIds, era);
            if (cleanroomEra == int.MaxValue)
            {
                return int.MaxValue;
            }
            candidate = Math.Max(candidate, cleanroomEra);
        }

        foreach (var slot in recipe.InputSlotAlternatives)
        {
            var slotEra = CheapestEra(slot, era);
            if (slotEra == int.MaxValue)
            {
                return int.MaxValue;
            }
            candidate = Math.Max(candidate, slotEra);
        }

        return candidate;
    }

    /// <summary>Indexes recipes by every item that can hold them back: input alternatives,
    /// handler machines, and the cleanroom for recipes that need one.</summary>
    private static Dictionary<string, List<PlannerRecipe>> BuildConsumers(
        List<PlannerRecipe> recipes, HashSet<string> cleanroomIds)
    {
        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                foreach (var alternative in slot)
                {
                    Add(consumers, alternative, recipe);
                }
            }
            foreach (var machine in recipe.Machines)
            {
                Add(consumers, machine.ItemId, recipe);
            }
            if (recipe.RequiresCleanroom)
            {
                foreach (var cleanroomId in cleanroomIds)
                {
                    Add(consumers, cleanroomId, recipe);
                }
            }
        }
        return consumers;

        static void Add(Dictionary<string, List<PlannerRecipe>> consumers, string id, PlannerRecipe recipe)
        {
            if (!consumers.TryGetValue(id, out var list))
            {
                consumers[id] = list = [];
            }
            list.Add(recipe);
        }
    }

    /// <summary>A machine buildable early still waits for its input-voltage tier to be powerable.</summary>
    private static Dictionary<string, int> MachineVoltages(UnifiedItems unified, Dump dump)
    {
        var machineVoltage = new Dictionary<string, int>();
        foreach (var (rawId, voltageTier) in dump.MachineVoltageTiers)
        {
            var machineId = unified.Canonical(rawId);
            if (!machineVoltage.TryGetValue(machineId, out var current) || voltageTier < current)
            {
                machineVoltage[machineId] = voltageTier;
            }
        }
        return machineVoltage;
    }

    private Dictionary<string, int> ExtractTiers(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified,
        Dictionary<string, int> era)
    {
        var tiers = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (!TieredClasses.Contains(leafClass))
            {
                continue;
            }
            if (era.TryGetValue(id, out var itemEra))
            {
                tiers[id] = itemEra;
            }
        }

        var recycled = ApplyRecyclingFallback(tiers, recipes, leafClasses);
        InheritTwinTiers(tiers, leafClasses, unified);

        logger.LogInformation("  {Recycled:N0} materials tiered by recycling fallback", recycled);
        return tiers;
    }

    /// <summary>Materials that never bootstrap (recycling-only) fall back to the cheapest direct recipe.</summary>
    private int ApplyRecyclingFallback(
        Dictionary<string, int> tiers, List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses)
    {
        var fallback = new Dictionary<string, int>();
        foreach (var recipe in recipes)
        {
            // Era-only recipes never price, so they cannot stand in for one that does.
            if (recipe.EraOnly)
            {
                continue;
            }
            var intrinsic = Intrinsic(recipe, recipe.BestCaseTier);
            foreach (var output in recipe.Outputs)
            {
                if (!TieredClasses.Contains(leafClasses.GetValueOrDefault(output.ItemId) ?? ""))
                {
                    continue;
                }
                if (tiers.ContainsKey(output.ItemId))
                {
                    continue;
                }
                if (!fallback.TryGetValue(output.ItemId, out var current) || intrinsic < current)
                {
                    fallback[output.ItemId] = intrinsic;
                }
            }
        }
        foreach (var (id, tier) in fallback)
        {
            tiers[id] = tier;
        }
        return fallback.Count;
    }

    /// <summary>A dust is the same material as its ingot or gem; it inherits that tier.</summary>
    private static void InheritTwinTiers(
        Dictionary<string, int> tiers, Dictionary<string, string> leafClasses, UnifiedItems unified)
    {
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass != "dust")
            {
                continue;
            }
            var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
            if (oredict is null || !oredict.StartsWith("dust", StringComparison.Ordinal))
            {
                continue;
            }
            var material = oredict["dust".Length..];
            foreach (var twinOredict in new[] { "ingot" + material, "gem" + material })
            {
                if (unified.CanonicalByOredict.TryGetValue(twinOredict, out var twinId) &&
                    tiers.TryGetValue(twinId, out var twinTier))
                {
                    tiers[id] = twinTier;
                    break;
                }
            }
        }
    }

    public void Explain(EraSolve solve, Dictionary<string, string> names, string itemId) =>
        Explain(solve, names, itemId, depth: 0);

    private static void Explain(EraSolve solve, Dictionary<string, string> names, string itemId, int depth)
    {
        var name = names.GetValueOrDefault(itemId, itemId);
        var indent = new string(' ', depth * 2);
        if (!solve.Era.TryGetValue(itemId, out var era))
        {
            Console.WriteLine($"{indent}{name}: unreachable");
            return;
        }
        if (solve.Seeds.Contains(itemId) || !solve.BestRecipe.TryGetValue(itemId, out var recipe))
        {
            Console.WriteLine($"{indent}{name}: era {era} (seed)");
            return;
        }
        Console.WriteLine($"{indent}{name}: era {era} via {recipe.Machine} tier {recipe.Tier} ({recipe.Id})");
        if (depth >= 12)
        {
            Console.WriteLine($"{indent}  ...");
            return;
        }
        var machine = recipe.Machines
            .Select(m => m.ItemId)
            .Where(id => solve.Era.ContainsKey(id))
            .OrderBy(id => solve.Era[id])
            .FirstOrDefault();
        if (machine is not null && solve.Era[machine] > 0)
        {
            Console.WriteLine($"{indent}  [machine] {names.GetValueOrDefault(machine, machine)}: era {solve.Era[machine]}");
            if (depth < 3)
            {
                Explain(solve, names, machine, depth + 2);
            }
        }
        foreach (var slot in recipe.InputSlotAlternatives)
        {
            var best = slot.Where(solve.Era.ContainsKey).OrderBy(id => solve.Era[id]).FirstOrDefault() ?? slot[0];
            Explain(solve, names, best, depth + 1);
        }
    }

    private static int CheapestEra(IEnumerable<string> ids, Dictionary<string, int> era)
    {
        var cheapest = int.MaxValue;
        foreach (var id in ids)
        {
            if (era.TryGetValue(id, out var itemEra) && itemEra < cheapest)
            {
                cheapest = itemEra;
            }
        }
        return cheapest;
    }

    /// <summary>Steam machines run their map's LV-and-below recipes in the steam era.</summary>
    private bool HasSteamHandler(PlannerRecipe recipe, Dictionary<string, int> era, Dump dump)
    {
        foreach (var machine in recipe.Machines)
        {
            if (!era.ContainsKey(machine.ItemId))
            {
                continue;
            }
            if (!dump.Items.TryGetValue(machine.ItemId, out var item))
            {
                continue;
            }
            if (config.SteamMachinePrefixes.Any(p => item.Name.StartsWith(p, StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The cheapest producible machine gates the recipe's era: each one is floored
    /// at its own input voltage, and only a multiblock brings the hatch allowance with it.</summary>
    private int MachineEra(
        PlannerRecipe recipe, Dictionary<string, int> era, Dictionary<string, int> machineVoltage, bool steam)
    {
        if (recipe.Machines.Count == 0)
        {
            return Intrinsic(recipe, recipe.Tier, steam);
        }
        var best = int.MaxValue;
        foreach (var machine in recipe.Machines)
        {
            if (!era.TryGetValue(machine.ItemId, out var machineEra))
            {
                continue;
            }
            var voltageFloor = machine.Tier ?? machineVoltage.GetValueOrDefault(machine.ItemId, 0);
            var on = Math.Max(
                Math.Max(machineEra, voltageFloor),
                Intrinsic(recipe, recipe.TierOn(machine), steam));
            if (on < best)
            {
                best = on;
            }
        }
        return best;
    }

    /// <summary>A recipe's own floor at a given voltage tier: its coil gate, if it has one.</summary>
    private int Intrinsic(PlannerRecipe recipe, int tier, bool steam = false)
    {
        // Steam machines run their map's LV-and-below recipes in the steam era.
        var voltage = steam && tier == 1 ? 0 : tier;
        return recipe.Heat is { } heat ? Math.Max(voltage, CoilTier(heat)) : voltage;
    }

    private int CoilTier(int heat)
    {
        foreach (var coil in config.Coils)
        {
            if (heat <= coil.MaxHeat)
            {
                return coil.Tier;
            }
        }
        return config.Coils[^1].Tier + 1;
    }
}
