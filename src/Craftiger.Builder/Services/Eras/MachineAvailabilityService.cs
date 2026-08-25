using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Eras;

/// <summary>A map served without machine blocks is available from the start; one whose blocks never craft stays null.</summary>
public sealed class MachineAvailabilityService(IOptions<ErasConfiguration> eras) : IMachineAvailabilityService
{
    private readonly ErasConfiguration _eras = eras.Value;

    public IReadOnlyDictionary<string, int?> Run(IReadOnlyList<PlannerRecipe> recipes, EraTable table)
    {
        var availability = new Dictionary<string, int?>();
        foreach (var recipe in recipes)
        {
            var cheapest = availability.GetValueOrDefault(recipe.Machine);
            if (recipe.Machines.Count == 0)
            {
                cheapest = 0;
            }
            foreach (var machine in recipe.Machines)
            {
                if (table.TryGetEra(machine.ItemId, out var machineEra)
                    && (cheapest is null || machineEra < cheapest))
                {
                    cheapest = machineEra;
                }
            }
            availability[recipe.Machine] = cheapest;
        }
        foreach (var (machine, floor) in _eras.MachineEraFloors)
        {
            if (availability.TryGetValue(machine, out var value) && value is { } known)
            {
                availability[machine] = Math.Max(known, floor);
            }
        }
        return availability;
    }
}
