namespace Craftiger.Builder.Models.Planner;

/// <summary>One filled cell of a shaped crafting grid, row-major 0–8, pointing at the input slot that sits there.</summary>
public sealed record PlannerGridCell(int Cell, int Slot);
