using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Services.Factory;

public sealed class FactoryTargetService : IFactoryTargetService
{
    /// <summary>Duplicates are summed; a non-positive rate is dropped; an unknown item fails the whole request.</summary>
    public FactoryTargets? Normalize(SolverIndex index, FactoryRequest request, ICollection<FactoryWarning> warnings)
    {
        var produce = new Dictionary<int, double>();
        var consume = new Dictionary<int, double>();
        var energy = 0.0;
        var bands = new Dictionary<int, double>();
        var failed = false;
        foreach (var target in request.Targets)
        {
            if (target.Kind == FactoryTargetKind.Energy)
            {
                if (target.Rate > 0)
                {
                    energy += target.Rate;
                    if (target.GeneratorTier is { } tier)
                    {
                        bands[tier] = bands.GetValueOrDefault(tier) + target.Rate;
                    }
                }
                continue;
            }
            if (target.ItemId is null || !index.TryGetItem(target.ItemId, out var item))
            {
                warnings.Add(FactoryWarning.TargetUnknown(target.ItemId ?? ""));
                failed = true;
                continue;
            }
            if (target.Rate <= 0)
            {
                continue;
            }
            var rates = target.Kind == FactoryTargetKind.Consume ? consume : produce;
            rates[item] = rates.GetValueOrDefault(item) + target.Rate;
        }
        return failed
            ? null
            : new FactoryTargets(
                produce, consume, energy,
                [.. bands.OrderBy(band => band.Key).Select(band => new EnergyBand(band.Key, band.Value))]);
    }
}
