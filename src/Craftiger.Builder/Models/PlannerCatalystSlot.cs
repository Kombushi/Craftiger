namespace Craftiger.Builder.Models;

/// <summary>One slot a recipe needs in place but never consumes — its members are alternatives
/// for the same role. Shipped for display and for the tie-break on wearing tools, never priced
/// and never era-gated.</summary>
public sealed record PlannerCatalystSlot(IReadOnlyList<PlannerCatalyst> Alternatives);
