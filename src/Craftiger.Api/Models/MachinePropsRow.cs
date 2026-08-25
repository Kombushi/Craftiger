namespace Craftiger.Api.Models;

/// <summary>A machine_props row of planner.sqlite as read at load.</summary>
internal sealed record MachinePropsRow(
    string ItemId,
    long? Era,
    double? GeneratorEfficiency,
    long? GeneratorEuT,
    long? GeneratorAmps,
    long? DynamoEuT,
    long? DynamoAmps,
    long? MaxParallel,
    string? RotorFuel);
