namespace Craftiger.Solver.Models.Factory;

/// <summary>A structured factory warning the UI renders; ItemId is empty where the warning concerns no item.</summary>
public sealed record FactoryWarning(FactoryWarningKind Kind, string ItemId = "")
{
    public static FactoryWarning TargetUnknown(string itemId) => new(FactoryWarningKind.TargetUnknown, itemId);

    public static FactoryWarning UnreachableTarget(string itemId) => new(FactoryWarningKind.UnreachableTarget, itemId);

    public static FactoryWarning PinUnknown(string itemId) => new(FactoryWarningKind.PinUnknown, itemId);

    public static FactoryWarning PinIllegal(string itemId) => new(FactoryWarningKind.PinIllegal, itemId);

    public static FactoryWarning PinConflict(string itemId) => new(FactoryWarningKind.PinConflict, itemId);

    public static FactoryWarning RoutesPruned() => new(FactoryWarningKind.RoutesPruned);

    public static FactoryWarning NoGenerator() => new(FactoryWarningKind.NoGenerator);

    public static FactoryWarning ConsumeShortfall(string itemId) => new(FactoryWarningKind.ConsumeShortfall, itemId);

    public static FactoryWarning InfeasibleItem(string itemId) => new(FactoryWarningKind.InfeasibleItem, itemId);

    public static FactoryWarning InfeasibleEnergy() => new(FactoryWarningKind.InfeasibleEnergy);

    public static FactoryWarning Infeasible() => new(FactoryWarningKind.Infeasible);

    public static FactoryWarning FreeLunch() => new(FactoryWarningKind.FreeLunch);

    public static FactoryWarning Timeout() => new(FactoryWarningKind.Timeout);

    public static FactoryWarning SolverError() => new(FactoryWarningKind.SolverError);
}
