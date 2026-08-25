namespace Craftiger.Builder.Models.Planner;

public sealed record PlannerTurbineRotor(
    string ItemId, string Size, string Material, long Durability, double BaseEfficiency,
    int OverflowTier);
