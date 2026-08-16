namespace Craftiger.Solver.Models;

/// <summary>The user's machine garage. <paramref name="MachineTiers"/> overrides the default
/// per machine, with null meaning the machine is not owned at all. <paramref name="BuiltMultiblocks"/>
/// names the mixed maps whose multiblock is built, unlocking their lower recipe tier.
/// <paramref name="CoilHeat"/> is the installed coil's max heat per heat-gated map; a map
/// without an entry has no coils installed.</summary>
public sealed record Garage(
    int DefaultTier,
    IReadOnlyDictionary<string, int?> MachineTiers,
    IReadOnlySet<string> BuiltMultiblocks,
    IReadOnlyDictionary<string, int> CoilHeat);
