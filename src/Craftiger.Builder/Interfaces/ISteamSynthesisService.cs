using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Synthesizes the steam carrier's recipes, machine rows and pseudo-fuels, which exist in no dump table.</summary>
public interface ISteamSynthesisService
{
    SteamSynthesis Run(Dump dump, UnifiedItems unified, IReadOnlyList<PlannerBoilerFuel> boilerFuels);
}
