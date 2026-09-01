namespace Craftiger.Builder.Models.Dump;

/// <summary>One GregTech ore prefix with GT's own classification flags.</summary>
public sealed record DumpOrePrefix(
    string Name,
    bool Unifiable,
    bool MaterialBased,
    bool Container,
    long MaterialAmount)
{
    /// <summary>A shape's whole substance is its material; containers hold theirs inside something else.</summary>
    public bool IsShape => Unifiable && MaterialBased && !Container;
}
