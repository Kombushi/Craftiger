namespace Craftiger.Api.Models;

/// <summary>A renewable_seeds row of planner.sqlite as read at load.</summary>
internal sealed record SeedRow(string ItemId, string Kind);
