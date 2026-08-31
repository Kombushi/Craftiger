namespace Craftiger.Builder.Models.Planner;

/// <summary>Which engines read a recipe: None is every engine; Factory rows exist only for rate planning; FactoryMob rows additionally wait for the mob-farms toggle.</summary>
public enum RecipeScope
{
    None,
    Factory,
    FactoryMob,
}
