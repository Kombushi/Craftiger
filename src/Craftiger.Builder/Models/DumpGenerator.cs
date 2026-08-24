namespace Craftiger.Builder.Models;

/// <summary>A single-block generator's conversion stats; efficiency is percent and may
/// exceed 100.</summary>
public sealed record DumpGenerator(string ItemId, double Efficiency, long MaxEuOutput, long AmpsOut);
