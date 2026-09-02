namespace Craftiger.Builder.Models.Options;

/// <summary>What the world hands over: minable blocks, world fluids, and farmables.</summary>
public sealed record WorldConfiguration
{
    /// <summary>World-minable leaf blocks by oredict name or item id, each at the era of the cheapest world that has them.</summary>
    public required IReadOnlyDictionary<string, int> MinableBlockEras { get; init; }

    /// <summary>Fluids the world hands over, by internal name; pumpable fluids left off price through their own chemistry.</summary>
    public required IReadOnlyDictionary<string, WorldFluid> WorldFluids { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }

    /// <summary>Whether the world hands the item over, by its id or any of its oredicts.</summary>
    public bool IsMinable(string id, IEnumerable<string> oredicts) =>
        MinableBlockEras.ContainsKey(id) || oredicts.Any(MinableBlockEras.ContainsKey);
}
