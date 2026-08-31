namespace Craftiger.Builder.Models.Planner;

/// <summary>The environment walls the artifact ships: the cleanroom's item and era, and the first rocket's era gating low gravity.</summary>
public sealed record PlannerEnvironment(string CleanroomItemId, int CleanroomEra, int LowGravityEra);
