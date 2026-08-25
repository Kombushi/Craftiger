using System.Text.Json.Serialization;

namespace Craftiger.Api.Models;

/// <summary>Display data for an item a response refers to by id; Aliases are the display names unification merged away, so a canonicalized ingredient stays recognizable.</summary>
public sealed record ItemRefDto(
    string Name,
    long AtlasIdx,
    bool IsFluid,
    string? LeafClass,
    double? Cost,
    bool Uncraftable,
    int? MaxStack,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Aliases = null);
