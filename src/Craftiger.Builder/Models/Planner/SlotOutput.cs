namespace Craftiger.Builder.Models.Planner;

/// <summary>An output row with the machine output slot it came from; slots past the first open by machine tier.</summary>
public sealed record SlotOutput(PlannerOutput Output, long Slot);
