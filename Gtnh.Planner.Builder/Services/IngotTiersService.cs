using Gtnh.Planner.Builder.Interfaces;
using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Services;

// TODO: refactor the service, rename it
// TODO: remove FreeFluids and unify everything under WorldFluidEras
// TODO: what about minable blocks from higher eras? For example, end stone (HV era)
// TODO: refactor WorldDropItemIds workaround
// TODO: bind gems to their corresponding eras
// TODO: research if MinableBlockOredicts, FarmableOredictPrefixes can be replaced with the info from the dump

public sealed class IngotTiersService(BuilderConfig config, IOreWorldgenService oreWorldgen) : IIngotTiersService
{
    private static readonly HashSet<string> WorldOriginClasses = ["minable_block", "farmable", "log", "gem", "free_fluid"];

    public EraSolve Run(List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, UnifiedItems unified, Dump dump)
    {
        // Dusts are not era seeds: a dust obtainable only by macerating its own
        // metal must inherit the metal's era instead of granting era 0.
        var worldgen = oreWorldgen.Run(dump, unified);
        var era = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (WorldOriginClasses.Contains(leafClass))
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
        foreach (var id in config.WorldDropItemIds)
        {
            era.TryAdd(unified.Canonical(id), 0);
        }
        foreach (var fluid in dump.Fluids.Values)
        {
            if (config.WorldFluidEras.TryGetValue(fluid.InternalName, out var fluidEra))
            {
                era.TryAdd(fluid.Id, fluidEra);
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
        var best = new Dictionary<string, PlannerRecipe>();

        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                foreach (var alternative in slot)
                {
                    if (!consumers.TryGetValue(alternative, out var list))
                    {
                        consumers[alternative] = list = [];
                    }
                    list.Add(recipe);
                }
            }
            foreach (var machineId in recipe.MachineItemIds)
            {
                if (!consumers.TryGetValue(machineId, out var list))
                {
                    consumers[machineId] = list = [];
                }
                list.Add(recipe);
            }
        }

        var cleanroomIds = dump.Items.Values
            .Where(i => i.Name == config.CleanroomItemName)
            .Select(i => unified.Canonical(i.Id))
            .ToHashSet();
        foreach (var recipe in recipes)
        {
            if (!recipe.RequiresCleanroom)
            {
                continue;
            }
            foreach (var cleanroomId in cleanroomIds)
            {
                if (!consumers.TryGetValue(cleanroomId, out var list))
                {
                    consumers[cleanroomId] = list = [];
                }
                list.Add(recipe);
            }
        }

        // A machine buildable early still waits for its input-voltage tier to be powerable.
        var machineVoltage = new Dictionary<string, int>();
        foreach (var (rawId, voltageTier) in dump.MachineVoltageTiers)
        {
            var machineId = unified.Canonical(rawId);
            if (!machineVoltage.TryGetValue(machineId, out var current) || voltageTier < current)
            {
                machineVoltage[machineId] = voltageTier;
            }
        }

        var queue = new Queue<PlannerRecipe>(recipes);
        var queued = new HashSet<string>(recipes.Select(r => r.Id));
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);

            var candidate = Intrinsic(recipe);
            if (candidate == 1 && recipe.Heat is null && HasSteamHandler(recipe, era, dump))
            {
                candidate = 0;
            }
            var machineEra = MachineEra(recipe, era, machineVoltage);
            if (machineEra == int.MaxValue)
            {
                continue;
            }
            candidate = Math.Max(candidate, machineEra);
            if (recipe.RequiresCleanroom)
            {
                var cleanroomEra = int.MaxValue;
                foreach (var cleanroomId in cleanroomIds)
                {
                    if (era.TryGetValue(cleanroomId, out var e) && e < cleanroomEra)
                    {
                        cleanroomEra = e;
                    }
                }
                if (cleanroomEra == int.MaxValue)
                {
                    continue;
                }
                candidate = Math.Max(candidate, cleanroomEra);
            }
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                var slotEra = int.MaxValue;
                foreach (var alternative in slot)
                {
                    if (era.TryGetValue(alternative, out var altEra) && altEra < slotEra)
                    {
                        slotEra = altEra;
                    }
                }
                if (slotEra == int.MaxValue)
                {
                    candidate = int.MaxValue;
                    break;
                }
                candidate = Math.Max(candidate, slotEra);
            }
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

        var tiers = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass != "ingot")
            {
                continue;
            }
            if (era.TryGetValue(id, out var itemEra))
            {
                tiers[id] = itemEra;
            }
        }

        // Ingots that never bootstrap (recycling-only) fall back to the cheapest direct recipe.
        var fallback = new Dictionary<string, int>();
        foreach (var recipe in recipes)
        {
            var intrinsic = Intrinsic(recipe);
            foreach (var output in recipe.Outputs)
            {
                if (leafClasses.GetValueOrDefault(output.ItemId) != "ingot")
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

        // A dust is the same material as its ingot; it inherits the ingot's tier.
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
            var ingotOredict = "ingot" + oredict["dust".Length..];
            if (unified.CanonicalByOredict.TryGetValue(ingotOredict, out var ingotId) &&
                tiers.TryGetValue(ingotId, out var ingotTier))
            {
                tiers[id] = ingotTier;
            }
        }

        return new EraSolve(tiers, era, best, seeds);
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
        var machine = recipe.MachineItemIds
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

    /// <summary>Steam machines run their map's LV-and-below recipes in the steam era.</summary>
    private bool HasSteamHandler(PlannerRecipe recipe, Dictionary<string, int> era, Dump dump)
    {
        foreach (var machineId in recipe.MachineItemIds)
        {
            if (!era.ContainsKey(machineId))
            {
                continue;
            }
            if (!dump.Items.TryGetValue(machineId, out var item))
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

    /// <summary>The cheapest producible handler machine gates the recipe's era,
    /// each floored at its own input-voltage tier.</summary>
    private static int MachineEra(
        PlannerRecipe recipe, Dictionary<string, int> era, Dictionary<string, int> machineVoltage)
    {
        if (recipe.MachineItemIds.Count == 0)
        {
            return 0;
        }
        var best = int.MaxValue;
        foreach (var machineId in recipe.MachineItemIds)
        {
            if (!era.TryGetValue(machineId, out var machineEra))
            {
                continue;
            }
            var floored = Math.Max(machineEra, machineVoltage.GetValueOrDefault(machineId, 0));
            if (floored < best)
            {
                best = floored;
            }
        }
        return best;
    }

    private int Intrinsic(PlannerRecipe recipe) =>
        recipe.Heat is { } heat ? Math.Max(recipe.Tier, CoilTier(heat)) : recipe.Tier;

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
