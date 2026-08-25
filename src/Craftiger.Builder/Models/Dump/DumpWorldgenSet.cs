namespace Craftiger.Builder.Models.Dump;

/// <summary>What the world generates: placed ores, small-ore drops, and pumpable fluids per dimension.</summary>
public sealed record DumpWorldgenSet(
    IReadOnlyList<DumpWorldgenOre> WorldgenOres,
    IReadOnlyList<DumpUndergroundFluid> UndergroundFluids);
