using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>One garage-legal way to burn a fuel: a generator block, per-machine consumption and net output after the Enet loss; turbine lines also carry the rotor, its fit and the condensate returned.</summary>
public sealed record GeneratorLine(
    string Map,
    string BlockItemId,
    int Tier,
    int FuelItem,
    double UnitsPerSecond,
    double NetEuT,
    string? RotorItemId = null,
    RotorFit Fit = RotorFit.Tight,
    int? CondensateItem = null,
    double CondensatePerUnit = 0)
{
    public bool Satisfies(EnergyBand band) => Tier >= band.Tier;

    /// <summary>Fuel weight spent per net EU/t — the generator band's pruning measure.</summary>
    public double WeightPerEu(CostTable costs) => costs.Cost(FuelItem) * UnitsPerSecond / NetEuT;

    public double CondensatePerSecond => UnitsPerSecond * CondensatePerUnit;

    /// <summary>The synthetic line id; item ids contain '~', so '|' separates the parts.</summary>
    public string LineId(SolverIndex index)
    {
        var fuelId = index.ItemIds[FuelItem];
        return RotorItemId is { } rotor
            ? $"generator|{BlockItemId}|{fuelId}|{rotor}|{(Fit == RotorFit.Loose ? "loose" : "tight")}"
            : $"generator|{BlockItemId}|{fuelId}";
    }
}
