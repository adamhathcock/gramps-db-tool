using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using GrampsDbTool.Models;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed partial class GrampsContext
{
    private const string MediaIndexSetting = "omap_index";

    private static readonly IReadOnlyDictionary<string, string> ObjectTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = "person",
        ["family"] = "family",
        ["event"] = "event",
        ["place"] = "place",
        ["source"] = "source",
        ["citation"] = "citation",
        ["media"] = "media",
        ["repository"] = "repository",
        ["note"] = "note",
        ["tag"] = "tag"
    };

    private static readonly IReadOnlyDictionary<string, string> TableClasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = "Person",
        ["family"] = "Family",
        ["event"] = "Event",
        ["place"] = "Place",
        ["source"] = "Source",
        ["citation"] = "Citation",
        ["media"] = "Media",
        ["repository"] = "Repository",
        ["note"] = "Note",
        ["tag"] = "Tag"
    };

    private static readonly IReadOnlyDictionary<string, string> ReferenceProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["citation_list"] = "Citation",
        ["note_list"] = "Note",
        ["tag_list"] = "Tag",
        ["media_list"] = "Media",
        ["event_ref_list"] = "Event",
        ["family_list"] = "Family",
        ["parent_family_list"] = "Family",
        ["person_ref_list"] = "Person",
        ["child_ref_list"] = "Person",
        ["reporef_list"] = "Repository",
        ["placeref_list"] = "Place",
        ["source_handle"] = "Source",
        ["father_handle"] = "Person",
        ["mother_handle"] = "Person",
        ["place"] = "Place"
    };

    public DatabaseBackupResult BackupDatabase(string? suffix = null, bool overwrite = false)
    {
        var sourcePath = ResolveDatabasePath();
        var backupPath = BuildBackupPath(sourcePath, suffix, overwrite);

        using var source = OpenConnection();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var destination = new SqliteConnection(destinationConnectionString);
        destination.Open();
        source.BackupDatabase(destination);

        var fileInfo = new FileInfo(backupPath);
        return new DatabaseBackupResult(sourcePath, backupPath, fileInfo.Length, DateTimeOffset.UtcNow);
    }

    public RecordUpdateResult MergePatchRecord(
        string objectType,
        string handle,
        JsonObject patch,
        long? expectedChange = null,
        bool updateChange = true,
        bool dryRun = false)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new ArgumentException("Handle is required.", nameof(handle));
        }

        var tableName = NormalizeObjectType(objectType);
        using var connection = OpenConnection(writable: !dryRun);
        if (!TableExists(connection, tableName))
        {
            throw new InvalidOperationException($"The {tableName} table does not exist in the configured database.");
        }

        using var transaction = dryRun ? null : connection.BeginTransaction();
        var existingJson = ReadJsonData(connection, transaction, tableName, handle);
        var record = ParseObject(existingJson, "Existing json_data is not a JSON object.");
        var oldChange = GetLong(record["change"]);

        if (expectedChange is not null && oldChange != expectedChange)
        {
            throw new InvalidOperationException($"The record change value is {oldChange?.ToString() ?? "null"}, not the expected {expectedChange}.");
        }

        var patchedRecord = CloneObject(record);
        ApplyMergePatch(patchedRecord, patch);
        ValidatePatchedRecord(tableName, handle, patchedRecord);
        var patchedModel = DeserializeRecord(tableName, patchedRecord);

        if (updateChange)
        {
            patchedRecord["change"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            patchedModel = DeserializeRecord(tableName, patchedRecord);
        }

        var tableColumns = GetTableColumns(connection, transaction, tableName);
        var materializedColumns = BuildMaterializedColumns(tableName, patchedModel, tableColumns);
        var references = ExtractReferences(tableName, handle, patchedRecord, patchedModel);
        var updatedColumns = new List<string> { "json_data" };
        updatedColumns.AddRange(materializedColumns.Keys);

        if (!dryRun)
        {
            UpdateRecord(connection, transaction, tableName, handle, patchedRecord, materializedColumns);
            RebuildReferences(connection, transaction, tableName, handle, patchedRecord, references);
            transaction!.Commit();
        }

        return new RecordUpdateResult(
            tableName,
            handle,
            dryRun,
            oldChange,
            GetLong(patchedRecord["change"]),
            updatedColumns,
            references.Count,
            patchedRecord);
    }

    public ImportMediaResult ImportMedia(
        string sourcePath,
        string? description = null,
        string? mime = null,
        string? fileName = null,
        string? personHandle = null,
        bool @private = false,
        bool dryRun = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        var resolvedSourcePath = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(resolvedSourcePath))
        {
            throw new FileNotFoundException("Source media file was not found.", resolvedSourcePath);
        }

        if ((File.GetAttributes(resolvedSourcePath) & FileAttributes.Directory) == FileAttributes.Directory)
        {
            throw new ArgumentException("Source path must be a file, not a directory.", nameof(sourcePath));
        }

        var sourceInfo = new FileInfo(resolvedSourcePath);
        if (sourceInfo.Length == 0)
        {
            throw new ArgumentException("Source media file cannot be empty.", nameof(sourcePath));
        }

        var destinationFileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(resolvedSourcePath) : fileName;
        var safeFileName = SanitizeFileName(destinationFileName);
        var resolvedDatabasePath = ResolveDatabasePath();
        var mediaPath = ResolveMediaPath(resolvedDatabasePath, safeFileName);
        var mediaHandle = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mediaMime = string.IsNullOrWhiteSpace(mime) ? GuessMimeType(safeFileName) : mime.Trim();
        JsonObject? updatedPersonRecord = null;
        var wroteFile = false;

        using var connection = OpenConnection(writable: !dryRun);
        if (!TableExists(connection, "media"))
        {
            throw new InvalidOperationException("The media table does not exist in the configured database.");
        }

        if (!TableExists(connection, "metadata"))
        {
            throw new InvalidOperationException("The metadata table does not exist in the configured database.");
        }

        if (!string.IsNullOrWhiteSpace(personHandle) && !TableExists(connection, "person"))
        {
            throw new InvalidOperationException("The person table does not exist in the configured database.");
        }

        using var transaction = dryRun ? null : connection.BeginTransaction();
        var mediaIndex = ReadMetadataIndex(connection, transaction, MediaIndexSetting);
        var grampsId = FormatMediaGrampsId(mediaIndex + 1);
        var mediaRecord = BuildMediaRecord(mediaHandle, grampsId, mediaPath.StoredPath, mediaMime, description, change, @private);

        if (!string.IsNullOrWhiteSpace(personHandle))
        {
            var personJson = ReadJsonData(connection, transaction, "person", personHandle.Trim());
            updatedPersonRecord = CloneObject(ParseObject(personJson, "Existing person json_data is not a JSON object."));
            var mediaList = EnsureArray(updatedPersonRecord, "media_list");
            mediaList.Add(BuildMediaRef(mediaHandle, @private));
            updatedPersonRecord["change"] = change;
            ValidatePatchedRecord("person", personHandle.Trim(), updatedPersonRecord);
            _ = DeserializeRecord("person", updatedPersonRecord);
        }

        if (!dryRun)
        {
            try
            {
                Directory.CreateDirectory(mediaPath.Directory);
                File.Copy(resolvedSourcePath, mediaPath.AbsolutePath);
                wroteFile = true;

                InsertRecord(connection, transaction, "media", mediaHandle, mediaRecord);
                UpdateMetadataIndex(connection, transaction, MediaIndexSetting, mediaIndex + 1);

                if (updatedPersonRecord is not null)
                {
                    var personModel = DeserializeRecord("person", updatedPersonRecord);
                    var personColumns = BuildMaterializedColumns("person", personModel, GetTableColumns(connection, transaction, "person"));
                    var personReferences = ExtractReferences("person", personHandle!.Trim(), updatedPersonRecord, personModel);
                    UpdateRecord(connection, transaction, "person", personHandle.Trim(), updatedPersonRecord, personColumns);
                    RebuildReferences(connection, transaction, "person", personHandle.Trim(), updatedPersonRecord, personReferences);
                }

                transaction!.Commit();
            }
            catch
            {
                if (wroteFile && File.Exists(mediaPath.AbsolutePath))
                {
                    File.Delete(mediaPath.AbsolutePath);
                }

                throw;
            }
        }

        return new ImportMediaResult(
            dryRun,
            mediaHandle,
            grampsId,
            resolvedSourcePath,
            mediaPath.AbsolutePath,
            mediaPath.StoredPath,
            mediaMime,
            string.IsNullOrWhiteSpace(personHandle) ? null : personHandle.Trim(),
            mediaRecord,
            updatedPersonRecord);
    }

    private static string BuildBackupPath(string sourcePath, string? suffix, bool overwrite)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? ".";
        var timestampSuffix = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var safeSuffix = string.IsNullOrWhiteSpace(suffix) ? timestampSuffix : suffix.Trim();

        if (safeSuffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || safeSuffix.Contains('/') || safeSuffix.Contains('\\'))
        {
            throw new ArgumentException("Backup suffix must be a file-name-safe value.", nameof(suffix));
        }

        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".db";
        }

        var backupPath = Path.Combine(directory, $"{stem}.{safeSuffix}.backup{extension}");
        if (Path.GetFullPath(backupPath).Equals(Path.GetFullPath(sourcePath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Backup path must be different from the source database path.");
        }

        if (File.Exists(backupPath))
        {
            if (!overwrite)
            {
                throw new IOException($"Backup file already exists: {backupPath}");
            }

            File.Delete(backupPath);
        }

        return backupPath;
    }

    private static string NormalizeObjectType(string objectType)
    {
        if (string.IsNullOrWhiteSpace(objectType))
        {
            throw new ArgumentException("Object type is required.", nameof(objectType));
        }

        var normalized = objectType.Trim().ToLowerInvariant();
        if (ObjectTables.TryGetValue(normalized, out var tableName))
        {
            return tableName;
        }

        throw new ArgumentException("Object type must be one of: person, family, event, place, source, citation, media, repository, note, tag.", nameof(objectType));
    }

    private static string ReadJsonData(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string handle)
    {
        using var command = CreateCommand(connection, transaction);
        command.CommandText = $"select json_data from {QuoteIdentifier(tableName)} where handle = $handle limit 1";
        command.Parameters.AddWithValue("$handle", handle);

        var jsonData = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            throw new InvalidOperationException($"No {tableName} record exists with handle {handle}.");
        }

        return jsonData;
    }

    private static JsonObject ParseObject(string json, string errorMessage)
    {
        return JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException(errorMessage);
    }

    private static JsonObject CloneObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString(JsonOptions)) as JsonObject
            ?? throw new InvalidOperationException("JSON object clone failed.");
    }

    private static JsonNode? CloneNode(JsonNode? source)
    {
        return source is null ? null : JsonNode.Parse(source.ToJsonString(JsonOptions));
    }

    private static void ApplyMergePatch(JsonObject target, JsonObject patch)
    {
        foreach (var property in patch)
        {
            if (property.Value is null)
            {
                target.Remove(property.Key);
                continue;
            }

            if (property.Value is JsonObject patchObject)
            {
                var targetObject = target.TryGetPropertyValue(property.Key, out var existingValue) && existingValue is JsonObject existingObject
                    ? existingObject
                    : [];

                ApplyMergePatch(targetObject, patchObject);
                target[property.Key] = targetObject;
                continue;
            }

            target[property.Key] = CloneNode(property.Value);
        }
    }

    private static void ValidatePatchedRecord(string tableName, string handle, JsonObject record)
    {
        var patchedHandle = GetString(record["handle"]);
        if (!handle.Equals(patchedHandle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Patch cannot change or remove the record handle.");
        }

        if (record.TryGetPropertyValue("_class", out var classNode) && GetString(classNode) is { } className)
        {
            var expectedClass = TableClasses[tableName];
            if (!expectedClass.Equals(className, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Patch cannot change _class from {expectedClass} to {className}.");
            }
        }
    }

    private static object DeserializeRecord(string tableName, JsonObject record)
    {
        var json = record.ToJsonString(JsonOptions);
        return tableName switch
        {
            "person" => DeserializePatched<Person>(json, tableName),
            "family" => DeserializePatched<Family>(json, tableName),
            "event" => DeserializePatched<Event>(json, tableName),
            "place" => DeserializePatched<Place>(json, tableName),
            "source" => DeserializePatched<Source>(json, tableName),
            "citation" => DeserializePatched<Citation>(json, tableName),
            "media" => DeserializePatched<GrampsDbTool.Models.Media>(json, tableName),
            "repository" => DeserializePatched<Repository>(json, tableName),
            "note" => DeserializePatched<Note>(json, tableName),
            "tag" => DeserializePatched<Tag>(json, tableName),
            _ => throw new ArgumentException($"Unsupported object type: {tableName}.", nameof(tableName))
        };
    }

    private static T DeserializePatched<T>(string json, string tableName)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Patched {tableName} json_data could not be deserialized as {typeof(T).Name}.");
    }

    private static IReadOnlyDictionary<string, object?> BuildMaterializedColumns(
        string tableName,
        object record,
        IReadOnlySet<string> tableColumns)
    {
        var columns = new Dictionary<string, object?>(StringComparer.Ordinal);

        void Set(string columnName, object? value)
        {
            if (tableColumns.Contains(columnName) && columnName != "handle" && columnName != "json_data")
            {
                columns[columnName] = value;
            }
        }

        if (record is GrampsObject grampsObject)
        {
            Set("gramps_id", grampsObject.GrampsId);
            Set("change", grampsObject.Change);
            Set("private", grampsObject.Private);
        }
        else if (record is Tag tag)
        {
            Set("change", tag.Change);
        }

        switch (record)
        {
            case Person person:
                Set("given_name", person.PrimaryName?.FirstName);
                Set("surname", GetPrimarySurname(person.PrimaryName));
                Set("gender", person.Gender);
                Set("birth_ref_index", person.BirthRefIndex);
                Set("death_ref_index", person.DeathRefIndex);
                break;
            case Family family:
                Set("father_handle", family.FatherHandle);
                Set("mother_handle", family.MotherHandle);
                break;
            case Event eventObject:
                Set("type", TypeValue(eventObject.Type));
                Set("date", DateValue(eventObject.Date));
                Set("description", eventObject.Description);
                Set("place", eventObject.PlaceHandle);
                break;
            case Place place:
                Set("title", place.Title);
                Set("long", place.Longitude);
                Set("lat", place.Latitude);
                Set("code", place.Code);
                Set("enclosed_by", FirstObjectRef(place.PlaceRefs));
                break;
            case Source source:
                Set("title", source.Title);
                Set("author", source.Author);
                Set("pubinfo", source.PublicationInfo);
                Set("abbrev", source.Abbreviation);
                break;
            case Citation citation:
                Set("date", DateValue(citation.Date));
                Set("page", citation.Page);
                Set("confidence", citation.Confidence);
                Set("source_handle", citation.SourceHandle);
                break;
            case GrampsDbTool.Models.Media media:
                Set("path", media.Path);
                Set("mime", media.Mime);
                Set("desc", media.Description);
                Set("checksum", media.Checksum);
                Set("date", DateValue(media.Date));
                break;
            case Repository repository:
                Set("type", TypeValue(repository.Type));
                Set("name", repository.Name);
                break;
            case Note note:
                Set("text", StyledTextValue(note.Text));
                Set("format", note.Format);
                Set("type", TypeValue(note.Type));
                break;
            case Tag tag:
                Set("name", tag.Name);
                Set("color", tag.Color);
                Set("priority", tag.Priority);
                break;
        }

        return columns;
    }

    private static void UpdateRecord(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        string handle,
        JsonObject record,
        IReadOnlyDictionary<string, object?> materializedColumns)
    {
        using var command = CreateCommand(connection, transaction);
        var assignments = new List<string> { "json_data = $json_data" };
        command.Parameters.AddWithValue("$json_data", record.ToJsonString(JsonOptions));

        var index = 0;
        foreach (var column in materializedColumns)
        {
            var parameterName = $"$p{index++}";
            assignments.Add($"{QuoteIdentifier(column.Key)} = {parameterName}");
            command.Parameters.AddWithValue(parameterName, column.Value ?? DBNull.Value);
        }

        command.CommandText = $"update {QuoteIdentifier(tableName)} set {string.Join(", ", assignments)} where handle = $handle";
        command.Parameters.AddWithValue("$handle", handle);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"No {tableName} record was updated for handle {handle}.");
        }
    }

    private static void InsertRecord(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string handle, JsonObject record)
    {
        var model = DeserializeRecord(tableName, record);
        var tableColumns = GetTableColumns(connection, transaction, tableName);
        var materializedColumns = BuildMaterializedColumns(tableName, model, tableColumns);

        using var command = CreateCommand(connection, transaction);
        var columns = new List<string> { "handle", "json_data" };
        var parameters = new List<string> { "$handle", "$json_data" };
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json_data", record.ToJsonString(JsonOptions));

        var index = 0;
        foreach (var column in materializedColumns)
        {
            var parameterName = $"$p{index++}";
            columns.Add(column.Key);
            parameters.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, column.Value ?? DBNull.Value);
        }

        command.CommandText = $"insert into {QuoteIdentifier(tableName)} ({string.Join(", ", columns.Select(QuoteIdentifier))}) values ({string.Join(", ", parameters)})";
        command.ExecuteNonQuery();
    }

    private static JsonArray EnsureArray(JsonObject record, string propertyName)
    {
        if (record[propertyName] is JsonArray array)
        {
            return array;
        }

        array = [];
        record[propertyName] = array;
        return array;
    }

    private static JsonObject BuildMediaRef(string mediaHandle, bool @private) => new()
    {
        ["attribute_list"] = new JsonArray(),
        ["_class"] = "MediaRef",
        ["private"] = @private,
        ["citation_list"] = new JsonArray(),
        ["note_list"] = new JsonArray(),
        ["ref"] = mediaHandle
    };

    private static JsonObject BuildMediaRecord(string handle, string grampsId, string path, string mime, string? description, long change, bool @private) => new()
    {
        ["_class"] = "Media",
        ["handle"] = handle,
        ["gramps_id"] = grampsId,
        ["path"] = path,
        ["mime"] = mime,
        ["desc"] = description ?? string.Empty,
        ["checksum"] = string.Empty,
        ["attribute_list"] = new JsonArray(),
        ["citation_list"] = new JsonArray(),
        ["note_list"] = new JsonArray(),
        ["change"] = change,
        ["date"] = new JsonObject
        {
            ["_class"] = "Date",
            ["calendar"] = 0,
            ["modifier"] = 0,
            ["quality"] = 0,
            ["dateval"] = new JsonArray(0, 0, 0, false),
            ["text"] = string.Empty,
            ["sortval"] = 0,
            ["newyear"] = 0
        },
        ["tag_list"] = new JsonArray(),
        ["private"] = @private
    };

    private MediaPath ResolveMediaPath(string databasePath, string safeFileName)
    {
        var databaseDirectory = Path.GetDirectoryName(databasePath) ?? ".";
        var configuredDirectory = string.IsNullOrWhiteSpace(mediaDirectory) ? null : mediaDirectory.Trim();
        var directory = configuredDirectory is null ? Path.Combine(databaseDirectory, "media") : configuredDirectory;
        var storeRelativeToDatabase = configuredDirectory is null;
        var absoluteDirectory = Path.GetFullPath(directory, databaseDirectory);
        var absolutePath = GetAvailableMediaPath(absoluteDirectory, safeFileName);
        var storedPath = storeRelativeToDatabase
            ? Path.Join("media", Path.GetFileName(absolutePath)).Replace(Path.DirectorySeparatorChar, '/')
            : absolutePath;

        return new MediaPath(absoluteDirectory, absolutePath, storedPath);
    }

    private static string GetAvailableMediaPath(string directory, string safeFileName)
    {
        var candidate = Path.Combine(directory, safeFileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        for (var index = 1; index < 10000; index++)
        {
            candidate = Path.Combine(directory, $"{stem}-{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not choose a non-conflicting media file path.");
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (!name.Equals(fileName.Trim(), StringComparison.Ordinal) || name is "." or "..")
        {
            throw new ArgumentException("File name must not include a directory path.", nameof(fileName));
        }

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '_');
        }

        name = Regex.Replace(name, "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("File name must contain at least one valid character.", nameof(fileName));
        }

        return name;
    }

    private static string GuessMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static int ReadMetadataIndex(SqliteConnection connection, SqliteTransaction? transaction, string setting)
    {
        using var command = CreateCommand(connection, transaction);
        command.CommandText = "select json_data from metadata where setting = $setting limit 1";
        command.Parameters.AddWithValue("$setting", setting);
        var jsonData = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return 0;
        }

        var node = JsonNode.Parse(jsonData) as JsonObject;
        return GetLong(node?["value"]) is { } value ? Convert.ToInt32(value) : 0;
    }

    private static void UpdateMetadataIndex(SqliteConnection connection, SqliteTransaction? transaction, string setting, int value)
    {
        using var command = CreateCommand(connection, transaction);
        command.CommandText = "update metadata set json_data = $json_data where setting = $setting";
        command.Parameters.AddWithValue("$setting", setting);
        command.Parameters.AddWithValue("$json_data", new JsonObject
        {
            ["type"] = "int",
            ["value"] = value
        }.ToJsonString(JsonOptions));
        command.ExecuteNonQuery();
    }

    private static string FormatMediaGrampsId(int index) => $"O{index.ToString("D4", CultureInfo.InvariantCulture)}";

    private static int RebuildReferences(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        string handle,
        JsonObject record,
        IReadOnlyList<RecordReference> references)
    {
        if (!TableExists(connection, "reference"))
        {
            return 0;
        }

        var objectClass = GetString(record["_class"]) ?? TableClasses[tableName];
        using (var deleteCommand = CreateCommand(connection, transaction))
        {
            deleteCommand.CommandText = "delete from reference where obj_handle = $handle and obj_class = $class";
            deleteCommand.Parameters.AddWithValue("$handle", handle);
            deleteCommand.Parameters.AddWithValue("$class", objectClass);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var reference in references)
        {
            using var insertCommand = CreateCommand(connection, transaction);
            insertCommand.CommandText = """
                insert into reference (obj_handle, obj_class, ref_handle, ref_class)
                values ($obj_handle, $obj_class, $ref_handle, $ref_class)
                """;
            insertCommand.Parameters.AddWithValue("$obj_handle", handle);
            insertCommand.Parameters.AddWithValue("$obj_class", objectClass);
            insertCommand.Parameters.AddWithValue("$ref_handle", reference.Handle);
            insertCommand.Parameters.AddWithValue("$ref_class", reference.Class);
            insertCommand.ExecuteNonQuery();
        }

        return references.Count;
    }

    private static IReadOnlySet<string> GetTableColumns(SqliteConnection connection, SqliteTransaction? transaction, string tableName)
    {
        using var command = CreateCommand(connection, transaction);
        command.CommandText = $"pragma table_info({QuoteIdentifier(tableName)})";
        using var reader = command.ExecuteReader(CommandBehavior.SingleResult);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static IReadOnlyList<RecordReference> ExtractReferences(string tableName, string handle, JsonObject record, object model)
    {
        var references = new Dictionary<string, RecordReference>(StringComparer.Ordinal);

        void Add(string referenceClass, string? referenceHandle)
        {
            if (string.IsNullOrWhiteSpace(referenceHandle))
            {
                return;
            }

            var key = $"{referenceClass}\u001f{referenceHandle}";
            references[key] = new RecordReference(referenceClass, referenceHandle);
        }

        void AddFromValue(string referenceClass, JsonNode? value)
        {
            switch (value)
            {
                case JsonValue:
                    Add(referenceClass, GetString(value));
                    break;
                case JsonObject referenceObject:
                    Add(referenceClass, GetString(referenceObject["ref"]));
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        AddFromValue(referenceClass, item);
                    }
                    break;
            }
        }

        void Visit(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (var property in obj)
                {
                    if (ReferenceProperties.TryGetValue(property.Key, out var referenceClass))
                    {
                        AddFromValue(referenceClass, property.Value);
                    }

                    Visit(property.Value);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    Visit(item);
                }
            }
        }

        AddModelReferences(model, Add);
        Visit(record);
        return references.Values
            .Where(reference => !reference.Class.Equals(TableClasses[tableName], StringComparison.Ordinal) || !reference.Handle.Equals(handle, StringComparison.Ordinal))
            .ToList();
    }

    private static void AddModelReferences(object model, Action<string, string?> add)
    {
        if (model is GrampsObject grampsObject)
        {
            foreach (var tagHandle in grampsObject.TagHandles)
            {
                add("Tag", tagHandle);
            }
        }

        switch (model)
        {
            case Person person:
                AddReferences("Event", person.EventRefs, add);
                AddHandles("Family", person.FamilyHandles, add);
                AddHandles("Family", person.ParentFamilyHandles, add);
                AddReferences("Media", person.MediaRefs, add);
                AddHandles("Citation", person.CitationHandles, add);
                AddHandles("Note", person.NoteHandles, add);
                AddReferences("Person", person.PersonRefs, add);
                break;
            case Family family:
                add("Person", family.FatherHandle);
                add("Person", family.MotherHandle);
                AddReferences("Person", family.ChildRefs, add);
                AddReferences("Event", family.EventRefs, add);
                AddReferences("Media", family.MediaRefs, add);
                AddHandles("Citation", family.CitationHandles, add);
                AddHandles("Note", family.NoteHandles, add);
                break;
            case Event eventObject:
                add("Place", eventObject.PlaceHandle);
                AddHandles("Citation", eventObject.CitationHandles, add);
                AddHandles("Note", eventObject.NoteHandles, add);
                AddReferences("Media", eventObject.MediaRefs, add);
                break;
            case Place place:
                AddReferences("Place", place.PlaceRefs, add);
                AddHandles("Citation", place.CitationHandles, add);
                AddHandles("Note", place.NoteHandles, add);
                break;
            case Source source:
                AddReferences("Repository", source.RepositoryRefs, add);
                AddReferences("Media", source.MediaRefs, add);
                AddHandles("Note", source.NoteHandles, add);
                break;
            case Citation citation:
                add("Source", citation.SourceHandle);
                AddReferences("Media", citation.MediaRefs, add);
                AddHandles("Note", citation.NoteHandles, add);
                break;
            case Note:
            case GrampsDbTool.Models.Media:
            case Repository:
            case Tag:
                break;
        }
    }

    private static void AddHandles(string referenceClass, IEnumerable<string> handles, Action<string, string?> add)
    {
        foreach (var handle in handles)
        {
            add(referenceClass, handle);
        }
    }

    private static void AddReferences(string referenceClass, IEnumerable<ReferenceBase> references, Action<string, string?> add)
    {
        foreach (var reference in references)
        {
            add(referenceClass, reference.Ref);
        }
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var command = connection.CreateCommand();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    private static object? Scalar(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        if (value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
        }

        return value.TryGetValue<string>(out var stringValue) ? stringValue : null;
    }

    private static string? GetString(JsonNode? node) => Scalar(node)?.ToString();

    private static long? GetLong(JsonNode? node)
    {
        var value = Scalar(node);
        return value switch
        {
            int intValue => intValue,
            long longValue => longValue,
            double doubleValue => Convert.ToInt64(doubleValue),
            string stringValue when long.TryParse(stringValue, out var parsed) => parsed,
            _ => null
        };
    }

    private static object? TypeValue(JsonNode? node)
    {
        if (Scalar(node) is { } scalar)
        {
            return scalar;
        }

        if (node is not JsonObject typeObject)
        {
            return null;
        }

        return Scalar(typeObject["value"]) ?? Scalar(typeObject["string"]);
    }

    private static object? TypeValue(GrampsType? type) => type is null ? null : type.Value;

    private static object? DateValue(JsonNode? node)
    {
        if (Scalar(node) is { } scalar)
        {
            return scalar;
        }

        if (node is not JsonObject dateObject)
        {
            return null;
        }

        return Scalar(dateObject["sortval"]) ?? Scalar(dateObject["text"]);
    }

    private static object? DateValue(GrampsDate? date) => date is null ? null : date.SortValue;

    private static object? StyledTextValue(JsonNode? node)
    {
        if (Scalar(node) is { } scalar)
        {
            return scalar;
        }

        return node is JsonObject textObject ? Scalar(textObject["string"]) : null;
    }

    private static object? StyledTextValue(StyledText? text) => text?.String;

    private static string? GetPrimarySurname(Name? primaryName)
    {
        if (primaryName is null || primaryName.Surnames.Count == 0)
        {
            return null;
        }

        return primaryName.Surnames.FirstOrDefault(surname => surname.Primary)?.Value
            ?? primaryName.Surnames.Select(surname => surname.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetPrimarySurname(JsonObject? primaryName)
    {
        if (primaryName?["surname_list"] is not JsonArray surnames || surnames.Count == 0)
        {
            return null;
        }

        foreach (var surnameNode in surnames.OfType<JsonObject>())
        {
            if (Scalar(surnameNode["primary"]) is true)
            {
                return GetString(surnameNode["surname"]);
            }
        }

        return surnames.OfType<JsonObject>().Select(surname => GetString(surname["surname"])).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? FirstObjectRef(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return null;
        }

        foreach (var item in array.OfType<JsonObject>())
        {
            if (GetString(item["ref"]) is { Length: > 0 } reference)
            {
                return reference;
            }
        }

        return null;
    }

    private static string? FirstObjectRef(IEnumerable<ReferenceBase> references)
    {
        return references.Select(reference => reference.Ref).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private sealed record RecordReference(string Class, string Handle);

    private sealed record MediaPath(string Directory, string AbsolutePath, string StoredPath);
}
