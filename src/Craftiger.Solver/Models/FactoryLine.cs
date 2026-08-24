namespace Craftiger.Solver.Models;

/// <summary>One running recipe line of a plan. <paramref name="BusyMachines"/> is the
/// continuous machine count <c>runs/s × duration</c>; the UI ceils it per line.
/// <paramref name="Durationless"/> flags the free instant converters.</summary>
public sealed record FactoryLine(
    string RecipeId,
    string Machine,
    double RunsPerSecond,
    double BusyMachines,
    bool Durationless);
