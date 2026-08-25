namespace Craftiger.Api.Models;

/// <summary>A fuels row of planner.sqlite as read at load.</summary>
internal sealed record FuelRow(string Map, string ItemId, long Amount, double? EuPerUnit, double? EuT, long? DurationTicks);
