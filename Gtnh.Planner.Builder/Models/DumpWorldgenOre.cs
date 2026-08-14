namespace Gtnh.Planner.Builder.Models;

/// <summary>One worldgen placement of an ore item; MaterialName recovers the oredict for un-oredicted stone variants.</summary>
public sealed record DumpWorldgenOre(
    string ItemId, string? MaterialName, string DimensionAbbreviation, int DimensionTier, bool IsDrop);
