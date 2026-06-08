using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Models;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsRepository(GrampsConfig config, IMediaPathService mediaPathService)
{
    public async Task<IReadOnlyList<PersonSearchResultDto>> SearchPeopleAsync(string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        limit = Math.Clamp(limit, 1, 100);

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
            """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$like", $"%{query}%");
        command.Parameters.AddWithValue("$limit", limit);

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
            JsonMapping.StringArray(root, "note_list"),
            JsonMapping.StringArray(root, "citation_list"));
    }

    public async Task<MediaDto?> GetMediaAsync(string? handle, string? grampsId, CancellationToken cancellationToken = default)
    {
        var media = await GetObjectAsync("media", handle, grampsId, cancellationToken);
        if (media is null)
        {
            return null;
        }

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
