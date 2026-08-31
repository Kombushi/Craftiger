namespace Craftiger.Solver.Models.Factory;

/// <summary>One running line: a recipe on a chosen block at a chosen overclock; MachineItemId is null on the anonymous fallback block, Estimated marks lines run on assumptions, DurationSeconds is one run after overclocking, EuTPerMachine is one busy instance's draw — negative for a generator's net emission — and Inputs/Outputs are the line's item streams.</summary>
public sealed record FactoryLine(
    string RecipeId,
    string Machine,
    string? MachineItemId,
    double RunsPerSecond,
    int OcSteps,
    double Parallels,
    double BusyMachines,
    bool Durationless,
    bool Estimated,
    double DurationSeconds = 0,
    double EuTPerMachine = 0,
    IReadOnlyList<FactoryLineFlow>? Inputs = null,
    IReadOnlyList<FactoryLineFlow>? Outputs = null)
{
    /// <summary>The whole line's draw (or emission, negative) in EU/t.</summary>
    public double LineEuT => EuTPerMachine * BusyMachines;
}
