using System.Text.Json.Serialization;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Terminal state of a factory solve; Unbounded means a free-lunch cycle survived into the model, always a data defect.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactoryPlanStatus>))]
public enum FactoryPlanStatus
{
    [JsonStringEnumMemberName("solved")]
    Solved,

    [JsonStringEnumMemberName("infeasible")]
    Infeasible,

    [JsonStringEnumMemberName("unbounded")]
    Unbounded,

    [JsonStringEnumMemberName("timed_out")]
    TimedOut,

    [JsonStringEnumMemberName("failed")]
    Failed,
}
