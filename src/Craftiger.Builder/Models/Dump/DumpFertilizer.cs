namespace Craftiger.Builder.Models.Dump;

/// <summary>An item fertilizer with its per-use potency, from the CropsNH registry export.</summary>
public sealed record DumpFertilizer(string ItemId, int Potency);
