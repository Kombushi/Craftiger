namespace Craftiger.Solver.Models;

/// <summary>A solved steady-state plan. <paramref name="PricedInflowCost"/> is the resource
/// layer's value, <paramref name="DrawEuT"/> the total machine draw in EU/t, and
/// <paramref name="BusyMachines"/> the parallel-adjusted busy-machine total.</summary>
public sealed record FactoryPlan(
    FactoryPlanStatus Status,
    IReadOnlyList<FactoryLine> Lines,
    IReadOnlyList<FactoryItemFlow> Flows,
    IReadOnlyList<FactoryInflow> Inflows,
    IReadOnlyList<FactoryWarning> Warnings,
    double PricedInflowCost,
    double DrawEuT,
    double BusyMachines);
