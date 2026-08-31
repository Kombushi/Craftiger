namespace Craftiger.Api.Models;

/// <summary>One runnable generator line for the Planner's picker; Id is what a pipeline step names.</summary>
public sealed record GeneratorLineDto(
    string Id,
    string Map,
    string MachineItemId,
    string FuelItemId,
    int Tier,
    double NetEuT,
    double FuelPerSecond,
    string? Variant);
