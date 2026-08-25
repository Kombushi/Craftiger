namespace Craftiger.Builder.Models.Planner;

/// <summary>An input slot's members; one catalyst among them condemns the whole slot, since members are alternatives for one role.</summary>
public sealed record ResolvedSlot(IReadOnlyList<SlotMember> Members, bool Catalyst)
{
    public static readonly ResolvedSlot Empty = new([], false);

    /// <summary>The members with duplicate items folded, first occurrence kept.</summary>
    public IReadOnlyList<SlotMember> Alternatives => Members.DistinctBy(member => member.ItemId).ToList();
}
