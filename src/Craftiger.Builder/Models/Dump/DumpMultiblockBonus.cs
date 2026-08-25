namespace Craftiger.Builder.Models.Dump;

/// <summary>One typed bonus line of a multiblock; the value is the displayed number.</summary>
public sealed record DumpMultiblockBonus(
    string Kind, double Value, bool Multiplicative, string? TierAxis);
