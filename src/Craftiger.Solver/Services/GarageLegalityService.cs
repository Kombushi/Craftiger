using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;

namespace Craftiger.Solver.Services;

public sealed class GarageLegalityService(GarageRules rules) : IGarageLegalityService
{
    /// <summary>Heat capacity granted per energy hatch tier above MV on maps that have the
    /// bonus (the Electric Blast Furnace; verified against GT5-Unofficial source).</summary>
    private const int HeatPerTierAboveMv = 100;

    public int? EffectiveTier(string machine, Garage garage)
    {
        var overridden = garage.MachineTiers.TryGetValue(machine, out var tier);
        if (rules.AlwaysOwnedMachines.Contains(machine))
        {
            // Always-owned machines cannot be disowned, so a None override falls back.
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

        var multi = index.MultiTier[recipe];
        var required = multi >= 0 && garage.BuiltMultiblocks.Contains(machine) ? multi : index.Tier[recipe];
        if (required > tier)
        {
            return false;
        }

        var heat = index.Heat[recipe];
        if (heat >= 0 && !rules.HeatExemptMachines.Contains(machine))
        {
            var capacity = garage.CoilHeat.GetValueOrDefault(machine)
                + (rules.HeatBonusMachines.Contains(machine)
                    ? HeatPerTierAboveMv * Math.Max(0, tier - 2)
                    : 0);
            if (heat > capacity)
            {
                return false;
            }
        }
        return true;
    }
}
