namespace Craftiger.Api.Models;

/// <summary>Display data per recipe position beyond what the solver index holds: run time and
/// power, the catalyst slots — the tool, mold and circuit slots a recipe needs in place
/// but never consumes, display only, never read by the solver — as compressed rows of item
/// ids and amounts, and the shape of a shaped crafting recipe as compressed rows of grid cell
/// and input slot.</summary>
public sealed class ArtifactRecipeData(
    long[] durationTicks, long[] euT,
    int[] catalystSlotStart, int[] catalystAlternativeStart, string[] catalystItemId, long[] catalystAmount,
    int[] gridStart, byte[] gridCell, int[] gridSlot)
{
    /// <summary>Cells of the crafting grid, row-major.</summary>
    public const int GridCells = 9;

    public long[] DurationTicks { get; } = durationTicks;

    public long[] EuT { get; } = euT;

    /// <summary>Recipe <c>r</c> owns catalyst slots <c>CatalystSlotStart[r]</c> to <c>CatalystSlotStart[r + 1]</c>.</summary>
    public int[] CatalystSlotStart { get; } = catalystSlotStart;

    /// <summary>Catalyst slot <c>s</c> owns alternatives <c>CatalystAlternativeStart[s]</c> to <c>CatalystAlternativeStart[s + 1]</c>.</summary>
    public int[] CatalystAlternativeStart { get; } = catalystAlternativeStart;

    /// <summary>By id rather than index position: a tool may never appear as a priced input.</summary>
    public string[] CatalystItemId { get; } = catalystItemId;

    public long[] CatalystAmount { get; } = catalystAmount;

    /// <summary>Recipe <c>r</c> owns grid rows <c>GridStart[r]</c> to <c>GridStart[r + 1]</c>; a
    /// recipe with no rows has no shape.</summary>
    public int[] GridStart { get; } = gridStart;

    public byte[] GridCell { get; } = gridCell;

    /// <summary>The input slot a filled cell holds, numbered over ingredient slots, choice slots
    /// and catalyst slots in that order — the solver's slots first, then the catalysts.</summary>
    public int[] GridSlot { get; } = gridSlot;

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

    /// <summary>The recipe's shape as nine cells, each the input slot it holds or null for an
    /// empty cell; null when the recipe ships no shape.</summary>
    public IReadOnlyList<int?>? Grid(int recipe)
    {
        if (GridStart[recipe + 1] == GridStart[recipe])
        {
            return null;
        }
        var cells = new int?[GridCells];
        for (var g = GridStart[recipe]; g < GridStart[recipe + 1]; g++)
        {
            cells[GridCell[g]] = GridSlot[g];
        }
        return cells;
    }
}
