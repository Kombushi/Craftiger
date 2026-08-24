using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

public interface ISteamSynthesisService
{
    SteamSynthesis Run(Dump dump, UnifiedItems unified, IReadOnlyList<PlannerBoilerFuel> boilerFuels);
}
