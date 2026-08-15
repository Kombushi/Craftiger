namespace Craftiger.Builder.Models;

public sealed record WorldgenEras(
    Dictionary<string, int> OreBlocks, Dictionary<string, int> Drops, List<(string Material, int? Era)> Materials,
    Dictionary<string, int> Fluids)
{
    /// <summary>Resolves ore&lt;Stone&gt;&lt;Material&gt; variant oredicts by longest material suffix (MeteoricIron before Iron).</summary>
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
        return 0;
    }
}
