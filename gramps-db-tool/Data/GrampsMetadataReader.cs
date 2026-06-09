using System.Text.Json;
using GrampsDbTool.Configuration;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class GrampsMetadataReader(GrampsConfig config)
{
    public async Task<GrampsDatabasePaths> ReadDatabasePathsAsync(CancellationToken cancellationToken = default)
    {
        var mediaBasePath = await ReadRequiredStringMetadataAsync("media-path", cancellationToken);
        var savePath = await ReadOptionalStringMetadataAsync("save-path", cancellationToken);
        return new GrampsDatabasePaths(mediaBasePath, savePath);
    }

    private async Task<string> ReadRequiredStringMetadataAsync(string setting, CancellationToken cancellationToken)
    {
        var value = await ReadOptionalStringMetadataAsync(setting, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Gramps database metadata '{setting}' is missing or empty.");
        }

        return value;
    }

    private async Task<string?> ReadOptionalStringMetadataAsync(string setting, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json_data FROM metadata WHERE setting = $setting";
        command.Parameters.AddWithValue("$setting", setting);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string json || string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return valueElement.GetString();
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
}
