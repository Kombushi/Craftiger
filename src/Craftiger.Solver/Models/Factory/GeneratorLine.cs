using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>One garage-legal way to burn a fuel: a generator block, per-machine consumption and net output after the Enet loss; turbine lines also carry the rotor, its fit and the condensate returned, boosted lines their extra flows.</summary>
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
    double CondensatePerUnit = 0,
    string? Variant = null,
    IReadOnlyList<GeneratorFlow>? ExtraInputs = null,
    IReadOnlyList<GeneratorFlow>? ExtraOutputs = null)
{
    public IReadOnlyList<GeneratorFlow> Inputs => ExtraInputs ?? [];

    public IReadOnlyList<GeneratorFlow> Outputs => ExtraOutputs ?? [];

    public bool Satisfies(EnergyBand band) => Tier >= band.Tier;

    /// <summary>Input weight spent per net EU/t — the generator band's pruning measure; unpriced extras read as free rather than pruning the line.</summary>
    public double WeightPerEu(CostTable costs)
    {
        var perSecond = costs.Cost(FuelItem) * UnitsPerSecond;
        foreach (var flow in Inputs)
        {
            if (costs.TryCost(flow.Item, out var cost))
            {
                perSecond += cost * flow.PerSecond;
            }
        }
        return perSecond / NetEuT;
    }

    public double CondensatePerSecond => UnitsPerSecond * CondensatePerUnit;

    /// <summary>The synthetic line id; item ids contain '~', so '|' separates the parts.</summary>
    public string LineId(SolverIndex index)
    {
        var fuelId = index.ItemIds[FuelItem];
        var id = RotorItemId is { } rotor
            ? $"generator|{BlockItemId}|{fuelId}|{rotor}|{(Fit == RotorFit.Loose ? "loose" : "tight")}"
            : $"generator|{BlockItemId}|{fuelId}";
        return Variant is null ? id : $"{id}|{Variant}";
    }
}
