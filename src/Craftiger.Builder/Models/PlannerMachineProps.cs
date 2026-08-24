namespace Craftiger.Builder.Models;

/// <summary>Rate-planning stats of one machine block, merged from every dump source that
/// knows it; null columns mean the block plays no such role.</summary>
public sealed record PlannerMachineProps(
    string ItemId,
    int? Era,
    double? GeneratorEfficiency,
    long? GeneratorEuT,
    long? GeneratorAmps,
    long? DynamoEuT,
    long? DynamoAmps,
    int? MaxParallel,
    int? BoilerEuT,
    bool RotorTurbine = false);
