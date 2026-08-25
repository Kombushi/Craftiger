namespace Craftiger.Builder.Models.Dump;

/// <summary>A fluid pumpable from the ground in one dimension.</summary>
public sealed record DumpUndergroundFluid(string FluidId, string DimensionAbbreviation, int DimensionTier);
