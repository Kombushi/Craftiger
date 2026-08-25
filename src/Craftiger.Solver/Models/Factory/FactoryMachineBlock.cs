using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Factory;

/// <summary>One machine block serving a map: Tier is the single-block voltage tier (null on multiblocks), Era gates craftability, generator fields are null on everything else, RotorFuel names the rotor stat class a turbine controller spins.</summary>
public sealed record FactoryMachineBlock(
    string ItemId,
    int? Tier,
    bool Multiblock,
    bool Steam,
    int? Era,
    long MaxParallel,
    IReadOnlyList<FactoryMachineBonus> Bonuses,
    double? GeneratorEfficiency = null,
    long? GeneratorEuT = null,
    long? GeneratorAmps = null,
    string? RotorFuel = null)
{
    /// <summary>Large turbines accept one hatch of at most four amps; the XL turbos take multi-amp hatches.</summary>
    private const int MaxSingleHatchAmps = 4;

    public bool IsBuildable(Garage garage) => garage.Reaches(Era);

    public bool IsRotorTurbine => RotorFuel is not null;

    public long Amps => GeneratorAmps ?? 1;

    public double BaseParallels => Math.Max(1, MaxParallel);

    /// <summary>Whether the block folds several machines' throughput and so takes multi-amp hatches.</summary>
    public bool MultiAmp => BaseParallels > 1;

    /// <summary>EU/t the block loses to the Enet on the amps it emits at its own tier.</summary>
    public double EnetLoss => VoltageTier.EnetLossPerAmp(Tier ?? 0) * Amps;

    public bool AcceptsHatch(FactoryDynamo hatch) => MultiAmp || hatch.Amps <= MaxSingleHatchAmps;

    public BlockEffects Effects(int coilTier, int voltageTier) => BlockEffects.Resolve(this, coilTier, voltageTier);
}
