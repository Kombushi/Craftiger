using System.Text.Json.Serialization;

namespace Craftiger.Api.Models;

/// <summary>Display data for an item a response refers to by id, so clients render icons
/// and names without extra lookups. <paramref name="MaxStack"/> is the pack's stack size,
/// null for fluids, so amounts can read as stacks. Aliases are the display names
/// unification merged away, shown so a canonicalized ingredient stays recognizable.</summary>
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
