namespace Craftiger.Builder.Models.Dump;

/// <summary>A single-block generator's conversion stats; efficiency is a percentage and may exceed 100.</summary>
public sealed record DumpGenerator(string ItemId, double Efficiency, long MaxEuOutput, long AmpsOut);
