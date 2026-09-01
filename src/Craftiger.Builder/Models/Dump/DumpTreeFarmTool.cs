using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Models.Dump;

/// <summary>A tool the Tree Growth Simulator accepts for one mode, probed through the machine's own code.</summary>
public sealed record DumpTreeFarmTool(string ItemId, TreeFarmMode Mode, int Multiplier);
