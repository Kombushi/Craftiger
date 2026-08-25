namespace Craftiger.Solver.Models.Factory;

/// <summary>One rotor's turbine numbers for one fuel class, tight and loose fit; flows and outputs are EU/t at the rotor's optimal flow.</summary>
public sealed record FactoryRotorStats(
    string ItemId,
    string Fuel,
    double Efficiency,
    double LooseEfficiency,
    double OptimalFlow,
    double LooseOptimalFlow,
    double OptimalEut,
    double LooseOptimalEut)
{
    public RotorPoint At(RotorFit fit) => fit == RotorFit.Loose
        ? new RotorPoint(LooseEfficiency, LooseOptimalFlow, LooseOptimalEut)
        : new RotorPoint(Efficiency, OptimalFlow, OptimalEut);

    /// <summary>Whether the other rotor is at least as good on both capped axes and better on one, ties broken by id.</summary>
    public bool IsDominatedBy(FactoryRotorStats other, RotorFit fit, double cap)
    {
        var mine = At(fit);
        var theirs = other.At(fit);
        var myEff = mine.CappedEfficiency(cap);
        var myOut = mine.CappedOutput(cap);
        var theirEff = theirs.CappedEfficiency(cap);
        var theirOut = theirs.CappedOutput(cap);
        return theirEff >= myEff && theirOut >= myOut
            && (theirEff > myEff || theirOut > myOut || string.CompareOrdinal(other.ItemId, ItemId) < 0);
    }
}
