using GrampsDbTool.Configuration;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsConnectionFactory(GrampsConfig config)
{
    public SqliteConnection CreateReadOnlyConnection()
    {
        return CreateConnection(SqliteOpenMode.ReadOnly);
    }

    public SqliteConnection CreateReadWriteConnection()
    {
        return CreateConnection(SqliteOpenMode.ReadWrite);
    }

    private SqliteConnection CreateConnection(SqliteOpenMode mode)
    {
        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}", config.DatabasePath);
        }

        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = config.DatabasePath,
            Mode = mode
        }.ToString());
    }
}
