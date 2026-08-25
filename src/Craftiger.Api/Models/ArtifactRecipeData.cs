using System.Collections.Immutable;

namespace Craftiger.Api.Models;

/// <summary>Display data per recipe position beyond the solver index: run time and power, the catalyst slots as compressed rows of id and amount, and a shaped recipe's grid cells.</summary>
public sealed record ArtifactRecipeData(
    ImmutableArray<long> DurationTicks,
    ImmutableArray<long> EuT,
    ImmutableArray<int> CatalystSlotStart,
    ImmutableArray<int> CatalystAlternativeStart,
    ImmutableArray<string> CatalystItemId,
    ImmutableArray<long> CatalystAmount,
    ImmutableArray<int> GridStart,
    ImmutableArray<byte> GridCell,
    ImmutableArray<int> GridSlot)
{
    /// <summary>Cells of the crafting grid, row-major.</summary>
    public const int GridCells = 9;

    public int CatalystSlotCount(int recipe) => CatalystSlotStart[recipe + 1] - CatalystSlotStart[recipe];

    public int CatalystAlternativeCount(int recipe, int slot)
    {
        var s = CatalystSlotStart[recipe] + slot;
        return CatalystAlternativeStart[s + 1] - CatalystAlternativeStart[s];
    }

    /// <summary>The flat position of one catalyst alternative, indexing CatalystItemId and CatalystAmount.</summary>
    public int CatalystAt(int recipe, int slot, int alternative) =>
        CatalystAlternativeStart[CatalystSlotStart[recipe] + slot] + alternative;

    /// <summary>The recipe's shape as nine cells, each the input slot it holds or null; null when the recipe ships no shape.</summary>
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
