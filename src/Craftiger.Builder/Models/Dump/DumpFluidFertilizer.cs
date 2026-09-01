namespace Craftiger.Builder.Models.Dump;

/// <summary>A fluid fertilizer with its per-liter potency, from the CropsNH registry export.</summary>
public sealed record DumpFluidFertilizer(string FluidId, int Potency);
