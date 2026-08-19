import java.nio.file.Files;
import java.nio.file.Path;
import java.sql.Clob;
import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.ResultSetMetaData;
import java.sql.SQLException;
import java.sql.Statement;
import java.sql.Types;
import java.util.ArrayList;
import java.util.List;

/**
 * Copies every table of an HSQLDB NESQL export into a SQLite database, preserving
 * Hibernate's column names. The HSQLDB side opens read-only, so the export stays untouched.
 */
public final class Convert
{
    public static void main(String[] args) throws Exception
    {
        if (args.length != 2)
        {
            System.err.println("usage: Convert <nesql-repository-dir> <output.sqlite>");
            System.exit(2);
        }
        Path source = Path.of(args[0]);
        if (Files.isDirectory(source))
        {
            source = source.resolve("nesql-db");
        }
        Path output = Path.of(args[1]);
        Files.deleteIfExists(output);

        // The source-file launcher's class loader breaks JDBC driver auto-registration.
        Class.forName("org.hsqldb.jdbc.JDBCDriver");
        Class.forName("org.sqlite.JDBC");

        try (Connection hsql = DriverManager.getConnection(
                "jdbc:hsqldb:file:" + source + ";readonly=true", "sa", "");
             Connection sqlite = DriverManager.getConnection("jdbc:sqlite:" + output))
        {
            try (Statement pragma = sqlite.createStatement())
            {
                pragma.execute("PRAGMA journal_mode=OFF");
                pragma.execute("PRAGMA synchronous=OFF");
            }
            sqlite.setAutoCommit(false);

            List<String> tables = new ArrayList<>();
            try (ResultSet rs = hsql.getMetaData().getTables(
                    null, "PUBLIC", "%", new String[] { "TABLE" }))
            {
                while (rs.next())
                {
                    tables.add(rs.getString("TABLE_NAME"));
                }
            }

            long total = 0;
            for (String table : tables)
            {
                total += CopyTable(hsql, sqlite, table);
            }
            sqlite.commit();
            System.out.printf("Converted %d tables, %d rows -> %s%n", tables.size(), total, output);
        }
    }

    private static long CopyTable(Connection hsql, Connection sqlite, String table)
            throws SQLException
    {
        try (Statement statement = hsql.createStatement();
             ResultSet rows = statement.executeQuery(
                 "SELECT * FROM \"PUBLIC\".\"" + table + "\""))
        {
            ResultSetMetaData meta = rows.getMetaData();
            int columns = meta.getColumnCount();
            StringBuilder create = new StringBuilder("CREATE TABLE \"" + table + "\" (");
            StringBuilder insert = new StringBuilder("INSERT INTO \"" + table + "\" VALUES (");
            for (int i = 1; i <= columns; i++)
            {
                create.append(i > 1 ? ", " : "")
                    .append('"').append(meta.getColumnName(i)).append("\" ")
                    .append(SqliteType(meta.getColumnType(i)));
                insert.append(i > 1 ? ", ?" : "?");
            }
            try (Statement ddl = sqlite.createStatement())
            {
                ddl.execute(create.append(")").toString());
            }

            long count = 0;
            try (PreparedStatement writer = sqlite.prepareStatement(insert.append(")").toString()))
            {
                while (rows.next())
                {
                    for (int i = 1; i <= columns; i++)
                    {
                        writer.setObject(i, Normalize(rows.getObject(i)));
                    }
                    writer.addBatch();
                    if (++count % 50_000 == 0)
                    {
                        writer.executeBatch();
                    }
                }
                writer.executeBatch();
            }
            System.out.printf("  %s: %d rows%n", table, count);
            return count;
        }
    }

    private static String SqliteType(int jdbcType)
    {
        return switch (jdbcType)
        {
            case Types.TINYINT, Types.SMALLINT, Types.INTEGER, Types.BIGINT,
                 Types.BOOLEAN, Types.BIT -> "INTEGER";
            case Types.FLOAT, Types.DOUBLE, Types.REAL, Types.NUMERIC, Types.DECIMAL -> "REAL";
            case Types.BLOB, Types.BINARY, Types.VARBINARY, Types.LONGVARBINARY -> "BLOB";
            default -> "TEXT";
        };
    }

    /** Booleans must land as 0/1: a TEXT 'FALSE' would satisfy every `!= 0` check downstream. */
    private static Object Normalize(Object value) throws SQLException
    {
        if (value instanceof Boolean flag)
        {
            return flag ? 1 : 0;
        }
        if (value instanceof Clob clob)
        {
            return clob.getSubString(1, (int) clob.length());
        }
        return value;
    }
}
