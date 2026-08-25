namespace Craftiger.Solver.Models.Factory;

/// <summary>One running line: a recipe on a chosen block at a chosen overclock; MachineItemId is null on the anonymous fallback block, Estimated marks lines run on assumptions.</summary>
public sealed record FactoryLine(
    string RecipeId,
    string Machine,
    string? MachineItemId,
    double RunsPerSecond,
    int OcSteps,
    double Parallels,
    double BusyMachines,
    bool Durationless,
    bool Estimated);
