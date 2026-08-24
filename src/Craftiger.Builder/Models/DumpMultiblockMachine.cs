namespace Craftiger.Builder.Models;

/// <summary>A multiblock controller with its parsed bonuses; MaxParallel is null when the
/// prototype's lookup needs a live structure.</summary>
public sealed record DumpMultiblockMachine(
    string ItemId, int? MaxParallel, IReadOnlyList<DumpMultiblockBonus> Bonuses);
