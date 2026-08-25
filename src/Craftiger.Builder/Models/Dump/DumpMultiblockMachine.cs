namespace Craftiger.Builder.Models.Dump;

/// <summary>A multiblock controller with its parsed bonuses; MaxParallel is null when only a live structure knows it.</summary>
public sealed record DumpMultiblockMachine(
    string ItemId, int? MaxParallel, IReadOnlyList<DumpMultiblockBonus> Bonuses);
