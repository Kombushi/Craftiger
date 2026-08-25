using Craftiger.Builder.Models.Options;

namespace Craftiger.Builder.Models.Eras;

/// <summary>The EBF coil ladder as an era gate: the first coil reaching a heat sets the tier a heat recipe needs.</summary>
public sealed record CoilLadder(IReadOnlyList<CoilSpec> Coils)
{
    /// <summary>The tier of the lowest coil reaching the heat, one past the ladder when none does.</summary>
    public int TierFor(int heat)
    {
        foreach (var coil in Coils)
        {
            if (heat <= coil.MaxHeat)
            {
                return coil.Tier;
            }
        }
        return Coils[^1].Tier + 1;
    }

    /// <summary>A recipe's own floor at a voltage tier: its coil gate, if it has one.</summary>
    public int Floor(int tier, int? heat) => heat is { } required ? Math.Max(tier, TierFor(required)) : tier;
}
