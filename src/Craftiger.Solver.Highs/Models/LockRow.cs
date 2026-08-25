namespace Craftiger.Solver.Highs.Models;

/// <summary>A lock row added after a layer: its position, coefficients and bounds, kept so it can be re-bounded later.</summary>
public readonly record struct LockRow(int Row, int[] Indices, double[] Values, double Lower, double Upper)
{
    /// <summary>The row's activity at a solution.</summary>
    public double Activity(double[] standing)
    {
        var activity = 0.0;
        for (var i = 0; i < Indices.Length; i++)
        {
            activity += Values[i] * standing[Indices[i]];
        }
        return activity;
    }
}
