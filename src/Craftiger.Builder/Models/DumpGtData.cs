namespace Craftiger.Builder.Models;

public sealed record DumpGtData(
    string RecipeId, long Voltage, long Amperage, long Duration, int? Heat, string? TierLabel,
    bool RequiresCleanroom, string Category);
