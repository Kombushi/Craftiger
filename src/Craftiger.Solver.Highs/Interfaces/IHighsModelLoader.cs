using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Models.Lp;
using Highs;

namespace Craftiger.Solver.Highs.Interfaces;

/// <summary>Configures a native solver instance and loads a scaled program into it.</summary>
public interface IHighsModelLoader
{
    void Configure(HighsLpSolver solver);

    void Load(HighsLpSolver solver, LinearProgram program, LpScaling scaling);
}
