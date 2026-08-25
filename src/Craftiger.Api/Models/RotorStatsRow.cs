namespace Craftiger.Api.Models;

/// <summary>A rotor_fuel_stats row of planner.sqlite as read at load.</summary>
internal sealed record RotorStatsRow(
    string ItemId,
    string Fuel,
    double Efficiency,
    double LooseEfficiency,
    double OptimalFlow,
    double LooseOptimalFlow,
    double OptimalEut,
    double LooseOptimalEut);
