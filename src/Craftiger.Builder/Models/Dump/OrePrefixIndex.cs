namespace Craftiger.Builder.Models.Dump;

/// <summary>Longest-prefix lookup over GT's ore-prefix registry: dustImpureIron belongs to dustImpure, never to dust.</summary>
public sealed record OrePrefixIndex
{
    private readonly IReadOnlyDictionary<string, DumpOrePrefix> _byName;
    private readonly int[] _lengths;

    public OrePrefixIndex(IReadOnlyDictionary<string, DumpOrePrefix> prefixes)
    {
        _byName = prefixes;
        _lengths = prefixes.Keys.Select(name => name.Length).Distinct().OrderDescending().ToArray();
    }

    public OrePrefixMatch? Match(string oredict)
    {
        foreach (var length in _lengths)
        {
            if (length <= oredict.Length && _byName.TryGetValue(oredict[..length], out var prefix))
            {
                return new OrePrefixMatch(prefix, oredict[length..]);
            }
        }
        return null;
    }

    /// <summary>Whether the oredict names one shape of a single material, so converting it back is matter-neutral.</summary>
    public bool IsMaterialShape(string oredict) => Match(oredict) is { Prefix.IsShape: true };

    /// <summary>The prefix's material content, or 0 when GT leaves it undefined.</summary>
    public long AmountOf(string name) =>
        _byName.TryGetValue(name, out var prefix) && prefix.MaterialAmount > 0 ? prefix.MaterialAmount : 0;

    public bool IsShapeName(string name) => _byName.TryGetValue(name, out var prefix) && prefix.IsShape;
}
