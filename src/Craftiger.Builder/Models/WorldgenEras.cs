namespace Craftiger.Builder.Models;

public sealed record WorldgenEras(
    Dictionary<string, int> OreBlocks, Dictionary<string, int> Drops, List<(string Material, int? Era)> Materials,
    Dictionary<string, int> Fluids)
{
    /// <summary>Resolves ore&lt;Stone&gt;&lt;Material&gt; variant oredicts by longest material suffix
    /// (MeteoricIron before Iron). A material no vein and no small ore places resolves to
    /// nothing at all: GregTech oredicts a stone variant of every material whether or not the
    /// world ever puts one down, and seeding those would date them from the Steam age.</summary>
    public int? OredictSeed(string oredict)
    {
        var name = oredict["ore".Length..];
        foreach (var (material, era) in Materials)
        {
            if (name.EndsWith(material, StringComparison.Ordinal))
            {
                return era;
            }
        }
        return null;
    }
}
