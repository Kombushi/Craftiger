using System.Text.Json.Serialization;

namespace Craftiger.Solver.Models.Factory;

/// <summary>What a factory warning reports; the wire names are what the UI renders.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactoryWarningKind>))]
public enum FactoryWarningKind
{
    [JsonStringEnumMemberName("target_unknown")]
    TargetUnknown,

    [JsonStringEnumMemberName("unreachable_target")]
    UnreachableTarget,

    [JsonStringEnumMemberName("pin_unknown")]
    PinUnknown,

    [JsonStringEnumMemberName("pin_illegal")]
    PinIllegal,

    [JsonStringEnumMemberName("pin_conflict")]
    PinConflict,

    [JsonStringEnumMemberName("step_unknown")]
    StepUnknown,

    [JsonStringEnumMemberName("step_illegal")]
    StepIllegal,

    [JsonStringEnumMemberName("step_variant_unknown")]
    StepVariantUnknown,

    [JsonStringEnumMemberName("supply_unknown")]
    SupplyUnknown,

    [JsonStringEnumMemberName("routes_pruned")]
    RoutesPruned,

    [JsonStringEnumMemberName("no_generator")]
    NoGenerator,

    [JsonStringEnumMemberName("consume_shortfall")]
    ConsumeShortfall,

    [JsonStringEnumMemberName("infeasible_item")]
    InfeasibleItem,

    [JsonStringEnumMemberName("infeasible_energy")]
    InfeasibleEnergy,

    [JsonStringEnumMemberName("infeasible")]
    Infeasible,

    [JsonStringEnumMemberName("free_lunch")]
    FreeLunch,

    [JsonStringEnumMemberName("timeout")]
    Timeout,

    [JsonStringEnumMemberName("solver_error")]
    SolverError,
}
