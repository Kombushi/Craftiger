using System.Collections.Frozen;
using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Services.Costs;

public sealed class GarageLegalityService(IOptions<GarageRules> options) : IGarageLegalityService
{
    /// <summary>Heat granted per energy hatch tier above MV on maps with the bonus, verified against GT5-Unofficial source.</summary>
    private const int HeatPerTierAboveMv = 100;

    private readonly FrozenSet<string> _alwaysOwned = options.Value.AlwaysOwnedMachines.ToFrozenSet();
    private readonly FrozenSet<string> _heatExempt = options.Value.HeatExemptMachines.ToFrozenSet();
    private readonly FrozenSet<string> _heatBonus = options.Value.HeatBonusMachines.ToFrozenSet();

    public bool IsAlwaysOwned(string machine) => _alwaysOwned.Contains(machine);

    public int? EffectiveTier(string machine, Garage garage)
    {
        var overridden = garage.TryGetOverride(machine, out var tier);
        if (IsAlwaysOwned(machine))
        {
            return overridden && tier is { } owned ? owned : garage.DefaultTier;
        }
        return overridden ? tier : garage.DefaultTier;
    }

    public bool IsLegal(SolverIndex index, int recipe, Garage garage)
    {
        var machine = index.Machine[recipe];
        if (EffectiveTier(machine, garage) is not { } tier)
        {
            return false;
        }

        var required = index.MultiTierOf(recipe) is { } multi && garage.HasBuilt(machine) ? multi : index.Tier[recipe];
        if (required > tier)
        {
            return false;
        }

        return index.HeatOf(recipe) is not { } heat
            || _heatExempt.Contains(machine)
            || heat <= HeatCapacity(machine, garage);
    }

    public int HeatCapacity(string machine, Garage garage)
    {
        var capacity = garage.CoilHeatOf(machine);
        if (_heatBonus.Contains(machine) && EffectiveTier(machine, garage) is { } tier)
        {
            capacity += HeatPerTierAboveMv * Math.Max(0, tier - 2);
        }
        return capacity;
    }
}
