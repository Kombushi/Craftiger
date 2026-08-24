namespace Craftiger.Solver.Models;

/// <summary>One running line of a plan: a recipe on a chosen machine block at a chosen
/// overclock. <paramref name="MachineItemId"/> is null on the anonymous fallback block;
/// <paramref name="BusyMachines"/> is the continuous count <c>runs/s × duration ÷ parallels</c>
/// the UI ceils; <paramref name="Estimated"/> marks lines run on assumptions — a block
/// without extracted bonus data, or a bonus axis the garage cannot resolve.</summary>
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
