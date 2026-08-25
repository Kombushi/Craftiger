namespace Craftiger.Builder.Models.Eras;

/// <summary>Where the world places things: ore blocks, mined drops, materials and pumpable fluids, each at its cheapest era.</summary>
public sealed record WorldgenEras(
    IReadOnlyDictionary<string, int> OreBlocks,
    IReadOnlyDictionary<string, int> Drops,
    IReadOnlyDictionary<string, int> MaterialEras,
    IReadOnlyDictionary<string, int> Fluids)
{
    /// <summary>Resolves an ore* oredict by exact material name; a name the world never places seeds nothing.</summary>
    public int? OredictSeed(string oredict) =>
        MaterialEras.TryGetValue(oredict["ore".Length..], out var era) ? era : null;
}
