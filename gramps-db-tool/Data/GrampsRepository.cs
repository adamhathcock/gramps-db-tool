using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Models;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsRepository(GrampsConfig config, IMediaPathService mediaPathService)
{
    public async Task<IReadOnlyList<PersonSearchResultDto>> SearchPeopleAsync(string query, int limit = 20,
        int offset = 0, CancellationToken cancellationToken = default)
    {
        query = (query ?? string.Empty).Trim();
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(offset, 0);

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT json_data
                              FROM person
                              WHERE $query = ''
                                 OR given_name LIKE $like
                                 OR surname LIKE $like
                                 OR gramps_id LIKE $like
                              ORDER BY surname, given_name, gramps_id
                              LIMIT $limit
                              OFFSET $offset
                              """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$like", $"%{query}%");
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);

        var people = new List<PersonSearchResultDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var document = JsonDocument.Parse(reader.GetString(0));
            var person = document.RootElement;
            people.Add(new PersonSearchResultDto(
                RequiredString(person, "handle"),
                JsonMapping.String(person, "gramps_id"),
                JsonMapping.DisplayName(person),
                JsonMapping.Int(person, "gender")));
        }

        return people;
    }

    public async Task<PersonDto?> GetPersonAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var person = await GetObjectAsync("person", handle, grampsId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(person);
        var root = document.RootElement;
        return new PersonDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.DisplayName(root),
            JsonMapping.Int(root, "gender"),
            JsonMapping.RefArray(root, "event_ref_list"),
            JsonMapping.StringArray(root, "family_list"),
            JsonMapping.StringArray(root, "parent_family_list"),
            JsonMapping.RefArray(root, "media_list"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    public async Task<IReadOnlyList<MediaDto>> GetMediaAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("media", "media", handles, grampsIds, MapMedia, cancellationToken);
    }

    public async Task<MediaDto?> GetMediaAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var media = await GetObjectAsync("media", handle, grampsId, cancellationToken);
        if (media is null)
        {
            return null;
        }

        return MapMedia(media);
    }

    private MediaDto MapMedia(string media)
    {
        using var document = JsonDocument.Parse(media);
        var root = document.RootElement;
        var path = JsonMapping.String(root, "path");

        return new MediaDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            path,
            string.IsNullOrWhiteSpace(path) ? null : mediaPathService.ResolvePath(path),
            JsonMapping.String(root, "mime"),
            JsonMapping.String(root, "desc"),
            JsonMapping.String(root, "checksum"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    public async Task<NoteDto?> GetNoteAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var notes = await GetNoteAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return notes.FirstOrDefault();
    }

    public async Task<IReadOnlyList<NoteDto>> GetNoteAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("note", "note", handles, grampsIds, MapNote, cancellationToken);
    }

    public async Task<CitationDto?> GetCitationAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var citations = await GetCitationAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return citations.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CitationDto>> GetCitationAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("citation", "citation", handles, grampsIds, MapCitation, cancellationToken);
    }

    public async Task<FamilyDto?> GetFamilyAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var family = await GetObjectAsync("family", handle, grampsId, cancellationToken);
        if (family is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(family);
        var root = document.RootElement;
        return new FamilyDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "father_handle"),
            JsonMapping.String(root, "mother_handle"),
            JsonMapping.GrampsTypeName(root, "type"),
            JsonMapping.ChildRefArray(root, "child_ref_list"),
            JsonMapping.RefArray(root, "event_ref_list"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    public async Task<EventDto?> GetEventAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var events = await GetEventAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return events.FirstOrDefault();
    }

    public async Task<IReadOnlyList<EventDto>> GetEventAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("event", "event", handles, grampsIds, MapEvent, cancellationToken);
    }

    public async Task<SourceDto?> GetSourceAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var sources = await GetSourceAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return sources.FirstOrDefault();
    }

    public async Task<IReadOnlyList<SourceDto>> GetSourceAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("source", "source", handles, grampsIds, MapSource, cancellationToken);
    }

    public async Task<IReadOnlyList<TagDto>> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json_data FROM tag ORDER BY priority, name";

        var tags = new List<TagDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(MapTag(reader.GetString(0)));
        }

        return tags;
    }

    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default)
    {
        var hasHandles = handles is not null;
        var hasNames = names is not null;
        if (hasHandles == hasNames)
        {
            throw new ArgumentException("Supply either tag handles or names, but not both.");
        }

        var lookupValues = hasHandles ? handles! : names!;
        var lookupName = hasHandles ? "handles" : "names";
        var columnName = hasHandles ? "handle" : "name";

        if (lookupValues.Count == 0)
        {
            throw new ArgumentException($"At least one tag {lookupName} value is required.");
        }

        if (lookupValues.Count > 100)
        {
            throw new ArgumentException($"At most 100 tag {lookupName} values may be requested.");
        }

        var requestedValues = lookupValues.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (requestedValues.Length != lookupValues.Count)
        {
            throw new ArgumentException($"Tag {lookupName} values must not be empty.");
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var parameters = requestedValues.Distinct()
            .Select((value, index) => new { Value = value, Parameter = $"$value{index}" }).ToArray();
        command.CommandText =
            $"SELECT json_data, {columnName} FROM tag WHERE {columnName} IN ({string.Join(", ", parameters.Select(static parameter => parameter.Parameter))})";
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Parameter, parameter.Value);
        }

        var tagsByLookupValue = new Dictionary<string, TagDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tagsByLookupValue[reader.GetString(1)] = MapTag(reader.GetString(0));
        }

        var tags = new List<TagDto>();
        foreach (var value in requestedValues)
        {
            if (tagsByLookupValue.TryGetValue(value, out var tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    public async Task<IReadOnlyList<TaggedObjectDto>> FindObjectsByTagAsync(
        string? tagHandle,
        string? tagName,
        IReadOnlyList<string>? objectTypes = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagHandle) == string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Supply either tagHandle or tagName, but not both.");
        }

        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(offset, 0);

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        var resolvedTagHandle = string.IsNullOrWhiteSpace(tagHandle)
            ? await ReadTagHandleByNameAsync(connection, tagName!, cancellationToken)
            : tagHandle;
        if (string.IsNullOrWhiteSpace(resolvedTagHandle))
        {
            return [];
        }

        var tables = TaggedObjectTables;
        if (objectTypes is not null)
        {
            var requestedTypes = objectTypes.Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requestedTypes.Count != objectTypes.Count)
            {
                throw new ArgumentException("Object types must not be empty.");
            }

            var unknownTypes = requestedTypes.Except(TaggedObjectTables.Select(static table => table.ObjectType),
                StringComparer.OrdinalIgnoreCase).ToArray();
            if (unknownTypes.Length > 0)
            {
                throw new ArgumentException($"Unsupported tagged object type: {string.Join(", ", unknownTypes)}.");
            }

            tables = TaggedObjectTables.Where(table => requestedTypes.Contains(table.ObjectType)).ToArray();
        }

        if (tables.Length == 0)
        {
            return [];
        }

        var unionSql = string.Join(" UNION ALL ", tables.Select(static table => $"""
             SELECT '{table.ObjectType}' AS object_type,
                    handle,
                    {table.GrampsIdExpression} AS gramps_id,
                    {table.LabelExpression} AS label,
                    json_data
             FROM {table.TableName}
             WHERE EXISTS (SELECT 1 FROM json_each(json_data, '$.tag_list') WHERE value = $tagHandle)
             """));

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                               SELECT object_type, handle, gramps_id, label, json_data
                               FROM ({unionSql})
                               ORDER BY object_type, label, gramps_id, handle
                               LIMIT $limit OFFSET $offset
                               """;
        command.Parameters.AddWithValue("$tagHandle", resolvedTagHandle);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);

        var objects = new List<TaggedObjectDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var document = JsonDocument.Parse(reader.GetString(4));
            objects.Add(new TaggedObjectDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) || string.IsNullOrWhiteSpace(reader.GetString(3))
                    ? reader.GetString(1)
                    : reader.GetString(3),
                JsonMapping.StringArray(document.RootElement, "tag_list")));
        }

        return objects;
    }

    private async Task<string?> GetObjectAsync(string tableName, string? handle, string? grampsId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(handle) && string.IsNullOrWhiteSpace(grampsId))
        {
            throw new ArgumentException("Either handle or grampsId is required.");
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(handle)
            ? $"SELECT json_data FROM {tableName} WHERE gramps_id = $grampsId LIMIT 1"
            : $"SELECT json_data FROM {tableName} WHERE handle = $handle LIMIT 1";

        if (string.IsNullOrWhiteSpace(handle))
        {
            command.Parameters.AddWithValue("$grampsId", grampsId);
        }
        else
        {
            command.Parameters.AddWithValue("$handle", handle);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private async Task<IReadOnlyList<T>> GetObjectsAsync<T>(
        string objectName,
        string tableName,
        IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds,
        Func<string, T> mapObject,
        CancellationToken cancellationToken)
    {
        var hasHandles = handles is not null;
        var hasGrampsIds = grampsIds is not null;
        if (hasHandles == hasGrampsIds)
        {
            throw new ArgumentException($"Supply either {objectName} handles or Gramps IDs, but not both.");
        }

        var lookupValues = hasHandles ? handles! : grampsIds!;
        var lookupName = hasHandles ? "handles" : "grampsIds";
        var columnName = hasHandles ? "handle" : "gramps_id";

        if (lookupValues.Count == 0)
        {
            throw new ArgumentException($"At least one {objectName} {lookupName} value is required.");
        }

        if (lookupValues.Count > 100)
        {
            throw new ArgumentException($"At most 100 {objectName} {lookupName} values may be requested.");
        }

        var requestedValues = lookupValues.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (requestedValues.Length != lookupValues.Count)
        {
            throw new ArgumentException($"{ToSentenceCase(objectName)} {lookupName} values must not be empty.");
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var parameters = requestedValues.Distinct()
            .Select((value, index) => new { Value = value, Parameter = $"$value{index}" }).ToArray();
        command.CommandText =
            $"SELECT json_data, {columnName} FROM {tableName} WHERE {columnName} IN ({string.Join(", ", parameters.Select(static parameter => parameter.Parameter))})";
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Parameter, parameter.Value);
        }

        var objectsByLookupValue = new Dictionary<string, T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            objectsByLookupValue[reader.GetString(1)] = mapObject(reader.GetString(0));
        }

        var objects = new List<T>();
        foreach (var value in requestedValues)
        {
            if (objectsByLookupValue.TryGetValue(value, out var obj))
            {
                objects.Add(obj);
            }
        }

        return objects;
    }

    private static IReadOnlyList<string>? ToLookupList(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : [value];
    }

    private static string ToSentenceCase(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static NoteDto MapNote(string note)
    {
        using var document = JsonDocument.Parse(note);
        var root = document.RootElement;
        return new NoteDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.NoteText(root),
            JsonMapping.Int(root, "format"),
            JsonMapping.GrampsTypeName(root, "type"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    private static CitationDto MapCitation(string citation)
    {
        using var document = JsonDocument.Parse(citation);
        var root = document.RootElement;
        return new CitationDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "page"),
            JsonMapping.Int(root, "confidence"),
            JsonMapping.String(root, "source_handle"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.RefArray(root, "media_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    private static EventDto MapEvent(string @event)
    {
        using var document = JsonDocument.Parse(@event);
        var root = document.RootElement;
        return new EventDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.GrampsTypeName(root, "type"),
            JsonMapping.String(root, "description"),
            JsonMapping.String(root, "place"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.RefArray(root, "media_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    private static SourceDto MapSource(string source)
    {
        using var document = JsonDocument.Parse(source);
        var root = document.RootElement;
        return new SourceDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "title"),
            JsonMapping.String(root, "author"),
            JsonMapping.String(root, "pubinfo"),
            JsonMapping.String(root, "abbrev"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.RefArray(root, "media_list"),
            JsonMapping.StringArray(root, "tag_list"));
    }

    private static TagDto MapTag(string tag)
    {
        using var document = JsonDocument.Parse(tag);
        var root = document.RootElement;
        return new TagDto(
            RequiredString(root, "handle"),
            RequiredString(root, "name"),
            JsonMapping.String(root, "color"),
            JsonMapping.Int(root, "priority"),
            JsonMapping.Long(root, "change"));
    }

    private static async Task<string?> ReadTagHandleByNameAsync(SqliteConnection connection, string tagName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT handle FROM tag WHERE name = $name LIMIT 1";
        command.Parameters.AddWithValue("$name", tagName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static readonly TaggedObjectTable[] TaggedObjectTables =
    [
        new("person", "person", "gramps_id", "trim(COALESCE(given_name, '') || ' ' || COALESCE(surname, ''))"),
        new("family", "family", "gramps_id", "COALESCE(gramps_id, handle)"),
        new("event", "event", "gramps_id", "COALESCE(description, gramps_id, handle)"),
        new("place", "place", "gramps_id", "COALESCE(title, gramps_id, handle)"),
        new("source", "source", "gramps_id", "COALESCE(title, gramps_id, handle)"),
        new("citation", "citation", "gramps_id", "COALESCE(page, gramps_id, handle)"),
        new("media", "media", "gramps_id", "COALESCE(desc, path, gramps_id, handle)"),
        new("repository", "repository", "gramps_id", "COALESCE(name, gramps_id, handle)"),
        new("note", "note", "gramps_id", "COALESCE(gramps_id, handle)")
    ];

    private sealed record TaggedObjectTable(
        string ObjectType,
        string TableName,
        string GrampsIdExpression,
        string LabelExpression);

    private string CreateConnectionString()
    {
        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}",
                config.DatabasePath);
        }

        return new SqliteConnectionStringBuilder
        {
            DataSource = config.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = JsonMapping.String(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Gramps object is missing required string field '{propertyName}'.");
        }

        return value;
    }
}