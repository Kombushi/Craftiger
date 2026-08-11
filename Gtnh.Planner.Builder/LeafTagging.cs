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

/// <summary>Tiers ingots by production era: a min-of-max fixpoint over the recipe graph.</summary>
public static class IngotTiers
{
    public static Dictionary<string, int> Run(
        List<PlannerRecipe> recipes, Dictionary<string, string> leafClasses, BuilderConfig config)
    {
        var era = new Dictionary<string, int>();
        foreach (var (id, leafClass) in leafClasses)
            if (leafClass != "ingot") era[id] = 0;

        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var input in recipe.Inputs.Keys)
            {
                if (!consumers.TryGetValue(input, out var list)) consumers[input] = list = [];
                list.Add(recipe);
            }
        }

        var queue = new Queue<PlannerRecipe>(recipes);
        var queued = new HashSet<string>(recipes.Select(r => r.Id));
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);

            var candidate = Intrinsic(recipe, config);
            foreach (var input in recipe.Inputs.Keys)
            {
                if (!era.TryGetValue(input, out var inputEra)) { candidate = int.MaxValue; break; }
                candidate = Math.Max(candidate, inputEra);
            }
            if (candidate == int.MaxValue) continue;

            foreach (var output in recipe.Outputs)
            {
                if (era.TryGetValue(output.ItemId, out var current) && current <= candidate) continue;
                era[output.ItemId] = candidate;
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

        return tiers;
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