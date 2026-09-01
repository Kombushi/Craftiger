using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Eras;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services.Eras;

public sealed class EraPropagationService(
    IOptions<ErasConfiguration> eras,
    ILogger<EraPropagationService> logger) : IEraPropagationService
{
    private readonly ErasConfiguration _eras = eras.Value;

    public void Run(IReadOnlyList<PlannerRecipe> recipes, EraTable table, UnifiedItems unified, Dump dump, CoilLadder coils)
    {
        var cleanroomIds = dump.Items.Values
            .Where(i => i.Name == _eras.CleanroomItemName)
            .Select(i => unified.Canonical(i.Id))
            .ToHashSet();
        var consumers = BuildConsumers(recipes, cleanroomIds);
        var machineVoltage = MachineVoltages(unified, dump);

        var queue = new Queue<PlannerRecipe>(recipes);
        var queued = new HashSet<string>(recipes.Select(r => r.Id));
        while (queue.TryDequeue(out var recipe))
        {
            queued.Remove(recipe.Id);

            var candidate = RecipeEra(recipe, table, cleanroomIds, machineVoltage, coils);
            if (candidate == int.MaxValue)
            {
                continue;
            }

            foreach (var output in recipe.Outputs)
            {
                var floored = cleanroomIds.Contains(output.ItemId)
                    ? Math.Max(candidate, _eras.CleanroomMinEra)
                    : candidate;
                if (!table.Reach(output.ItemId, floored, recipe))
                {
                    continue;
                }
                foreach (var consumer in consumers.GetValueOrDefault(output.ItemId) ?? [])
                {
                    if (queued.Add(consumer.Id))
                    {
                        queue.Enqueue(consumer);
                    }
                }
            }
        }

        logger.LogInformation("  {Reachable:N0} items reachable", table.Count);
    }

    /// <summary>The era a recipe can first run at, or int.MaxValue while an input, machine or the cleanroom is unreachable.</summary>
    private int RecipeEra(
        PlannerRecipe recipe,
        EraTable table,
        HashSet<string> cleanroomIds,
        Dictionary<string, int> machineVoltage,
        CoilLadder coils)
    {
        var candidate = MachineEra(recipe, table, machineVoltage, coils);
        if (candidate == int.MaxValue)
        {
            return int.MaxValue;
        }

        if (recipe.RequiresCleanroom)
        {
            var cleanroomEra = table.CheapestEra(cleanroomIds);
            if (cleanroomEra == int.MaxValue)
            {
                return int.MaxValue;
            }
            candidate = Math.Max(candidate, cleanroomEra);
        }

        foreach (var slot in recipe.InputSlotAlternatives)
        {
            var slotEra = table.CheapestEra(slot);
            if (slotEra == int.MaxValue)
            {
                return int.MaxValue;
            }
            candidate = Math.Max(candidate, slotEra);
        }

        if (_eras.MachineEraFloors.TryGetValue(recipe.Machine, out var floor))
        {
            candidate = Math.Max(candidate, floor);
        }

        return candidate;
    }

    /// <summary>The cheapest producible machine gates the recipe, each floored at its own input voltage.</summary>
    private static int MachineEra(PlannerRecipe recipe, EraTable table, Dictionary<string, int> machineVoltage, CoilLadder coils)
    {
        if (recipe.Machines.Count == 0)
        {
            return coils.Floor(recipe.Tier, recipe.Heat);
        }
        var best = int.MaxValue;
        foreach (var machine in recipe.Machines)
        {
            if (!table.TryGetEra(machine.ItemId, out var machineEra))
            {
                continue;
            }
            // A steam machine burns fuel, so its own voltage tier is no floor at all.
            var voltageFloor = recipe.RunsOnSteam(machine)
                ? 0
                : machine.Tier ?? machineVoltage.GetValueOrDefault(machine.ItemId, 0);
            var on = Math.Max(
                Math.Max(machineEra, voltageFloor),
                coils.Floor(recipe.VoltageTierOn(machine), recipe.Heat));
            if (on < best)
            {
                best = on;
            }
        }
        return best;
    }

    /// <summary>Indexes recipes by every item that can hold them back: inputs, handler machines, and the cleanroom.</summary>
    private static Dictionary<string, List<PlannerRecipe>> BuildConsumers(
        IReadOnlyList<PlannerRecipe> recipes, HashSet<string> cleanroomIds)
    {
        var consumers = new Dictionary<string, List<PlannerRecipe>>();
        foreach (var recipe in recipes)
        {
            foreach (var slot in recipe.InputSlotAlternatives)
            {
                foreach (var alternative in slot)
                {
                    Add(consumers, alternative, recipe);
                }
            }
            foreach (var machine in recipe.Machines)
            {
                Add(consumers, machine.ItemId, recipe);
            }
            if (recipe.RequiresCleanroom)
            {
                foreach (var cleanroomId in cleanroomIds)
                {
                    Add(consumers, cleanroomId, recipe);
                }
            }
        }
        return consumers;

        static void Add(Dictionary<string, List<PlannerRecipe>> consumers, string id, PlannerRecipe recipe)
        {
            if (!consumers.TryGetValue(id, out var list))
            {
                consumers[id] = list = [];
            }
            list.Add(recipe);
        }
    }

    /// <summary>A machine buildable early still waits for its input-voltage tier to be powerable.</summary>
    private static Dictionary<string, int> MachineVoltages(UnifiedItems unified, Dump dump)
    {
        var machineVoltage = new Dictionary<string, int>();
        foreach (var (rawId, voltageTier) in dump.MachineVoltageTiers)
        {
            var machineId = unified.Canonical(rawId);
            if (!machineVoltage.TryGetValue(machineId, out var current) || voltageTier < current)
            {
                machineVoltage[machineId] = voltageTier;
            }
        }
        return machineVoltage;
    }
}
