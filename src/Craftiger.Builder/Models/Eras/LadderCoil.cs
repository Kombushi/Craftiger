namespace Craftiger.Builder.Models.Eras;

/// <summary>One rung of the coil ladder: a coil name, its heat, and the era the coil is first craftable.</summary>
public sealed record LadderCoil(string Name, int MaxHeat, int Tier);
