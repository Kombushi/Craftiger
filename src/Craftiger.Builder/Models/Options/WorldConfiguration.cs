namespace Craftiger.Builder.Models.Options;

/// <summary>What the world hands over: minable blocks, world fluids, and farmables.</summary>
public sealed record WorldConfiguration
{
    /// <summary>World-minable leaf blocks by oredict name or, where the dump gives none,
    /// by item id — each at the era of the cheapest world it can be mined in.</summary>
    public required IReadOnlyDictionary<string, int> MinableBlockEras { get; init; }

    /// <summary>Fluids the world hands over, by internal name. Pumpable fluids left off this
    /// list are not world fluids — they price through their own chemistry, and pumping only
    /// gates when they become available.</summary>
    public required IReadOnlyDictionary<string, WorldFluid> WorldFluids { get; init; }

    /// <summary>Oredict prefixes of farmable leaves.</summary>
    public required IReadOnlyList<string> FarmableOredictPrefixes { get; init; }
}
