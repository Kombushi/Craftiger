namespace Craftiger.Builder.Models.Dump;

/// <summary>One fluid input slot; a group with several stacks accepts any one of them.</summary>
public sealed record DumpFluidInput(IReadOnlyList<DumpFluidStack> Members);
