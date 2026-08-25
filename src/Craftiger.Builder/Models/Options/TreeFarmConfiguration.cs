using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Models.Options;

/// <summary>The Tree Growth Simulator's recipe map and its curated tool table, as GT5-Unofficial's controller reads the input bus.</summary>
public sealed record TreeFarmConfiguration
{
    /// <summary>The recipe map's unlocalized name.</summary>
    public required string Map { get; init; }

    /// <summary>Every tool each mode accepts; the builder ships only the best multiplier's tools, since catalysts never price.</summary>
    public required IReadOnlyDictionary<TreeFarmMode, IReadOnlyList<TreeFarmTool>> Tools { get; init; }
}
