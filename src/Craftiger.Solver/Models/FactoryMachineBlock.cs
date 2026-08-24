namespace Craftiger.Solver.Models;

/// <summary>One machine block serving a map: <paramref name="Tier"/> is the single-block
/// voltage tier, null on multiblocks, whose voltage comes from the garage instead;
/// <paramref name="Era"/> gates craftability. A block without extracted bonus data runs at one
/// parallel with no modifiers — a flagged, conservative overestimate. Generator blocks carry
/// their fuel efficiency and per-amp output; null on everything else.
/// <paramref name="RotorTurbine"/> marks controllers whose output comes from an installed
/// rotor's stat table.</summary>
public sealed record FactoryMachineBlock(
    string ItemId,
    int? Tier,
    bool Multiblock,
    bool Steam,
    int? Era,
    long MaxParallel,
    IReadOnlyList<FactoryMachineBonus> Bonuses,
    double? GeneratorEfficiency = null,
    long? GeneratorEuT = null,
    long? GeneratorAmps = null,
    bool RotorTurbine = false);
