namespace Craftiger.Api.Models;

/// <summary>Display data for one recipe as planner.sqlite ships it.</summary>
public sealed record ArtifactRecipe(
    string Id, string Machine, int Tier, int? MultiTier, int? Heat,
    long DurationTicks, long EuT);
