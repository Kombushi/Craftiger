using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Factory;

/// <summary>A dynamo hatch: the capacity ceiling of a large turbine line — output beyond voltage times amps is voided, never stored.</summary>
public sealed record FactoryDynamo(string ItemId, int? Era, long EuT, long Amps)
{
    public double Capacity => (double)EuT * Amps;

    public int Tier => VoltageTier.Of(EuT);

    public bool IsBuildable(Garage garage) => garage.Reaches(Era);

    /// <summary>What the hatch emits from a raw output: capped to its capacity, less the Enet loss on the amps used.</summary>
    public HatchChoice Emit(double rawEuT)
    {
        var capped = Math.Min(rawEuT, Capacity);
        return new HatchChoice(capped - VoltageTier.EnetLossPerAmp(Tier) * (capped / EuT), Tier);
    }
}
