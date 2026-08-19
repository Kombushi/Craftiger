namespace Craftiger.Builder.Models;

/// <summary>One GregTech ore prefix with GT's own classification flags.</summary>
public sealed record DumpOrePrefix(
    string Name,
    bool Unifiable,
    bool SelfReferencing,
    bool MaterialBased,
    bool Container,
    bool Recyclable,
    long MaterialAmount);
