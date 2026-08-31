namespace Craftiger.Builder.Models.Planner;

/// <summary>How a recipe trades power for output: the standard ladder halves duration per step, a tree farm keeps its duration and multiplies its outputs instead.</summary>
public enum OverclockMode
{
    Standard,
    TreeFarm,

    /// <summary>Exact per-tier rows: never overclocked.</summary>
    Fixed,

    /// <summary>The EEC's ladder: perfect overclocks to a 20-tick floor, then quadrupled outputs.</summary>
    EntityCrusher,
}
