using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

public sealed class UndergroundFluidRecipeService(
    IOptions<SynthesizedMachinesConfiguration> options,
    ILogger<UndergroundFluidRecipeService> logger)
    : IUndergroundFluidRecipeService
{
    private readonly SynthesizedMachinesConfiguration _config = options.Value;

    public List<PlannerRecipe> Run(Dump dump, UnifiedItems unified, WorldgenEras worldgen)
    {
        var rigs = _config.PumpMachineItemNames
            .SelectMany(dump.ItemIdsNamed)
            .Select(unified.Canonical)
            .Distinct()
            .Select(id => new RecipeMachine(id, Multiblock: false, Tier: null, Steam: false))
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
                $"gtuf~{fluidId}", _config.PumpMachine, dimensionEra, Heat: null, DurationTicks: 0, EuT: 0,
                Inputs: [],
                Choices: [],
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
