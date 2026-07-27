using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Models;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsRepository(GrampsConfig config, IMediaPathService mediaPathService)
{
    public async Task<PageDto<PersonSearchResultDto>> SearchPeopleAsync(string query, int limit = 20,
        int offset = 0, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        ValidatePaging(limit, offset);

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = """
                                   SELECT COUNT(*)
                                   FROM person
                                   WHERE $query = ''
                                      OR given_name LIKE $like
                                      OR surname LIKE $like
                                      OR gramps_id LIKE $like
                                   """;
        countCommand.Parameters.AddWithValue("$query", query);
        countCommand.Parameters.AddWithValue("$like", $"%{query}%");
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
                              SELECT json_data
                              FROM person
                              WHERE $query = ''
                                 OR given_name LIKE $like
                                 OR surname LIKE $like
                                 OR gramps_id LIKE $like
                              ORDER BY surname, given_name, gramps_id, handle
                              LIMIT $limit
                              OFFSET $offset
                              """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$like", $"%{query}%");
        command.Parameters.AddWithValue("$limit", limit + 1);
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
                JsonMapping.Int(person, "gender"),
                JsonMapping.Long(person, "change"),
                JsonMapping.Bool(person, "private")));
        }

        return CreatePage(people, limit, offset, totalCount);
    }

    public async Task<PageDto<ObjectSummaryDto>> ListObjectsAsync(string query = "",
        IReadOnlyList<string>? objectTypes = null, int limit = 100, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        ValidatePaging(limit, offset);
        var tables = ResolveObjectTables(objectTypes);
        if (tables.Length == 0)
        {
            return new PageDto<ObjectSummaryDto>([], limit, offset, 0, 0, false, null);
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var unionSql = BuildObjectUnionSql(tables);
        var filterSql = """
                        WHERE $query = ''
                           OR handle LIKE $like
                           OR COALESCE(gramps_id, '') LIKE $like
                           OR COALESCE(label, '') LIKE $like
                        """;

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = $"SELECT COUNT(*) FROM ({unionSql}) {filterSql}";
        AddSearchParameters(countCommand, query);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"""
                               SELECT object_type, handle, gramps_id, label, change, private
                               FROM ({unionSql})
                               {filterSql}
                               ORDER BY object_type, label COLLATE NOCASE, gramps_id, handle
                               LIMIT $limit OFFSET $offset
                               """;
        AddSearchParameters(command, query);
        command.Parameters.AddWithValue("$limit", limit + 1);
        command.Parameters.AddWithValue("$offset", offset);

        var objects = new List<ObjectSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            objects.Add(new ObjectSummaryDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) || string.IsNullOrWhiteSpace(reader.GetString(3))
                    ? reader.GetString(1)
                    : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5) != 0));
        }

        return CreatePage(objects, limit, offset, totalCount);
    }

    public async Task<PageDto<BacklinkDto>> FindBacklinksAsync(string handle,
        IReadOnlyList<string>? objectTypes = null, int limit = 100, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new ArgumentException("Referenced handle is required.");
        }

        ValidatePaging(limit, offset);
        var tables = ResolveObjectTables(objectTypes);
        if (tables.Length == 0)
        {
            return new PageDto<BacklinkDto>([], limit, offset, 0, 0, false, null);
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var unionSql = BuildObjectUnionSql(tables);
        var sourceSql = $"""
                         SELECT DISTINCT summary.object_type, summary.handle, summary.gramps_id, summary.label
                         FROM reference
                         JOIN ({unionSql}) AS summary
                           ON summary.object_type = lower(reference.obj_class)
                          AND summary.handle = reference.obj_handle
                         WHERE reference.ref_handle = $handle
                         """;

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = $"SELECT COUNT(*) FROM ({sourceSql})";
        countCommand.Parameters.AddWithValue("$handle", handle);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"""
                               SELECT object_type, handle, gramps_id, label
                               FROM ({sourceSql})
                               ORDER BY object_type, label COLLATE NOCASE, gramps_id, handle
                               LIMIT $limit OFFSET $offset
                               """;
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$limit", limit + 1);
        command.Parameters.AddWithValue("$offset", offset);

        var backlinks = new List<BacklinkDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            backlinks.Add(new BacklinkDto(reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) || string.IsNullOrWhiteSpace(reader.GetString(3))
                    ? reader.GetString(1)
                    : reader.GetString(3)));
        }

        return CreatePage(backlinks, limit, offset, totalCount);
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
            JsonMapping.Int(root, "birth_ref_index"),
            JsonMapping.Int(root, "death_ref_index"),
            JsonMapping.EventRefArray(root, "event_ref_list"),
            JsonMapping.StringArray(root, "family_list"),
            JsonMapping.StringArray(root, "parent_family_list"),
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.PersonRefArray(root, "person_ref_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
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
            JsonMapping.Date(root, "date"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
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
        var families = await GetFamilyAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return families.FirstOrDefault();
    }

    public async Task<IReadOnlyList<FamilyDto>> GetFamilyAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("family", "family", handles, grampsIds, MapFamily, cancellationToken);
    }

    private static FamilyDto MapFamily(string family)
    {
        using var document = JsonDocument.Parse(family);
        var root = document.RootElement;
        return new FamilyDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "father_handle"),
            JsonMapping.String(root, "mother_handle"),
            JsonMapping.GrampsType(root, "type"),
            JsonMapping.StructuredChildRefArray(root, "child_ref_list"),
            JsonMapping.EventRefArray(root, "event_ref_list"),
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
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

    public async Task<IReadOnlyList<PlaceDto>> GetPlaceAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("place", "place", handles, grampsIds, MapPlace, cancellationToken);
    }

    public async Task<RepositoryDto?> GetRepositoryAsync(string? handle, string? grampsId,
        CancellationToken cancellationToken = default)
    {
        var repositories = await GetRepositoryAsync(ToLookupList(handle), ToLookupList(grampsId), cancellationToken);
        return repositories.FirstOrDefault();
    }

    public async Task<IReadOnlyList<RepositoryDto>> GetRepositoryAsync(IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectsAsync("repository", "repository", handles, grampsIds, MapRepository,
            cancellationToken);
    }

    public async Task<PageDto<TagDto>> ListTagsAsync(int limit = 100, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(limit, offset);
        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM tag";
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT json_data FROM tag ORDER BY priority, name, handle LIMIT $limit OFFSET $offset";
        command.Parameters.AddWithValue("$limit", limit + 1);
        command.Parameters.AddWithValue("$offset", offset);

        var tags = new List<TagDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(MapTag(reader.GetString(0)));
        }

        return CreatePage(tags, limit, offset, totalCount);
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

    public async Task<PageDto<TaggedObjectDto>> FindObjectsByTagAsync(
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

        ValidatePaging(limit, offset);

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var resolvedTagHandle = string.IsNullOrWhiteSpace(tagHandle)
            ? await ReadTagHandleByNameAsync(connection, transaction, tagName!, cancellationToken)
            : tagHandle;
        if (string.IsNullOrWhiteSpace(resolvedTagHandle))
        {
            return new PageDto<TaggedObjectDto>([], limit, offset, 0, 0, false, null);
        }

        ObjectTable[] tables;
        if (objectTypes is null)
        {
            tables = TaggedObjectTables;
        }
        else
        {
            tables = ResolveObjectTables(objectTypes);
            if (tables.Any(static table => table.ObjectType == "tag"))
            {
                throw new ArgumentException("Unsupported tagged object type: tag.");
            }
        }

        if (tables.Length == 0)
        {
            return new PageDto<TaggedObjectDto>([], limit, offset, 0, 0, false, null);
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

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = $"SELECT COUNT(*) FROM ({unionSql})";
        countCommand.Parameters.AddWithValue("$tagHandle", resolvedTagHandle);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"""
                               SELECT object_type, handle, gramps_id, label, json_data
                               FROM ({unionSql})
                               ORDER BY object_type, label, gramps_id, handle
                               LIMIT $limit OFFSET $offset
                               """;
        command.Parameters.AddWithValue("$tagHandle", resolvedTagHandle);
        command.Parameters.AddWithValue("$limit", limit + 1);
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
                JsonMapping.StringArray(document.RootElement, "tag_list"),
                JsonMapping.Long(document.RootElement, "change"),
                JsonMapping.Bool(document.RootElement, "private")));
        }

        return CreatePage(objects, limit, offset, totalCount);
    }

    private async Task<string?> GetObjectAsync(string tableName, string? handle, string? grampsId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(handle) == string.IsNullOrWhiteSpace(grampsId))
        {
            throw new ArgumentException("Supply either handle or grampsId, but not both.");
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
            JsonMapping.GrampsType(root, "type"),
            JsonMapping.NoteLinks(root),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
    }

    private static CitationDto MapCitation(string citation)
    {
        using var document = JsonDocument.Parse(citation);
        var root = document.RootElement;
        return new CitationDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.Date(root, "date"),
            JsonMapping.String(root, "page"),
            JsonMapping.Int(root, "confidence"),
            JsonMapping.String(root, "source_handle"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
    }

    private static EventDto MapEvent(string @event)
    {
        using var document = JsonDocument.Parse(@event);
        var root = document.RootElement;
        return new EventDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.GrampsType(root, "type"),
            JsonMapping.Date(root, "date"),
            JsonMapping.String(root, "description"),
            JsonMapping.String(root, "place"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
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
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.RepositoryRefArray(root, "reporef_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
    }

    private static PlaceDto MapPlace(string place)
    {
        using var document = JsonDocument.Parse(place);
        var root = document.RootElement;
        var primaryName = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.Object
            ? JsonMapping.String(name, "value")
            : null;

        return new PlaceDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "title"),
            JsonMapping.String(root, "long"),
            JsonMapping.String(root, "lat"),
            primaryName,
            JsonMapping.GrampsType(root, "place_type"),
            JsonMapping.PlaceRefArray(root, "placeref_list"),
            JsonMapping.MediaRefArray(root, "media_list"),
            JsonMapping.StringArray(root, "citation_list"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
    }

    private static RepositoryDto MapRepository(string repository)
    {
        using var document = JsonDocument.Parse(repository);
        var root = document.RootElement;
        return new RepositoryDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.GrampsType(root, "type"),
            JsonMapping.String(root, "name"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "tag_list"),
            JsonMapping.Long(root, "change"),
            JsonMapping.Bool(root, "private"));
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

    private static async Task<string?> ReadTagHandleByNameAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tagName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT handle FROM tag WHERE name = $name LIMIT 1";
        command.Parameters.AddWithValue("$name", tagName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static PageDto<T> CreatePage<T>(List<T> items, int limit, int offset, int totalCount)
    {
        var hasMore = items.Count > limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new PageDto<T>(items, limit, offset, items.Count, totalCount, hasMore,
            hasMore ? offset + items.Count : null);
    }

    private static void ValidatePaging(int limit, int offset)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be from 1 to 500.");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must not be negative.");
        }
    }

    private static ObjectTable[] ResolveObjectTables(IReadOnlyList<string>? objectTypes)
    {
        if (objectTypes is null)
        {
            return ObjectTables;
        }

        if (objectTypes.Any(static value => string.IsNullOrWhiteSpace(value)))
        {
            throw new ArgumentException("Object types must not be empty.");
        }

        var requestedTypes = objectTypes.Select(static value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownTypes = requestedTypes.Except(ObjectTables.Select(static table => table.ObjectType),
            StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknownTypes.Length > 0)
        {
            throw new ArgumentException($"Unsupported object type: {string.Join(", ", unknownTypes)}.");
        }

        return ObjectTables.Where(table => requestedTypes.Contains(table.ObjectType)).ToArray();
    }

    private static string BuildObjectUnionSql(IEnumerable<ObjectTable> tables)
    {
        return string.Join(" UNION ALL ", tables.Select(static table => $"""
                                                                         SELECT '{table.ObjectType}' AS object_type,
                                                                                handle,
                                                                                {table.GrampsIdExpression} AS gramps_id,
                                                                                {table.LabelExpression} AS label,
                                                                                {table.ChangeExpression} AS change,
                                                                                {table.PrivateExpression} AS private
                                                                         FROM {table.TableName}
                                                                         """));
    }

    private static void AddSearchParameters(SqliteCommand command, string query)
    {
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$like", $"%{query}%");
    }

    private static readonly ObjectTable[] ObjectTables =
    [
        new("person", "person", "gramps_id", "trim(COALESCE(given_name, '') || ' ' || COALESCE(surname, ''))", "change",
            "private"),
        new("family", "family", "gramps_id", "COALESCE(gramps_id, handle)", "change", "private"),
        new("event", "event", "gramps_id", "COALESCE(description, gramps_id, handle)", "change", "private"),
        new("place", "place", "gramps_id", "COALESCE(title, gramps_id, handle)", "change", "private"),
        new("source", "source", "gramps_id", "COALESCE(title, gramps_id, handle)", "change", "private"),
        new("citation", "citation", "gramps_id", "COALESCE(page, gramps_id, handle)", "change", "private"),
        new("media", "media", "gramps_id", "COALESCE(desc, path, gramps_id, handle)", "change", "private"),
        new("repository", "repository", "gramps_id", "COALESCE(name, gramps_id, handle)", "change", "private"),
        new("note", "note", "gramps_id", "COALESCE(json_extract(json_data, '$.text.string'), gramps_id, handle)",
            "change", "private"),
        new("tag", "tag", "NULL", "COALESCE(name, handle)", "change", "NULL")
    ];

    private static readonly ObjectTable[] TaggedObjectTables =
        ObjectTables.Where(static table => table.ObjectType != "tag").ToArray();

    private sealed record ObjectTable(
        string ObjectType,
        string TableName,
        string GrampsIdExpression,
        string LabelExpression,
        string ChangeExpression,
        string PrivateExpression);

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