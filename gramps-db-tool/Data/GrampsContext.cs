using System.Data;
using System.Text.Json;
using GrampsDbTool.Models;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed partial class GrampsContext(GrampsDatabaseOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] PrimaryTables =
    [
        "person", "family", "event", "place", "source", "citation", "media", "repository", "note", "tag", "reference", "metadata"
    ];

    private readonly string? databasePath = options.DatabasePath;
    private readonly bool allowWrites = options.AllowWrites;
    private readonly string? mediaDirectory = options.MediaDirectory;

    public GrampsObjectSet<Person> People => new(this, "person");
    public GrampsObjectSet<Family> Families => new(this, "family");
    public GrampsObjectSet<Event> Events => new(this, "event");
    public GrampsObjectSet<Place> Places => new(this, "place");
    public GrampsObjectSet<Source> Sources => new(this, "source");
    public GrampsObjectSet<Citation> Citations => new(this, "citation");
    public GrampsObjectSet<Note> Notes => new(this, "note");
    public GrampsObjectSet<Media> Media => new(this, "media");
    public GrampsObjectSet<Repository> Repositories => new(this, "repository");
    public GrampsObjectSet<Tag> Tags => new(this, "tag");

    public DatabaseInfo GetDatabaseInfo()
    {
        using var connection = OpenConnection();

        var tables = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select name from sqlite_master where type = 'table' order by name";
            using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var metadata = TableExists(connection, "metadata") ? ReadMetadata(connection) : [];
        var grampsTables = PrimaryTables.ToDictionary(table => table, table => TableExists(connection, table));

        return new DatabaseInfo(
            databasePath,
            ExecuteScalar(connection, "select sqlite_version()")?.ToString(),
            tables,
            grampsTables,
            metadata);
    }

    public IReadOnlyList<PersonSummary> SearchPeople(string? search, int maxRows)
    {
        maxRows = Math.Clamp(maxRows, 1, 100);

        using var connection = OpenConnection();
        if (!TableExists(connection, "person"))
        {
            return [];
        }

        using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(search))
        {
            command.CommandText = """
                select handle, gramps_id, given_name, surname, gender, birth_ref_index, death_ref_index, change, private
                from person
                order by surname, given_name, gramps_id
                limit $limit
                """;
        }
        else
        {
            command.CommandText = """
                select handle, gramps_id, given_name, surname, gender, birth_ref_index, death_ref_index, change, private
                from person
                where given_name like $search escape '\'
                   or surname like $search escape '\'
                   or gramps_id like $search escape '\'
                   or handle like $search escape '\'
                order by surname, given_name, gramps_id
                limit $limit
                """;
            command.Parameters.AddWithValue("$search", $"%{EscapeLike(search.Trim())}%");
        }

        command.Parameters.AddWithValue("$limit", maxRows);

        using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
        var people = new List<PersonSummary>();
        while (reader.Read())
        {
            people.Add(new PersonSummary(
                GetString(reader, "handle") ?? string.Empty,
                GetString(reader, "gramps_id"),
                GetString(reader, "given_name"),
                GetString(reader, "surname"),
                GetInt32(reader, "gender"),
                GetInt32(reader, "birth_ref_index"),
                GetInt32(reader, "death_ref_index"),
                GetInt64(reader, "change"),
                GetBoolean(reader, "private")));
        }

        return people;
    }

    internal T? GetByHandle<T>(string tableName, string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new ArgumentException("Handle is required.", nameof(handle));
        }

        using var connection = OpenConnection();
        return GetByColumn<T>(connection, tableName, "handle", handle);
    }

    internal T? GetByGrampsId<T>(string tableName, string grampsId)
    {
        if (string.IsNullOrWhiteSpace(grampsId))
        {
            throw new ArgumentException("Gramps ID is required.", nameof(grampsId));
        }

        if (tableName == "tag")
        {
            throw new InvalidOperationException("Tags do not have Gramps IDs.");
        }

        using var connection = OpenConnection();
        return GetByColumn<T>(connection, tableName, "gramps_id", grampsId);
    }

    public object? GetRecord(string objectType, string handle)
    {
        var tableName = NormalizeObjectType(objectType);
        return tableName switch
        {
            "person" => People.GetByHandle(handle),
            "family" => Families.GetByHandle(handle),
            "event" => Events.GetByHandle(handle),
            "place" => Places.GetByHandle(handle),
            "source" => Sources.GetByHandle(handle),
            "citation" => Citations.GetByHandle(handle),
            "media" => Media.GetByHandle(handle),
            "repository" => Repositories.GetByHandle(handle),
            "note" => Notes.GetByHandle(handle),
            "tag" => Tags.GetByHandle(handle),
            _ => throw new ArgumentException($"Unsupported object type: {objectType}.", nameof(objectType))
        };
    }

    public object? GetRecordById(string objectType, string grampsId)
    {
        var tableName = NormalizeObjectType(objectType);
        return tableName switch
        {
            "person" => People.GetByGrampsId(grampsId),
            "family" => Families.GetByGrampsId(grampsId),
            "event" => Events.GetByGrampsId(grampsId),
            "place" => Places.GetByGrampsId(grampsId),
            "source" => Sources.GetByGrampsId(grampsId),
            "citation" => Citations.GetByGrampsId(grampsId),
            "media" => Media.GetByGrampsId(grampsId),
            "repository" => Repositories.GetByGrampsId(grampsId),
            "note" => Notes.GetByGrampsId(grampsId),
            "tag" => throw new InvalidOperationException("Tags do not have Gramps IDs."),
            _ => throw new ArgumentException($"Unsupported object type: {objectType}.", nameof(objectType))
        };
    }

    internal IReadOnlyList<T> List<T>(string tableName, int maxRows)
    {
        maxRows = Math.Clamp(maxRows, 1, 500);

        using var connection = OpenConnection();
        if (!TableExists(connection, tableName))
        {
            return [];
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"select json_data from {QuoteIdentifier(tableName)} order by handle limit $limit";
        command.Parameters.AddWithValue("$limit", maxRows);

        using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
        var results = new List<T>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && Deserialize<T>(reader.GetString(0)) is { } value)
            {
                results.Add(value);
            }
        }

        return results;
    }

    private T? GetByColumn<T>(SqliteConnection connection, string tableName, string columnName, string value)
    {
        if (!TableExists(connection, tableName))
        {
            return default;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"select json_data from {QuoteIdentifier(tableName)} where {QuoteIdentifier(columnName)} = $value limit 1";
        command.Parameters.AddWithValue("$value", value);

        var jsonData = command.ExecuteScalar() as string;
        return string.IsNullOrWhiteSpace(jsonData) ? default : Deserialize<T>(jsonData);
    }

    private SqliteConnection OpenConnection(bool writable = false)
    {
        var resolvedDatabasePath = ResolveDatabasePath();

        if (writable && !allowWrites)
        {
            throw new InvalidOperationException("Writes are disabled. Start the server with --allow-writes or GRAMPS_ALLOW_WRITES=true to modify the database.");
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = resolvedDatabasePath,
            Mode = writable ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = writable ? "pragma busy_timeout = 5000" : "pragma query_only = on";
        command.ExecuteNonQuery();

        return connection;
    }

    private string ResolveDatabasePath()
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException("Set the Gramps SQLite path with --database <path> or GRAMPS_SQLITE_PATH.");
        }

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("Gramps SQLite database was not found.", databasePath);
        }

        return databasePath;
    }

    private static IReadOnlyList<MetadataEntry> ReadMetadata(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select setting, json_data, value from metadata order by setting";

        using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
        var metadata = new List<MetadataEntry>();
        while (reader.Read())
        {
            metadata.Add(new MetadataEntry(
                GetString(reader, "setting") ?? string.Empty,
                GetString(reader, "json_data"),
                GetString(reader, "value")));
        }

        return metadata;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from sqlite_master where type = 'table' and name = $name)";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static object? ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static T? Deserialize<T>(string jsonData) => JsonSerializer.Deserialize<T>(jsonData, JsonOptions);

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static string QuoteIdentifier(string identifier)
    {
        if (identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("Invalid SQLite identifier.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }

    private static string? GetString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int GetInt32(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static long GetInt64(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
    }

    private static bool GetBoolean(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }
}

public sealed class GrampsObjectSet<T>(GrampsContext context, string tableName)
{
    public T? GetByHandle(string handle) => context.GetByHandle<T>(tableName, handle);

    public T? GetByGrampsId(string grampsId) => context.GetByGrampsId<T>(tableName, grampsId);

    public IReadOnlyList<T> List(int maxRows = 100) => context.List<T>(tableName, maxRows);
}
