using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Services;

public sealed class UndergroundFluidRecipeService(
    BuilderConfig config, ILogger<UndergroundFluidRecipeService> logger) : IUndergroundFluidRecipeService
{
    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified, WorldgenEras worldgen)
    {
        var rigs = config.PumpMachineItemNames
            .SelectMany(dump.ItemIdsNamed)
            .Select(unified.Canonical)
            .Distinct()
            .Select(id => new RecipeMachine(id, Multiblock: false, Tier: null))
            .ToList();

        var recipes = new List<PlannerRecipe>();
        foreach (var (fluidId, dimensionEra) in worldgen.Fluids)
        {
            if (!dump.Fluids.ContainsKey(fluidId))
            {
                continue;
            }

            // Reaching the dimension is not enough; something has to pump the fluid out.
            recipes.Add(new PlannerRecipe(
                $"gtuf~{fluidId}", config.PumpMachine, dimensionEra, Heat: null, DurationTicks: 0, EuT: 0,
                Inputs: [],
                Outputs: [new PlannerOutput(fluidId, 1, 1.0)],
                Machines: rigs,
                InputSlotAlternatives: [],
                RequiresCleanroom: false,
                EraOnly: true));
        }

        logger.LogInformation("  {Count:N0} pumpable fluids, {Rigs} rigs", recipes.Count, rigs.Count);
        return recipes;
    }
}
