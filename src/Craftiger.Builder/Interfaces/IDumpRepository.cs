using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Loads the converted NESQL dump (SQLite) into memory.</summary>
public interface IDumpRepository
{
    Dump Read(string dumpPath);
}
