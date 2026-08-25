using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Merges the dump's per-machine stat tables and the curated overlay into planner rows.</summary>
public interface IMachinePropsService
{
    MachinePropsData Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, int> era,
        IReadOnlyList<PlannerMachineItem> synthesized);
}
