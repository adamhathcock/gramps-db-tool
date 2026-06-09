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
    public async Task<MediaDto?> UpdateMediaPathAsync(UpdateMediaPathRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.MediaHandle))
        {
            throw new ArgumentException("Media handle is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.NewPath))
        {
            throw new ArgumentException("New media path is required.", nameof(request));
        }

        writeGuard.RequireWritesEnabled();

        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        await backupService.CreateBackupAsync(cancellationToken);

        var storedPath = NormalizeStoredPath(request);
        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = connectionFactory.CreateReadWriteConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var beforeJson = await ReadMediaJsonAsync(connection, transaction, request.MediaHandle, cancellationToken);
        var afterJson = UpdateMediaJson(beforeJson, storedPath, change);

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE media
            SET json_data = $json, path = $path, change = $change
            WHERE handle = $handle
            """;
        command.Parameters.AddWithValue("$json", afterJson);
        command.Parameters.AddWithValue("$path", storedPath);
        command.Parameters.AddWithValue("$change", change);
        command.Parameters.AddWithValue("$handle", request.MediaHandle);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected != 1)
        {
            throw new InvalidOperationException("Media path update failed because the target row was not updated exactly once.");
        }

        await transaction.CommitAsync(cancellationToken);

        var updated = await repository.GetMediaAsync(request.MediaHandle, null, cancellationToken);
        return updated;
    }

    private string NormalizeStoredPath(UpdateMediaPathRequest request)
    {
        if (request.ConvertToRelative && Path.IsPathRooted(request.NewPath))
        {
            var relativePath = mediaPathService.ToRelativePath(request.NewPath);
            mediaPathService.ValidateMediaPath(relativePath);
            return relativePath;
        }

        mediaPathService.ValidateMediaPath(request.NewPath);
        return request.NewPath;
    }

    private static async Task<string> ReadMediaJsonAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, string mediaHandle, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT json_data FROM media WHERE handle = $handle LIMIT 1";
        command.Parameters.AddWithValue("$handle", mediaHandle);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string json || string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Media object not found or does not contain JSON data.");
        }

        return json;
    }

    private static string UpdateMediaJson(string beforeJson, string storedPath, long change)
    {
        var node = JsonNode.Parse(beforeJson)?.AsObject()
            ?? throw new InvalidOperationException("Media JSON data is not an object.");

        node["path"] = storedPath;
        node["change"] = change;

        return node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
