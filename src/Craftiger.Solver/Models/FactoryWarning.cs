namespace Craftiger.Solver.Models;

/// <summary>A structured factory warning the UI renders: <paramref name="Kind"/> is one of
/// target_unknown, target_unsupported, unreachable_target, pin_unknown, pin_illegal,
/// pin_conflict, routes_pruned, no_generator, consume_shortfall, infeasible_item,
/// infeasible_energy, infeasible, free_lunch, timeout, solver_error.</summary>
public sealed record FactoryWarning(string Kind, string ItemId);
