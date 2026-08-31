using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Models;

/// <summary>A solved factory plan with its cache id and the display lookup for every item it names; per-line EU/t is EuTPerMachine times BusyMachines.</summary>
public sealed record FactoryResponse(
    string FactoryId,
    FactoryPlanStatus Status,
    IReadOnlyList<FactoryLine> Lines,
    IReadOnlyList<FactoryItemFlow> Flows,
    IReadOnlyList<FactoryInflow> Inflows,
    IReadOnlyList<FactoryWarning> Warnings,
    double PricedInflowCost,
    double DrawEuT,
    double ExportEuT,
    double BusyMachines,
    IReadOnlyDictionary<string, ItemRefDto> Items);
