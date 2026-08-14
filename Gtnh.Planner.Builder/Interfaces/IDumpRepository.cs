using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Loads the converted NESQL dump (SQLite) into memory.</summary>
public interface IDumpRepository
{
    Dump Read(string dumpPath);
}
