namespace Craftiger.Api.Models;

/// <summary>A recipes row of planner.sqlite as read at load.</summary>
internal sealed record RecipeRow(
    string Id,
    string Machine,
    long Tier,
    long? MultiTier,
    long? Heat,
    long DurationTicks,
    long EuT,
    long Amps,
    long Cleanroom,
    string? Scope,
    long LowGravity,
    string? Overclock);
