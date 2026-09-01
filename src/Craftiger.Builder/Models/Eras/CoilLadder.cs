namespace Craftiger.Builder.Models.Eras;

/// <summary>The EBF coil ladder as an era gate: the cheapest coil reaching a heat sets the tier a heat recipe needs.</summary>
public sealed record CoilLadder(IReadOnlyList<LadderCoil> Coils)
{
    /// <summary>The lowest tier among coils reaching the heat; one past the ladder when none does, unreachable when it is empty.</summary>
    public int TierFor(int heat)
    {
        var best = int.MaxValue;
        foreach (var coil in Coils)
        {
            if (heat <= coil.MaxHeat && coil.Tier < best)
            {
                best = coil.Tier;
            }
        }
        if (best < int.MaxValue)
        {
            return best;
        }
        return Coils.Count == 0 ? int.MaxValue : Coils.Max(coil => coil.Tier) + 1;
    }

    /// <summary>A recipe's own floor at a voltage tier: its coil gate, if it has one.</summary>
    public int Floor(int tier, int? heat) => heat is { } required ? Math.Max(tier, TierFor(required)) : tier;
}
