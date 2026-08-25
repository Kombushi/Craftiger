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
}
