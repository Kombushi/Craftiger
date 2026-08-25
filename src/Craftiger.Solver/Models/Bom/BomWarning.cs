namespace Craftiger.Solver.Models.Bom;

/// <summary>A structured BOM warning the UI renders, naming the item it concerns.</summary>
public sealed record BomWarning(BomWarningKind Kind, string ItemId)
{
    public static BomWarning PinUnknown(string itemId) => new(BomWarningKind.PinUnknown, itemId);

    public static BomWarning PinIllegal(string itemId) => new(BomWarningKind.PinIllegal, itemId);

    public static BomWarning PinCycle(string itemId) => new(BomWarningKind.PinCycle, itemId);

    public static BomWarning UnreachableTarget(string itemId) => new(BomWarningKind.UnreachableTarget, itemId);

    public static BomWarning UnreachableInput(string itemId) => new(BomWarningKind.UnreachableInput, itemId);

    public static BomWarning LoopUnseeded(string itemId) => new(BomWarningKind.LoopUnseeded, itemId);
}
