using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories.DumpReaders;

/// <summary>What every reader needs of the dump's shape: table and column presence, and the multimap shorthand.</summary>
public static class DumpQueries
{
    public static bool HasTable(SqliteConnection db, string table) =>
        db.Query<string>("SELECT name FROM sqlite_master WHERE type = 'table'")
            .Any(name => name.Equals(table, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lets the builder read a dump taken before a column existed.</summary>
    public static bool HasColumn(SqliteConnection db, string table, string column) =>
        db.Query<string>($"SELECT name FROM pragma_table_info('{table}')")
            .Any(name => name.Equals(column, StringComparison.OrdinalIgnoreCase));

    public static void RequireTable(SqliteConnection db, string table, string exporterVersion)
    {
        if (!HasTable(db, table))
        {
            throw new InvalidOperationException(
                $"dump predates {table}; re-export with exporter {exporterVersion} or later");
        }
    }

    public static void RequireMachineProps(SqliteConnection db, string table) => RequireTable(db, table, "0.6.5");

    public static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }
        list.Add(value);
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<T>> Freeze<T>(Dictionary<string, List<T>> map) =>
        map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<T>)pair.Value);
}
