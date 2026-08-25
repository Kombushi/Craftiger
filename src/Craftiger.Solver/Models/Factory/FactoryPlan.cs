namespace Craftiger.Solver.Models.Factory;

/// <summary>A solved steady-state plan: PricedInflowCost is the resource layer's value, DrawEuT the machine draw, ExportEuT the generators' net emission, BusyMachines the parallel-adjusted total.</summary>
public sealed record FactoryPlan(
    FactoryPlanStatus Status,
    IReadOnlyList<FactoryLine> Lines,
    IReadOnlyList<FactoryItemFlow> Flows,
    IReadOnlyList<FactoryInflow> Inflows,
    IReadOnlyList<FactoryWarning> Warnings,
    double PricedInflowCost,
    double DrawEuT,
    double ExportEuT,
    double BusyMachines)
{
    public static FactoryPlan Empty(FactoryPlanStatus status, IReadOnlyList<FactoryWarning> warnings) =>
        new(status, [], [], [], warnings, 0, 0, 0, 0);
}
