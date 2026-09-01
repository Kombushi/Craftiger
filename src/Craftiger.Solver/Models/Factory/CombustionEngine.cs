namespace Craftiger.Solver.Models.Factory;

/// <summary>A boostable combustion engine multiblock: nominal output at integer-division fuel draw, tripled by a booster gas for doubled draw plus a weighted top-up on over-rich fuels.</summary>
public sealed record CombustionEngine(long NominalEuT, GeneratorMode Booster, GeneratorMode Lubricant)
{
    /// <summary>Boosting consumes fuel worth twice the nominal output.</summary>
    private const int BoostFuelFactor = 2;

    public static CombustionEngine? Of(FactoryMachineBlock block, IReadOnlyList<GeneratorMode> modes)
    {
        if (!block.Multiblock || block.GeneratorEuT is not { } nominal)
        {
            return null;
        }
        var booster = modes.FirstOrDefault(mode => mode.Kind == GeneratorModeKind.Booster);
        var lubricant = modes.FirstOrDefault(mode => mode.Kind == GeneratorModeKind.Lubricant);
        return booster is null || lubricant is null ? null : new CombustionEngine(nominal, booster, lubricant);
    }

    /// <summary>The unboosted burn; fuel richer than the nominal output refuses to run.</summary>
    public EngineBurn? Base(double euPerUnit)
    {
        var fuelValue = (long)euPerUnit;
        if (fuelValue <= 0 || fuelValue > NominalEuT)
        {
            return null;
        }
        var fuelPerTick = NominalEuT / fuelValue;
        return new EngineBurn(
            fuelPerTick * Ticks.PerSecond, NominalEuT, Lubricant.PerSecond, 0);
    }

    /// <summary>The boosted burn: output times the boost factor, integer-division draw plus the expected weighted top-up, doubled lubricant.</summary>
    public EngineBurn? Boosted(double euPerUnit)
    {
        var fuelValue = (long)euPerUnit;
        if (fuelValue <= 0)
        {
            return null;
        }
        var boostedDraw = BoostFuelFactor * NominalEuT / fuelValue;
        double perTick = boostedDraw;
        var boostedFuelValue = (long)(fuelValue * 1.5);
        var boostedOutput = NominalEuT * Booster.Factor;
        if (boostedFuelValue * 2 > boostedOutput)
        {
            var fraction = boostedOutput / boostedFuelValue;
            perTick += fraction - (long)fraction;
        }
        return new EngineBurn(
            perTick * Ticks.PerSecond, boostedOutput, Lubricant.PerSecond * 2, Booster.PerSecond);
    }
}
