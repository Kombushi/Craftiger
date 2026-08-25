namespace Craftiger.Builder.Models.Dump;

/// <summary>The item and fluid registries with what their tooltips reveal: input voltages and deprecation banners.</summary>
public sealed record DumpItemSet(
    IReadOnlyDictionary<string, DumpItem> Items,
    IReadOnlyDictionary<string, DumpFluid> Fluids,
    IReadOnlyDictionary<string, int> MachineVoltageTiers,
    IReadOnlySet<string> DeprecatedItems);
