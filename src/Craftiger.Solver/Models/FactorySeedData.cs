namespace Craftiger.Solver.Models;

/// <summary>The curated auto-infinite primitives, item id to seed kind (WORLD, FARM, MOB).
/// Seeds are the base of the per-solve auto-infinite fixpoint and buy at weight zero in the
/// resource layer; MOB seeds only count when the factory's mob-farm toggle is on.</summary>
public sealed record FactorySeedData(IReadOnlyDictionary<string, string> Kinds)
{
    public const string MobKind = "MOB";

    public static readonly FactorySeedData Empty = new(new Dictionary<string, string>());
}
