namespace Craftiger.Solver.Models.Factory;

/// <summary>A rotor at one fit: its efficiency, its optimal flow in EU/t of fuel value, and the EU/t it makes there.</summary>
public readonly record struct RotorPoint(double Efficiency, double Flow, double Eut)
{
    /// <summary>The output a dynamo cap leaves; a capped rotor still burns its full optimal flow.</summary>
    public double CappedOutput(double cap) => Math.Min(Eut, cap);

    /// <summary>Efficiency judged under the cap: the same fuel makes only the capped output.</summary>
    public double CappedEfficiency(double cap) => Eut <= 0 ? 0 : Efficiency * CappedOutput(cap) / Eut;
}
