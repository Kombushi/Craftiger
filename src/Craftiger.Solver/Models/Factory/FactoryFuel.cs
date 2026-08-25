namespace Craftiger.Solver.Models.Factory;

/// <summary>One burnable fuel on a generator map: standard fuels carry EU per unit before efficiency, timed fuels a fixed EU/t over a lifetime per Amount consumed.</summary>
public sealed record FactoryFuel(
    string Map,
    string ItemId,
    long Amount,
    double? EuPerUnit,
    double? EuT,
    long? DurationTicks)
{
    public bool IsTimed => EuT is not null && DurationTicks is > 0;

    /// <summary>How a single-block generator burns this fuel: timed fuels at their fixed EU/t over the lifetime, standard fuels at the block's full output; null when the pair cannot burn.</summary>
    public FuelBurn? Burn(FactoryMachineBlock block)
    {
        if (block.GeneratorEuT is not { } outEuT)
        {
            return null;
        }
        if (EuT is { } fixedEuT && DurationTicks is { } lifetime && lifetime > 0)
        {
            return new FuelBurn(Amount * Ticks.PerSecond / lifetime, fixedEuT);
        }
        if (EuPerUnit is { } euPerUnit && euPerUnit > 0)
        {
            var effective = euPerUnit * (block.GeneratorEfficiency ?? 100) / 100;
            var rawEuT = outEuT * (double)block.Amps;
            return new FuelBurn(rawEuT * Ticks.PerSecond / effective, rawEuT);
        }
        return null;
    }
}
