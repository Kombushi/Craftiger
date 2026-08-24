namespace Craftiger.Solver.Models;

/// <summary>One machine block serving a map: <paramref name="Tier"/> is the single-block
/// voltage tier, null on multiblocks, whose voltage comes from the garage instead;
/// <paramref name="Era"/> gates craftability. A block without extracted bonus data runs at one
/// parallel with no modifiers — a flagged, conservative overestimate.</summary>
public sealed record FactoryMachineBlock(
    string ItemId,
    int? Tier,
    bool Multiblock,
    bool Steam,
    int? Era,
    long MaxParallel,
    IReadOnlyList<FactoryMachineBonus> Bonuses);
