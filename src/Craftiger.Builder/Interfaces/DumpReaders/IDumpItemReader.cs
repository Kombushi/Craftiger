using Craftiger.Builder.Models.Dump;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Interfaces.DumpReaders;

public interface IDumpItemReader
{
    DumpItemSet Read(SqliteConnection db);
}
