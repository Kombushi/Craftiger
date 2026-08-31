namespace Craftiger.Solver.Models.Factory;

/// <summary>How a recipe trades power for output: the standard ladder halves duration per step, a tree farm keeps its duration and multiplies its outputs, a fixed row never climbs, and the entity crusher quarters duration to a floor before multiplying outputs.</summary>
public enum OverclockMode
{
    Standard,
    TreeFarm,
    Fixed,
    EntityCrusher,
}
