using Craftiger.Api.Models;
using Craftiger.Solver.Models.Factory;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.Interfaces;

/// <summary>Reads the factory tables of an open planner.sqlite.</summary>
public interface IFactoryArtifactReader
{
    FactoryArtifactData Read(SqliteConnection db, IReadOnlyDictionary<string, string> meta, FactoryRecipeData recipes);
}
