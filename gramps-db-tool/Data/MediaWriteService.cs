using System.Text.Json;
using System.Text.Json.Nodes;
using GrampsDbTool.Models;
using GrampsDbTool.Safety;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class MediaWriteService(
    WriteGuard writeGuard,
    SingleWriterLock singleWriterLock,
    BackupService backupService,
    GrampsConnectionFactory connectionFactory,
    GrampsRepository repository,
    IMediaPathService mediaPathService)
{
    public async Task<MediaDto?> UpdateMediaAsync(UpdateMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.MediaHandle))
        {
            throw new ArgumentException("Media handle is required.", nameof(request));
        }

        if (request.NewPath is not null && string.IsNullOrWhiteSpace(request.NewPath))
        {
            throw new ArgumentException("New media path is required.", nameof(request));
        }

        if (request.NewPath is null && request.TagHandles is null)
        {
            throw new ArgumentException("Supply a new media path and/or tag handles.", nameof(request));
        }

        ValidateTagHandles(request.TagHandles);

        writeGuard.RequireWritesEnabled();

        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        await backupService.CreateBackupAsync(cancellationToken);

        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = connectionFactory.CreateReadWriteConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (request.TagHandles is not null)
        {
            await RequireTagsExistAsync(connection, transaction, request.TagHandles, cancellationToken);
        }

        var media = await ReadMediaAsync(connection, transaction, request.MediaHandle, cancellationToken);
        var storedPath = request.NewPath is null ? media.Path : NormalizeStoredPath(request);
        var afterJson =
            UpdateMediaJson(media.Json, storedPath, request.NewPath is not null, request.TagHandles, change);

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
                              UPDATE media
                              SET json_data = $json, path = $path, change = $change
                              WHERE handle = $handle
                              """;
        command.Parameters.AddWithValue("$json", afterJson);
        command.Parameters.AddWithValue("$path", (object?)storedPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$change", change);
        command.Parameters.AddWithValue("$handle", request.MediaHandle);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                "Media path update failed because the target row was not updated exactly once.");
        }

        if (request.TagHandles is not null)
        {
            await ReplaceTagReferencesAsync(connection, transaction, request.MediaHandle, request.TagHandles,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var updated = await repository.GetMediaAsync(request.MediaHandle, null, cancellationToken);
        return updated;
    }

    private string NormalizeStoredPath(UpdateMediaRequest request)
    {
        var newPath = request.NewPath ?? throw new InvalidOperationException("New media path is required.");
        if (request.ConvertToRelative && Path.IsPathRooted(newPath))
        {
            var relativePath = mediaPathService.ToRelativePath(newPath);
            mediaPathService.ValidateMediaPath(relativePath);
            return relativePath;
        }

        mediaPathService.ValidateMediaPath(newPath);
        return newPath;
    }

    private static async Task<MediaRecord> ReadMediaAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string mediaHandle, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT json_data, path FROM media WHERE handle = $handle LIMIT 1";
        command.Parameters.AddWithValue("$handle", mediaHandle);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetValue(0) is not string json ||
            string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Media object not found or does not contain JSON data.");
        }

        return new MediaRecord(json, reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static string UpdateMediaJson(string beforeJson, string? storedPath, bool updatePath,
        IReadOnlyList<string>? tagHandles, long change)
    {
        var node = JsonNode.Parse(beforeJson)?.AsObject()
                   ?? throw new InvalidOperationException("Media JSON data is not an object.");

        if (updatePath)
        {
            node["path"] = storedPath;
        }

        if (tagHandles is not null)
        {
            var tagList = new JsonArray();
            foreach (var tagHandle in tagHandles)
            {
                tagList.Add(tagHandle);
            }

            node["tag_list"] = tagList;
        }

        node["change"] = change;

        return node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static void ValidateTagHandles(IReadOnlyList<string>? tagHandles)
    {
        if (tagHandles is null)
        {
            return;
        }

        if (tagHandles.Any(static handle => string.IsNullOrWhiteSpace(handle)))
        {
            throw new ArgumentException("Tag handles must not be empty.");
        }

        var duplicate = tagHandles.GroupBy(static handle => handle, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate tag handle supplied: {duplicate}.");
        }
    }

    private static async Task RequireTagsExistAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, IReadOnlyList<string> tagHandles,
        CancellationToken cancellationToken)
    {
        if (tagHandles.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        var parameters = tagHandles.Select((handle, index) => new { Handle = handle, Parameter = $"$tag{index}" })
            .ToArray();
        command.CommandText =
            $"SELECT handle FROM tag WHERE handle IN ({string.Join(", ", parameters.Select(static parameter => parameter.Parameter))})";
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Parameter, parameter.Handle);
        }

        var existingHandles = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            existingHandles.Add(reader.GetString(0));
        }

        var missingHandles = tagHandles.Where(handle => !existingHandles.Contains(handle)).ToArray();
        if (missingHandles.Length > 0)
        {
            throw new InvalidOperationException($"Unknown tag handle(s): {string.Join(", ", missingHandles)}.");
        }
    }

    private static async Task ReplaceTagReferencesAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string mediaHandle, IReadOnlyList<string> tagHandles,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = (SqliteTransaction)transaction;
            deleteCommand.CommandText =
                "DELETE FROM reference WHERE obj_handle = $mediaHandle AND ref_class = 'Tag'";
            deleteCommand.Parameters.AddWithValue("$mediaHandle", mediaHandle);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var tagHandle in tagHandles)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = (SqliteTransaction)transaction;
            insertCommand.CommandText = """
                                        INSERT INTO reference (obj_handle, obj_class, ref_handle, ref_class)
                                        VALUES ($mediaHandle, 'Media', $tagHandle, 'Tag')
                                        """;
            insertCommand.Parameters.AddWithValue("$mediaHandle", mediaHandle);
            insertCommand.Parameters.AddWithValue("$tagHandle", tagHandle);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record MediaRecord(string Json, string? Path);
}