namespace Craftiger.Api.Models;

/// <summary>Display data per recipe position beyond what the solver index holds: run time and
/// power, and the catalyst slots — the tool, mold and circuit slots a recipe needs in place
/// but never consumes, display only, never read by the solver — as compressed rows of item
/// ids and amounts.</summary>
public sealed class ArtifactRecipeData(
    long[] durationTicks, long[] euT,
    int[] catalystSlotStart, int[] catalystAlternativeStart, string[] catalystItemId, long[] catalystAmount)
{
    public long[] DurationTicks { get; } = durationTicks;

    public long[] EuT { get; } = euT;

    /// <summary>Recipe <c>r</c> owns catalyst slots <c>CatalystSlotStart[r]</c> to <c>CatalystSlotStart[r + 1]</c>.</summary>
    public int[] CatalystSlotStart { get; } = catalystSlotStart;

    /// <summary>Catalyst slot <c>s</c> owns alternatives <c>CatalystAlternativeStart[s]</c> to <c>CatalystAlternativeStart[s + 1]</c>.</summary>
    public int[] CatalystAlternativeStart { get; } = catalystAlternativeStart;

    /// <summary>By id rather than index position: a tool may never appear as a priced input.</summary>
    public string[] CatalystItemId { get; } = catalystItemId;

    public long[] CatalystAmount { get; } = catalystAmount;

    public int CatalystSlotCount(int recipe) => CatalystSlotStart[recipe + 1] - CatalystSlotStart[recipe];

    public int CatalystAlternativeCount(int recipe, int slot)
    {
        var s = CatalystSlotStart[recipe] + slot;
        return CatalystAlternativeStart[s + 1] - CatalystAlternativeStart[s];
    }

    /// <summary>The flat position of one catalyst alternative, indexing <see cref="CatalystItemId"/>
    /// and <see cref="CatalystAmount"/>.</summary>
    public int CatalystAt(int recipe, int slot, int alternative) =>
        CatalystAlternativeStart[CatalystSlotStart[recipe] + slot] + alternative;
}
