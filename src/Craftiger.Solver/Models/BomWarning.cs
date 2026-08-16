namespace Craftiger.Solver.Models;

/// <summary>A structured BOM warning the UI renders: <paramref name="Kind"/> is one of
/// pin_unknown, pin_illegal, pin_cycle, unreachable_target, unreachable_input.</summary>
public sealed record BomWarning(string Kind, string ItemId);
