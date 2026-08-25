namespace Craftiger.Builder.Models.Planner;

/// <summary>How a recipe trades power for output: the standard ladder halves duration per step, a tree farm keeps its duration and multiplies its outputs instead.</summary>
public enum OverclockMode
{
    Standard,
    TreeFarm,
}
