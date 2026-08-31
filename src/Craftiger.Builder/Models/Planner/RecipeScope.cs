namespace Craftiger.Builder.Models.Planner;

/// <summary>Which engines read a recipe: None is every engine; Factory rows exist only for rate planning; FactoryMob and FactoryBred rows additionally wait for their per-factory toggles.</summary>
public enum RecipeScope
{
    None,
    Factory,
    FactoryMob,
    FactoryBred,
}
