namespace Craftiger.Solver.Models.Graph;

/// <summary>Which engines read a recipe: None is every engine; Factory rows never price and exist only for rate planning; FactoryMob rows additionally wait for the mob-farms toggle.</summary>
public enum RecipeScope
{
    None,
    Factory,
    FactoryMob,
}
