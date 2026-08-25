using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services.Recipes;

/// <summary>Flat ingredients take the slot numbers of their input position, choices and catalysts follow in order.</summary>
public sealed class CraftingGridService : ICraftingGridService
{
    private const int GridCells = 9;

    public IReadOnlyList<PlannerGridCell>? GridOf(
        IReadOnlyList<GridCellRef> cellRefs, IReadOnlyDictionary<string, long> inputs, int choiceCount)
    {
        var slotOfInput = new Dictionary<string, int>();
        foreach (var key in inputs.Keys)
        {
            slotOfInput[key] = slotOfInput.Count;
        }
        var cells = new List<PlannerGridCell>(cellRefs.Count);
        foreach (var cellRef in cellRefs)
        {
            if (cellRef.Cell < 0 || cellRef.Cell >= GridCells)
            {
                continue;
            }
            int slot;
            if (cellRef.Item is not null)
            {
                // A cell whose ingredient netting removed has no slot, and the recipe then ships no shape.
                if (!slotOfInput.TryGetValue(cellRef.Item, out slot))
                {
                    return null;
                }
            }
            else if (cellRef.Choice is { } choice)
            {
                slot = inputs.Count + choice;
            }
            else
            {
                slot = inputs.Count + choiceCount + cellRef.Catalyst!.Value;
            }
            cells.Add(new PlannerGridCell(cellRef.Cell, slot));
        }
        return cells;
    }
}
