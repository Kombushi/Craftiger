using Craftiger.Builder.Models.Dump;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Interfaces.DumpReaders;

public interface IDumpWorldgenReader
{
    DumpWorldgenSet Read(SqliteConnection db);
}
