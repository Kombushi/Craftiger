namespace Craftiger.Builder.Models;

public sealed record WorldgenEras(
    Dictionary<string, int> OreBlocks, Dictionary<string, int> Drops, Dictionary<string, int> MaterialEras,
    Dictionary<string, int> Fluids)
{
    /// <summary>Resolves an ore* oredict by exact material name. Worldgen names its placed
    /// blocks by item and the plain ore* oredict is credited directly, so anything left for
    /// name resolution is a variant or a material the world never places — including names
    /// that merely end in a spawning material, like oreCosmicNeutronium — and seeds
    /// nothing at all.</summary>
    public int? OredictSeed(string oredict) =>
        MaterialEras.TryGetValue(oredict["ore".Length..], out var era) ? era : null;
}
