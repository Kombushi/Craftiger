namespace Craftiger.Solver.Models.Factory;

/// <summary>Normalized targets by item position, duplicates summed: produce rates, consume rates, the net EU/t export, and its per-tier bands.</summary>
public sealed record FactoryTargets(
    IReadOnlyDictionary<int, double> Produce,
    IReadOnlyDictionary<int, double> Consume,
    double EnergyEuT,
    IReadOnlyList<EnergyBand> Bands)
{
    public bool HasEnergy => EnergyEuT > 0;

    public bool HasConsume => Consume.Count > 0;

    public double ProduceRate(int item) => Produce.GetValueOrDefault(item);

    /// <summary>Produce targets in position order — the order their balance rows take.</summary>
    public IEnumerable<int> ProducedItems => Produce.Keys.Order();

    public IEnumerable<int> ConsumedItems => Consume.Keys.Order();
}
