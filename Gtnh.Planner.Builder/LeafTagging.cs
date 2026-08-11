namespace Gtnh.Planner.Builder;

/// <summary>Assigns leaf classes to canonical items by oredict and config lists.</summary>
public static class LeafTagging
{
    public static Dictionary<string, string> Run(
        IEnumerable<string> canonicalIds, Dump dump, UnifiedItems unified, BuilderConfig config)
    {
        var classes = new Dictionary<string, string>();

        foreach (var id in canonicalIds)
        {
            if (dump.Fluids.TryGetValue(id, out var fluid))
            {
                if (config.FreeFluids.Contains(fluid.InternalName)) classes[id] = "free_fluid";
                continue;
            }

            var oredict = unified.PrimaryOredictByCanonical.GetValueOrDefault(id);
            if (oredict is null) continue;

            var leafClass = Classify(oredict, config);
            if (leafClass is not null) classes[id] = leafClass;
        }

        return classes;
    }

    private static string? Classify(string oredict, BuilderConfig config)
    {
        if (config.MinableBlockOredicts.Contains(oredict)) return "minable_block";
        if (config.FarmableOredictPrefixes.Any(p => oredict.StartsWith(p, StringComparison.Ordinal))) return "farmable";
        if (oredict.StartsWith("dustSmall", StringComparison.Ordinal)) return "dust_small";
        if (oredict.StartsWith("dustTiny", StringComparison.Ordinal)) return "dust_tiny";
        if (oredict.StartsWith("dust", StringComparison.Ordinal)) return "dust";
        if (oredict.StartsWith("ingot", StringComparison.Ordinal)) return "ingot";
        if (oredict.StartsWith("gem", StringComparison.Ordinal)) return "gem";
        if (oredict.StartsWith("logWood", StringComparison.Ordinal)) return "log";
        return null;
    }
}

public sealed record EraSolve(
    Dictionary<string, int> Tiers,
    Dictionary<string, int> Era,
    Dictionary<string, PlannerRecipe> BestRecipe,
    HashSet<string> Seeds);

/// <summary>Tiers ingots by production era: a min-of-max fixpoint over the recipe graph.</summary>
public static class IngotTiers
{
    private static readonly HashSet<string> WorldOriginClasses =
        ["minable_block", "farmable", "log", "gem", "free_fluid"];

    public static EraSolve Run(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses,
        UnifiedItems unified, Dump dump, BuilderConfig config)
    {
        // Dusts are not era seeds: a dust obtainable only by macerating its own
        // metal must inherit the metal's era instead of granting era 0.
        var era = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
            if (WorldOriginClasses.Contains(leafClass)) era[id] = 0;
        foreach (var (id, oredict) in unified.PrimaryOredictByCanonical)
            if (oredict.StartsWith("ore", StringComparison.Ordinal)) era.TryAdd(id, OreSeedEra(oredict, config));
        foreach (var id in config.WorldDropItemIds)
            era.TryAdd(unified.Canonical(id), 0);
        foreach (var fluid in dump.Fluids.Values)
            if (config.WorldFluidEras.TryGetValue(fluid.InternalName, out var fluidEra))
                era.TryAdd(fluid.Id, fluidEra);
        var seeds = new HashSet<string>(era.Keys);
        var best = new Dictionary<string, PlannerRecipe>();

        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                foreach (var alternative in slot)
                {
                    if (!consumers.TryGetValue(alternative, out var list)) consumers[alternative] = list = [];
                    list.Add(recipe);
                }
            }
            foreach (var machineId in recipe.MachineItemIds)
            {
                if (!consumers.TryGetValue(machineId, out var list)) consumers[machineId] = list = [];
                list.Add(recipe);
            }
        }

        var cleanroomIds = dump.Items.Values
            .Where(i => i.Name == config.CleanroomItemName)
            .Select(i => unified.Canonical(i.Id))
            .ToHashSet();
        foreach (var recipe in recipes)
        {
            if (!recipe.RequiresCleanroom) continue;
            foreach (var cleanroomId in cleanroomIds)
            {
                if (!consumers.TryGetValue(cleanroomId, out var list)) consumers[cleanroomId] = list = [];
                list.Add(recipe);
            }
        }

        var queue = new Queue<PlannerRecipe>(recipes);
        var queued = new HashSet<string>(recipes.Select(r => r.Id));
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);

            var candidate = Intrinsic(recipe, config);
            if (candidate == 1 && recipe.Heat is null && HasSteamHandler(recipe, era, dump, config))
                candidate = 0;
            var machineEra = MachineEra(recipe, era);
            if (machineEra == int.MaxValue) continue;
            candidate = Math.Max(candidate, machineEra);
            if (recipe.RequiresCleanroom)
            {
                var cleanroomEra = int.MaxValue;
                foreach (var cleanroomId in cleanroomIds)
                    if (era.TryGetValue(cleanroomId, out var e) && e < cleanroomEra) cleanroomEra = e;
                if (cleanroomEra == int.MaxValue) continue;
                candidate = Math.Max(candidate, cleanroomEra);
            }
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                var slotEra = int.MaxValue;
                foreach (var alternative in slot)
                    if (era.TryGetValue(alternative, out var altEra) && altEra < slotEra) slotEra = altEra;
                if (slotEra == int.MaxValue) { candidate = int.MaxValue; break; }
                candidate = Math.Max(candidate, slotEra);
            }
            if (candidate == int.MaxValue) continue;

            foreach (var output in recipe.Outputs)
            {
                if (seeds.Contains(output.ItemId)) continue;
                if (era.TryGetValue(output.ItemId, out var current) && current <= candidate) continue;
                era[output.ItemId] = candidate;
                best[output.ItemId] = recipe;
                foreach (var consumer in consumers.GetValueOrDefault(output.ItemId) ?? [])
                    if (queued.Add(consumer.Id)) queue.Enqueue(consumer);
            }
        }

        var tiers = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
        {
            if (leafClass != "ingot") continue;
            if (era.TryGetValue(id, out var itemEra)) tiers[id] = itemEra;
        }

        // Ingots that never bootstrap (recycling-only) fall back to the cheapest direct recipe.
        var fallback = new Dictionary<string, int>();
        foreach (var recipe in recipes)
        {
            var intrinsic = Intrinsic(recipe, config);
            foreach (var output in recipe.Outputs)
            {
                if (leafClasses.GetValueOrDefault(output.ItemId) != "ingot") continue;
                if (tiers.ContainsKey(output.ItemId)) continue;
                if (!fallback.TryGetValue(output.ItemId, out var current) || intrinsic < current)
                    fallback[output.ItemId] = intrinsic;
            }
        }
        foreach (var (id, tier) in fallback) tiers[id] = tier;

        return new EraSolve(tiers, era, best, seeds);
    }

    public static void Explain(EraSolve solve, Dictionary<string, string> names, string itemId, int depth = 0)
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
            if (depth < 3) Explain(solve, names, machine, depth + 2);
        }
        foreach (var slot in recipe.InputSlotAlternatives)
        {
            var best = slot.Where(solve.Era.ContainsKey).OrderBy(id => solve.Era[id]).FirstOrDefault() ?? slot[0];
            Explain(solve, names, best, depth + 1);
        }
    }

    /// <summary>Steam machines run their map's LV-and-below recipes in the steam era.</summary>
    private static bool HasSteamHandler(
        PlannerRecipe recipe, Dictionary<string, int> era, Dump dump, BuilderConfig config)
    {
        foreach (var machineId in recipe.MachineItemIds)
        {
            if (!era.ContainsKey(machineId)) continue;
            if (!dump.Items.TryGetValue(machineId, out var item)) continue;
            if (config.SteamMachinePrefixes.Any(p => item.Name.StartsWith(p, StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    /// <summary>The cheapest producible handler machine gates the recipe's era.</summary>
    private static int MachineEra(PlannerRecipe recipe, Dictionary<string, int> era)
    {
        if (recipe.MachineItemIds.Count == 0) return 0;
        var best = int.MaxValue;
        foreach (var machineId in recipe.MachineItemIds)
            if (era.TryGetValue(machineId, out var machineEra) && machineEra < best) best = machineEra;
        return best;
    }

    private static int OreSeedEra(string oredict, BuilderConfig config)
    {
        var material = oredict["ore".Length..];
        foreach (var (suffix, era) in config.OreEras)
            if (material.EndsWith(suffix, StringComparison.Ordinal)) return era;
        return 0;
    }

    private static int Intrinsic(PlannerRecipe recipe, BuilderConfig config) =>
        recipe.Heat is { } heat ? Math.Max(recipe.Tier, CoilTier(heat, config)) : recipe.Tier;

    public static int CoilTier(int heat, BuilderConfig config)
    {
        foreach (var coil in config.Coils)
            if (heat <= coil.MaxHeat) return coil.Tier;
        return config.Coils[^1].Tier + 1;
    }
}