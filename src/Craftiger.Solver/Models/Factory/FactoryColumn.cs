namespace Craftiger.Solver.Models.Factory;

/// <summary>What an LP column means when the solution is read back.</summary>
public abstract record FactoryColumn;

/// <summary>Runs per second of a recipe on one variant.</summary>
public sealed record RunColumn(int Recipe, RunVariant Variant) : FactoryColumn;

/// <summary>Runs per second of a recipe fed through one alternative of a choice slot.</summary>
public sealed record SplitColumn(int Recipe, int Item, long Amount) : FactoryColumn;

/// <summary>Units per second of a leaf bought from outside.</summary>
public sealed record BuyColumn(int Item) : FactoryColumn;

/// <summary>Machines running one generator line.</summary>
public sealed record GenerateColumn(GeneratorLine Line) : FactoryColumn;

/// <summary>Units per second a consume target delivers.</summary>
public sealed record SupplyColumn(int Item) : FactoryColumn;
