using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace GrampsDbTool.Tests;

internal sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; } =
        Path.Combine(Path.GetTempPath(), "gramps-db-tool-tests", Guid.NewGuid().ToString("N"));

    public string DatabasePath => Path.Combine(DirectoryPath, "sqlite.db");
    public string MediaPath => Path.Combine(DirectoryPath, "media");
    public string SavePath => Path.Combine(DirectoryPath, "backups");

    public TestDatabase(bool includeMediaPath = true, bool includeSavePath = true, bool includeFanChartData = false)
    {
        Directory.CreateDirectory(DirectoryPath);
        Directory.CreateDirectory(MediaPath);
        Directory.CreateDirectory(SavePath);
        CreateDatabase(includeMediaPath, includeSavePath);
        if (includeFanChartData)
        {
            AddFanChartData();
        }
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
        using var connection =
            new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();

        Execute(connection, """
                            CREATE TABLE metadata (setting VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, value BLOB);
                            CREATE TABLE person (handle VARCHAR(50) PRIMARY KEY NOT NULL, given_name TEXT, surname TEXT, json_data TEXT, gramps_id TEXT, gender INTEGER, death_ref_index INTEGER, birth_ref_index INTEGER, change INTEGER, private INTEGER);
                            CREATE TABLE family (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, father_handle VARCHAR(50), mother_handle VARCHAR(50), change INTEGER, private INTEGER);
                            CREATE TABLE event (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, description TEXT, place VARCHAR(50), change INTEGER, private INTEGER);
                            CREATE TABLE source (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, title TEXT, author TEXT, pubinfo TEXT, abbrev TEXT, change INTEGER, private INTEGER);
                            CREATE TABLE media (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, path TEXT, mime TEXT, desc TEXT, checksum TEXT, change INTEGER, private INTEGER);
                            CREATE TABLE place (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, title TEXT, long TEXT, lat TEXT, change INTEGER, private INTEGER);
                            CREATE TABLE repository (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, type INTEGER, name TEXT, change INTEGER, private INTEGER);
                            CREATE TABLE note (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, format INTEGER, change INTEGER, private INTEGER);
                            CREATE TABLE citation (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, gramps_id TEXT, page TEXT, confidence INTEGER, source_handle VARCHAR(50), change INTEGER, private INTEGER);
                            CREATE TABLE tag (handle VARCHAR(50) PRIMARY KEY NOT NULL, json_data TEXT, name TEXT, color VARCHAR(13), priority INTEGER, change INTEGER);
                            CREATE TABLE reference (obj_handle VARCHAR(50), obj_class TEXT, ref_handle VARCHAR(50), ref_class TEXT);
                            """);

        if (includeMediaPath)
        {
            InsertMetadata(connection, "media-path", MediaPath);
        }

        if (includeSavePath)
        {
            InsertMetadata(connection, "save-path", SavePath);
        }

        InsertTag(connection, "tag1", "Needs Review", "#ff0000", 0, 1000);
        InsertTag(connection, "tag2", "Missing Media", "#ff7800", 1, 1001);

        InsertObject(connection, "person", "person1", "I0001", $$"""
                                                                  {"_class":"Person","handle":"person1","gramps_id":"I0001","gender":1,"primary_name":{"first_name":"Ada","surname_list":[{"surname":"Lovelace"}]},"birth_ref_index":0,"death_ref_index":-1,"event_ref_list":[{"ref":"event1","role":{"value":0,"string":"Primary"},"private":false,"citation_list":["citation1"],"note_list":["note1"]}],"family_list":["family1"],"parent_family_list":[],"media_list":[{"ref":"media1","rect":[0,0,100,100]}],"note_list":["note1"],"citation_list":["citation1"],"tag_list":["tag1"],"person_ref_list":[{"ref":"person2","rel":"Colleague"}],"change":1234,"private":true}
                                                                 """, "given_name", "Ada", "surname", "Lovelace",
            "change", 1234, "private", 1);
        InsertObject(connection, "person", "person2", "I0002", $$"""
                                                                  {"_class":"Person","handle":"person2","gramps_id":"I0002","gender":1,"primary_name":{"first_name":"Charles","surname_list":[{"surname":"Babbage"}]},"event_ref_list":[],"family_list":["family1"],"parent_family_list":[],"media_list":[],"note_list":[],"citation_list":[],"tag_list":[]}
                                                                  """, "given_name", "Charles", "surname", "Babbage");
        InsertObject(connection, "family", "family1", "F0001", """
                                                                {"_class":"Family","handle":"family1","gramps_id":"F0001","father_handle":"person2","mother_handle":"person1","child_ref_list":[{"ref":"person3","frel":{"value":1,"string":"Birth"},"mrel":{"value":1,"string":"Birth"}}],"type":{"value":0,"string":"Married"},"event_ref_list":[{"ref":"event1","role":{"value":1,"string":"Family"}}],"media_list":[{"ref":"media1"}],"note_list":["note1"],"citation_list":["citation1"],"tag_list":["tag2"]}
                                                               """);
        InsertObject(connection, "event", "event1", "E0001", """
                                                              {"_class":"Event","handle":"event1","gramps_id":"E0001","type":{"value":12,"string":"Birth"},"date":{"calendar":0,"modifier":0,"quality":0,"dateval":[10,12,1815,false],"text":"","sortval":2384181,"newyear":0},"description":"Born","place":"place1","note_list":["note1"],"citation_list":["citation1"],"media_list":[{"ref":"media1"}],"tag_list":["tag1"]}
                                                              """, "description", "Born");
        InsertObject(connection, "source", "source1", "S0001", """
                                                               {"_class":"Source","handle":"source1","gramps_id":"S0001","title":"Register","author":"Clerk","pubinfo":"Archive","abbrev":"REG","note_list":["note1"],"media_list":[{"ref":"media1"}],"reporef_list":[{"ref":"repo1","call_number":"MS 42","media_type":{"value":0,"string":"Book"}}],"tag_list":["tag2"]}
                                                               """, "title", "Register");
        InsertObject(connection, "media", "media1", "O0001", """
                                                             {"_class":"Media","handle":"media1","gramps_id":"O0001","path":"photos/ada.jpg","mime":"image/jpeg","desc":"Portrait","checksum":"abc","date":{"calendar":0,"modifier":3,"quality":1,"dateval":[0,0,1840,false],"text":"","sortval":2393097,"newyear":0},"note_list":["note1"],"citation_list":["citation1"],"tag_list":["tag1","tag2"]}
                                                             """, "path", "photos/ada.jpg");
        InsertObject(connection, "media", "media2", "O0002", """
                                                             {"_class":"Media","handle":"media2","gramps_id":"O0002","path":"photos/charles.jpg","mime":"image/jpeg","desc":"Portrait","checksum":"def","note_list":[],"citation_list":[],"tag_list":[]}
                                                             """, "path", "photos/charles.jpg");
        InsertObject(connection, "place", "place1", "P0001", """
                                                             {"_class":"Place","handle":"place1","gramps_id":"P0001","title":"London","long":"-0.1276","lat":"51.5072","placeref_list":[{"ref":"place2","date":{"calendar":0,"modifier":0,"quality":0,"dateval":[0,0,1800,false],"text":"","sortval":2378497,"newyear":[3,25]}}],"name":{"value":"London","lang":"en"},"place_type":{"value":9,"string":"City"},"media_list":[{"ref":"media1"}],"citation_list":["citation1"],"note_list":["note1"],"tag_list":["tag1"]}
                                                             """, "title", "London");
        InsertObject(connection, "place", "place2", "P0002", """
                                                             {"_class":"Place","handle":"place2","gramps_id":"P0002","title":"England","tag_list":[]}
                                                             """, "title", "England");
        InsertObject(connection, "repository", "repo1", "R0001", """
                                                                 {"_class":"Repository","handle":"repo1","gramps_id":"R0001","type":{"value":1,"string":"Archive"},"name":"Archive","note_list":["note1"],"tag_list":["tag2"]}
                                                                 """, "name", "Archive");
        InsertObject(connection, "note", "note1", "N0001", """
                                                           {"_class":"Note","handle":"note1","gramps_id":"N0001","text":{"string":"A note","tags":[{"name":{"value":8,"string":""},"value":"gramps://Person/handle/person1","ranges":[[0,6]]}]},"format":0,"type":{"value":0,"string":"General"},"tag_list":["tag2"]}
                                                           """, "format", 0);
        InsertObject(connection, "citation", "citation1", "C0001", """
                                                                   {"_class":"Citation","handle":"citation1","gramps_id":"C0001","date":{"calendar":0,"modifier":0,"quality":0,"dateval":[1,1,1843,false],"text":"","sortval":2394193,"newyear":0},"page":"p. 1","confidence":3,"source_handle":"source1","note_list":["note1"],"media_list":[{"ref":"media1"}],"tag_list":["tag1"]}
                                                                   """, "page", "p. 1", "confidence", 3,
            "source_handle", "source1");

        Execute(connection, """
                            INSERT INTO reference VALUES ('person1', 'Person', 'event1', 'Event');
                            INSERT INTO reference VALUES ('person1', 'Person', 'media1', 'Media');
                            INSERT INTO reference VALUES ('person1', 'Person', 'person2', 'Person');
                            INSERT INTO reference VALUES ('family1', 'Family', 'event1', 'Event');
                            INSERT INTO reference VALUES ('family1', 'Family', 'media1', 'Media');
                            INSERT INTO reference VALUES ('event1', 'Event', 'place1', 'Place');
                            INSERT INTO reference VALUES ('source1', 'Source', 'repo1', 'Repository');
                            INSERT INTO reference VALUES ('citation1', 'Citation', 'source1', 'Source');
                            INSERT INTO reference VALUES ('place1', 'Place', 'place2', 'Place');
                            INSERT INTO reference VALUES ('note1', 'Note', 'person1', 'Person');
                            """);
    }

    private void AddFanChartData()
    {
        Directory.CreateDirectory(Path.Combine(MediaPath, "photos"));
        File.WriteAllText(Path.Combine(MediaPath, "photos", "ada.jpg"), "test image");

        using var connection =
            new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();
        InsertMetadata(connection, "default-person-handle", "person1");
        UpdateObjectJson(connection, "person", "person1", """
                                                               {"_class":"Person","handle":"person1","gramps_id":"I0001","gender":1,"primary_name":{"first_name":"Ada","surname_list":[{"surname":"Lovelace"}]},"birth_ref_index":0,"death_ref_index":1,"event_ref_list":[{"ref":"event1","role":{"value":0,"string":"Primary"},"private":false,"citation_list":["citation1"],"note_list":["note1"]},{"ref":"event3","role":{"value":0,"string":"Primary"}}],"family_list":["family1"],"parent_family_list":[],"media_list":[{"ref":"media1","rect":[0,0,100,100]}],"note_list":["note1"],"citation_list":["citation1"],"tag_list":["tag1"],"person_ref_list":[{"ref":"person2","rel":"Colleague"}],"change":1234,"private":true}
                                                               """);
        InsertObject(connection, "person", "person3", "I0003", """
                                                                  {"_class":"Person","handle":"person3","gramps_id":"I0003","gender":0,"primary_name":{"first_name":"Cora","surname_list":[{"surname":"Byron"}]},"event_ref_list":[],"family_list":[],"parent_family_list":["family1"],"media_list":[],"note_list":[],"citation_list":[],"tag_list":[]}
                                                                  """);
        UpdateObjectJson(connection, "family", "family1", """
                                                               {"_class":"Family","handle":"family1","gramps_id":"F0001","father_handle":"person2","mother_handle":"person1","child_ref_list":[{"ref":"person3","frel":{"value":1,"string":"Birth"},"mrel":{"value":1,"string":"Birth"}}],"type":{"value":0,"string":"Married"},"event_ref_list":[{"ref":"event1","role":{"value":1,"string":"Family"}},{"ref":"event2","role":{"value":1,"string":"Family"}}],"media_list":[{"ref":"media1"}],"note_list":["note1"],"citation_list":["citation1"],"tag_list":["tag2"]}
                                                               """);
        InsertObject(connection, "event", "event2", "E0002", """
                                                              {"_class":"Event","handle":"event2","gramps_id":"E0002","type":{"value":1,"string":"Marriage"},"date":{"calendar":0,"modifier":0,"quality":0,"dateval":[5,1,1810,false],"text":"","sortval":2382176,"newyear":0},"description":"Married","place":"place1"}
                                                              """, "description", "Married");
        InsertObject(connection, "event", "event3", "E0003", """
                                                              {"_class":"Event","handle":"event3","gramps_id":"E0003","type":{"value":13,"string":"Death"},"date":{"calendar":0,"modifier":0,"quality":0,"dateval":[9,2,1852,false],"text":"","sortval":2397500,"newyear":0},"description":"Died","place":"place1"}
                                                              """, "description", "Died");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void UpdateObjectJson(SqliteConnection connection, string table, string handle, string json)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {table} SET json_data = $json WHERE handle = $handle";
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json", json);
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

    private static void InsertObject(SqliteConnection connection, string table, string handle, string grampsId,
        string json, params object[] indexedValues)
    {
        var columns = new List<string> { "handle", "json_data", "gramps_id" };
        var parameters = new List<string> { "$handle", "$json", "$grampsId" };

        for (var i = 0; i < indexedValues.Length; i += 2)
        {
            columns.Add((string)indexedValues[i]);
            parameters.Add($"$p{i}");
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$grampsId", grampsId);

        for (var i = 0; i < indexedValues.Length; i += 2)
        {
            command.Parameters.AddWithValue($"$p{i}", indexedValues[i + 1]);
        }

        command.ExecuteNonQuery();
    }

    private static void InsertTag(SqliteConnection connection, string handle, string name, string color, int priority,
        long change)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO tag (handle, json_data, name, color, priority, change)
                              VALUES ($handle, $json, $name, $color, $priority, $change)
                              """;
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json",
            JsonSerializer.Serialize(new { _class = "Tag", handle, name, color, priority, change }));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$color", color);
        command.Parameters.AddWithValue("$priority", priority);
        command.Parameters.AddWithValue("$change", change);
        command.ExecuteNonQuery();
    }
}
