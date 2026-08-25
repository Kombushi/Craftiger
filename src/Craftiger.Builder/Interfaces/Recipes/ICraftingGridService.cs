using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces.Recipes;

/// <summary>Rebuilds a shaped recipe's grid over its final slots, or none when netting removed a cell's ingredient.</summary>
public interface ICraftingGridService
{
    IReadOnlyList<PlannerGridCell>? GridOf(
        IReadOnlyList<GridCellRef> cellRefs, IReadOnlyDictionary<string, long> inputs, int choiceCount);
}
