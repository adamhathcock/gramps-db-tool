using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace GrampsDbTool.Tests;

internal sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "gramps-db-tool-tests", Guid.NewGuid().ToString("N"));
    public string DatabasePath => Path.Combine(DirectoryPath, "sqlite.db");
    public string MediaPath => Path.Combine(DirectoryPath, "media");
    public string SavePath => Path.Combine(DirectoryPath, "backups");

    public TestDatabase(bool includeMediaPath = true, bool includeSavePath = true)
    {
        Directory.CreateDirectory(DirectoryPath);
        Directory.CreateDirectory(MediaPath);
        Directory.CreateDirectory(SavePath);
        CreateDatabase(includeMediaPath, includeSavePath);
    }

    public string WriteConfig(string? contents = null)
    {
        var configPath = Path.Combine(DirectoryPath, "gramps-db-tool.json");
        File.WriteAllText(configPath, contents ?? $$"""
            {
              "databasePath": "{{DatabasePath.Replace("\\", "\\\\")}}"
            }
            """);
        return configPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private void CreateDatabase(bool includeMediaPath, bool includeSavePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();

        Execute(connection, """
            CREATE TABLE metadata (setting VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, value BLOB);
            CREATE TABLE person (handle VARCHAR(50) PRIMARY KEY NOT NULL, given_name TEXT, surname TEXT, json_data TEXT, gramps_id TEXT, gender INTEGER, death_ref_index INTEGER, birth_ref_index INTEGER, change INTEGER, private INTEGER);
            CREATE TABLE family (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, father_handle VARCHAR(50), mother_handle VARCHAR(50), change INTEGER, private INTEGER);
            CREATE TABLE event (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, description TEXT, place VARCHAR(50), change INTEGER, private INTEGER);
            CREATE TABLE source (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, title TEXT, author TEXT, pubinfo TEXT, abbrev TEXT, change INTEGER, private INTEGER);
            CREATE TABLE media (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, path TEXT, mime TEXT, desc TEXT, checksum TEXT, change INTEGER, private INTEGER);
            CREATE TABLE note (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, format INTEGER, change INTEGER, private INTEGER);
            CREATE TABLE citation (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, page TEXT, confidence INTEGER, source_handle VARCHAR(50), change INTEGER, private INTEGER);
            """);

        if (includeMediaPath)
        {
            InsertMetadata(connection, "media-path", MediaPath);
        }

        if (includeSavePath)
        {
            InsertMetadata(connection, "save-path", SavePath);
        }

        InsertObject(connection, "person", "person1", "I0001", $$"""
            {"_class":"Person","handle":"person1","gramps_id":"I0001","gender":1,"primary_name":{"first_name":"Ada","surname_list":[{"surname":"Lovelace"}]},"event_ref_list":[{"ref":"event1"}],"family_list":["family1"],"parent_family_list":[],"note_list":["note1"],"citation_list":["citation1"]}
            """, "given_name", "Ada", "surname", "Lovelace");
        InsertObject(connection, "family", "family1", "F0001", """
            {"_class":"Family","handle":"family1","gramps_id":"F0001","father_handle":"person2","mother_handle":"person1","child_ref_list":[{"ref":"person3"}],"type":{"value":0,"string":"Married"},"event_ref_list":[{"ref":"event1"}],"note_list":["note1"],"citation_list":["citation1"]}
            """);
        InsertObject(connection, "event", "event1", "E0001", """
            {"_class":"Event","handle":"event1","gramps_id":"E0001","type":{"value":12,"string":"Birth"},"description":"Born","place":"place1","note_list":["note1"],"citation_list":["citation1"],"media_list":[{"ref":"media1"}]}
            """, "description", "Born");
        InsertObject(connection, "source", "source1", "S0001", """
            {"_class":"Source","handle":"source1","gramps_id":"S0001","title":"Register","author":"Clerk","pubinfo":"Archive","abbrev":"REG","note_list":["note1"],"media_list":[{"ref":"media1"}]}
            """, "title", "Register");
        InsertObject(connection, "media", "media1", "O0001", """
            {"_class":"Media","handle":"media1","gramps_id":"O0001","path":"photos/ada.jpg","mime":"image/jpeg","desc":"Portrait","checksum":"abc","note_list":["note1"],"citation_list":["citation1"]}
            """, "path", "photos/ada.jpg");
        InsertObject(connection, "note", "note1", "N0001", """
            {"_class":"Note","handle":"note1","gramps_id":"N0001","text":{"string":"A note"},"format":0,"type":{"value":0,"string":"General"}}
            """, "format", 0);
        InsertObject(connection, "citation", "citation1", "C0001", """
            {"_class":"Citation","handle":"citation1","gramps_id":"C0001","page":"p. 1","confidence":3,"source_handle":"source1","note_list":["note1"],"media_list":[{"ref":"media1"}]}
            """, "page", "p. 1", "confidence", 3, "source_handle", "source1");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void InsertMetadata(SqliteConnection connection, string setting, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO metadata (setting, json_data) VALUES ($setting, $json)";
        command.Parameters.AddWithValue("$setting", setting);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(new { type = "str", value }));
        command.ExecuteNonQuery();
    }

    private static void InsertObject(SqliteConnection connection, string table, string handle, string grampsId, string json, params object[] indexedValues)
    {
        var columns = new List<string> { "handle", "json_data", "gramps_id" };
        var parameters = new List<string> { "$handle", "$json", "$grampsId" };

        for (var i = 0; i < indexedValues.Length; i += 2)
        {
            columns.Add((string)indexedValues[i]);
            parameters.Add($"$p{i}");
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$grampsId", grampsId);

        for (var i = 0; i < indexedValues.Length; i += 2)
        {
            command.Parameters.AddWithValue($"$p{i}", indexedValues[i + 1]);
        }

        command.ExecuteNonQuery();
    }
}
