using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

public interface IMachinePropsService
{
    MachinePropsData Run(
        Dump dump, UnifiedItems unified, IReadOnlyDictionary<string, int> era,
        IReadOnlyList<PlannerMachineItem> synthesized);
}
