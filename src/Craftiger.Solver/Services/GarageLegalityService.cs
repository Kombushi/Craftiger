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

    public bool IsLegal(SolverRecipe recipe, Garage garage)
    {
        if (EffectiveTier(recipe.Machine, garage) is not { } tier)
        {
            return false;
        }

        var required = recipe.MultiTier is { } multi && garage.BuiltMultiblocks.Contains(recipe.Machine)
            ? multi
            : recipe.Tier;
        if (required > tier)
        {
            return false;
        }

        if (recipe.Heat is { } heat && !rules.HeatExemptMachines.Contains(recipe.Machine))
        {
            var capacity = garage.CoilHeat.GetValueOrDefault(recipe.Machine)
                + (rules.HeatBonusMachines.Contains(recipe.Machine)
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
