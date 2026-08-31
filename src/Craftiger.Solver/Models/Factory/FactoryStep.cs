namespace Craftiger.Solver.Models.Factory;

/// <summary>One pipeline step: a recipe or generator line chosen by hand, optionally pinned to one machine block and overclock level.</summary>
public sealed record FactoryStep(string Id, string? MachineItemId = null, int? OcSteps = null)
{
    public bool PinsVariant => MachineItemId is not null || OcSteps is not null;

    /// <summary>Whether a run variant satisfies the step's pin.</summary>
    public bool Admits(RunVariant variant) =>
        (MachineItemId is null || variant.MachineItemId == MachineItemId)
        && (OcSteps is null || variant.OcSteps == OcSteps);
}
