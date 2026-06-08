using GrampsDbTool.Configuration;

namespace GrampsDbTool.Safety;

public sealed class BackupService(GrampsConfig config, GrampsDatabasePaths databasePaths)
{
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePaths.SavePath))
        {
            throw new InvalidOperationException("Cannot create a write backup because Gramps save path metadata is missing or empty.");
        }

        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}", config.DatabasePath);
        }

        Directory.CreateDirectory(databasePaths.SavePath);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(databasePaths.SavePath, $"gramps-db-tool-{timestamp}.sqlite.db");

        await using var source = File.OpenRead(config.DatabasePath);
        await using var destination = File.Create(backupPath);
        await source.CopyToAsync(destination, cancellationToken);

        return backupPath;
    }
}
