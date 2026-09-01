namespace Craftiger.Solver.Models.Factory;

/// <summary>One way to run a recipe: a block at an overclock with its effective duration, energy per run and parallels; steam variants draw SteamPerRun liters of SteamItem instead of EU, and OutputFactor scales every output where the overclock multiplies yield rather than speed.</summary>
public sealed record RunVariant(
    string? MachineItemId,
    int OcSteps,
    double Parallels,
    double DurationSeconds,
    double EuPerRun,
    bool Estimated,
    int? SteamItem = null,
    double SteamPerRun = 0,
    double OutputFactor = 1)
{
    /// <summary>A free instant converter: the anonymous variant of a recipe with no duration.</summary>
    public static RunVariant Durationless { get; } = new(null, 0, 1, 0, 0, Estimated: false);

    public bool IsDurationless => DurationSeconds == 0;

    public bool DrawsEu => EuPerRun > 0;

    public bool DrawsSteam => SteamItem is not null;

    public bool ScalesOutputs => !OutputFactor.Equals(1.0);

    /// <summary>Machine draw in EU/t at a run rate.</summary>
    public double DrawEuT(double runsPerSecond) => runsPerSecond * EuPerRun / Ticks.PerSecond;

    /// <summary>The continuous busy-machine count at a run rate.</summary>
    public double BusyMachines(double runsPerSecond) => runsPerSecond * DurationSeconds / Parallels;

    /// <summary>Energy per run with steam counted at its EU content, so the energy layer is carrier-neutral.</summary>
    public double EnergyPerRun(double euPerSteamLiter) => EuPerRun + SteamPerRun * euPerSteamLiter;
}
