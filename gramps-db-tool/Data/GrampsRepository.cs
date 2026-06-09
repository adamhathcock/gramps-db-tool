using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Models;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsRepository(GrampsConfig config, IMediaPathService mediaPathService)
{
    public async Task<IReadOnlyList<PersonSearchResultDto>> SearchPeopleAsync(string query, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
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

    public async Task<PersonDto?> GetPersonAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
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
            JsonMapping.StringArray(root, "citation_list"));
    }

    public async Task<IReadOnlyList<MediaDto>> GetMediaAsync(IReadOnlyList<string>? handles, IReadOnlyList<string>? grampsIds = null, CancellationToken cancellationToken = default)
    {
        var hasHandles = handles is not null;
        var hasGrampsIds = grampsIds is not null;
        if (hasHandles == hasGrampsIds)
        {
            throw new ArgumentException("Supply either media handles or Gramps IDs, but not both.");
        }

        var lookupValues = hasHandles ? handles! : grampsIds!;
        var lookupName = hasHandles ? "handles" : "grampsIds";
        var columnName = hasHandles ? "handle" : "gramps_id";

        if (lookupValues.Count == 0)
        {
            throw new ArgumentException($"At least one media {lookupName} value is required.");
        }

        if (lookupValues.Count > 100)
        {
            throw new ArgumentException($"At most 100 media {lookupName} values may be requested.");
        }

        var requestedValues = lookupValues.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (requestedValues.Length != lookupValues.Count)
        {
            throw new ArgumentException($"Media {lookupName} values must not be empty.");
        }

        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var parameters = requestedValues.Distinct().Select((value, index) => new { Value = value, Parameter = $"$value{index}" }).ToArray();
        command.CommandText = $"SELECT json_data, {columnName} FROM media WHERE {columnName} IN ({string.Join(", ", parameters.Select(static parameter => parameter.Parameter))})";
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Parameter, parameter.Value);
        }

        var mediaByLookupValue = new Dictionary<string, MediaDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var media = MapMedia(reader.GetString(0));
            mediaByLookupValue[reader.GetString(1)] = media;
        }

        var mediaList = new List<MediaDto>();
        foreach (var value in requestedValues)
        {
            if (mediaByLookupValue.TryGetValue(value, out var media))
            {
                mediaList.Add(media);
            }
        }

        return mediaList;
    }

    public async Task<MediaDto?> GetMediaAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
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
            JsonMapping.StringArray(root, "citation_list"));
    }

    public async Task<NoteDto?> GetNoteAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
    {
        var note = await GetObjectAsync("note", handle, grampsId, cancellationToken);
        if (note is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(note);
        var root = document.RootElement;
        return new NoteDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.NoteText(root),
            JsonMapping.Int(root, "format"),
            JsonMapping.GrampsTypeName(root, "type"));
    }

    public async Task<CitationDto?> GetCitationAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
    {
        var citation = await GetObjectAsync("citation", handle, grampsId, cancellationToken);
        if (citation is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(citation);
        var root = document.RootElement;
        return new CitationDto(
            RequiredString(root, "handle"),
            JsonMapping.String(root, "gramps_id"),
            JsonMapping.String(root, "page"),
            JsonMapping.Int(root, "confidence"),
            JsonMapping.String(root, "source_handle"),
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.RefArray(root, "media_list"));
    }

    public async Task<FamilyDto?> GetFamilyAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
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
            JsonMapping.StringArray(root, "citation_list"));
    }

    public async Task<EventDto?> GetEventAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
    {
        var @event = await GetObjectAsync("event", handle, grampsId, cancellationToken);
        if (@event is null)
        {
            return null;
        }

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
            JsonMapping.RefArray(root, "media_list"));
    }

    public async Task<SourceDto?> GetSourceAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
    {
        var source = await GetObjectAsync("source", handle, grampsId, cancellationToken);
        if (source is null)
        {
            return null;
        }

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
            JsonMapping.RefArray(root, "media_list"));
    }

    private async Task<string?> GetObjectAsync(string tableName, string? handle, string? grampsId, CancellationToken cancellationToken)
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

    private string CreateConnectionString()
    {
        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}", config.DatabasePath);
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
