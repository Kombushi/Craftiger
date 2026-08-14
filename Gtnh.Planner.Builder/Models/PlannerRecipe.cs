namespace Gtnh.Planner.Builder.Models;

public sealed record PlannerRecipe(
    string Id,
    string Machine,
    int Tier,
    int? Heat,
    long DurationTicks,
    long EuT,
    Dictionary<string, long> Inputs,
    List<PlannerOutput> Outputs,
    IReadOnlyList<string> MachineItemIds,
    IReadOnlyList<IReadOnlyList<string>> InputSlotAlternatives,
    bool RequiresCleanroom,
    bool EraOnly = false);
