using System.Text.Json.Serialization;

namespace Craftiger.Solver.Models.Bom;

/// <summary>What a BOM warning reports; the wire names are what the UI renders.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BomWarningKind>))]
public enum BomWarningKind
{
    [JsonStringEnumMemberName("pin_unknown")]
    PinUnknown,

    [JsonStringEnumMemberName("pin_illegal")]
    PinIllegal,

    [JsonStringEnumMemberName("pin_cycle")]
    PinCycle,

    [JsonStringEnumMemberName("unreachable_target")]
    UnreachableTarget,

    [JsonStringEnumMemberName("unreachable_input")]
    UnreachableInput,

    [JsonStringEnumMemberName("loop_unseeded")]
    LoopUnseeded,
}
