using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Synthesizes factory-scoped Extreme Entity Crusher lines from the dump's mob tables.</summary>
public interface IMobLineRecipeService
{
    MobLines Run(Dump dump, UnifiedItems unified);
}
