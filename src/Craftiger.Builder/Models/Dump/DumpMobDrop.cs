namespace Craftiger.Builder.Models.Dump;

/// <summary>One drop of a mob at looting zero: Probability per kill, Type as the dump labels it (INFERNAL drops need an infernal spawn).</summary>
public sealed record DumpMobDrop(string ItemId, double Probability, int StackSize, string Type);
