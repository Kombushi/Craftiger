namespace Craftiger.Builder.Models;

/// <summary>Voltage is null on wirelessly star-powered recipes, which need no hatch.</summary>
public sealed record DumpGtData(
    string RecipeId, long? Voltage, long Amperage, long Duration, int? Heat, string? TierLabel,
    bool RequiresCleanroom, bool RequiresLowGravity, string Category);
