namespace Craftiger.Builder.Models.Dump;

/// <summary>Voltage is null on wirelessly star-powered recipes, which need no hatch; SpecialItemId is the controller-slot item a map shows beside its inputs.</summary>
public sealed record DumpGtData(
    long? Voltage, long Amperage, long Duration, int? Heat, string? TierLabel,
    bool RequiresCleanroom, bool RequiresLowGravity, long? SpecialValue, string? AdditionalInfo,
    string Category, string? SpecialItemId = null);
