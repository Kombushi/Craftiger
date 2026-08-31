namespace Craftiger.Solver.Models.Graph;

/// <summary>Which engines read a recipe: None is every engine; Factory rows never price and exist only for rate planning; FactoryMob and FactoryBred rows additionally wait for their per-factory toggles.</summary>
public enum RecipeScope
{
    None,
    Factory,
    FactoryMob,
    FactoryBred,
}
