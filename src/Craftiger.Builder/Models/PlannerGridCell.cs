namespace Craftiger.Builder.Models;

/// <summary>One filled cell of a shaped crafting grid, row-major 0–8, pointing at the recipe's
/// input slot that holds what sits there — an ingredient, a choice or a catalyst.</summary>
public sealed record PlannerGridCell(int Cell, int Slot);
