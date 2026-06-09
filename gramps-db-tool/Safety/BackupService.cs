using GrampsDbTool.Configuration;

namespace GrampsDbTool.Safety;

public sealed class BackupService(GrampsConfig config, GrampsDatabasePaths databasePaths)
{
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}", config.DatabasePath);
        }

        var backupDirectory = config.BackupPath ?? databasePaths.SavePath;
        Directory.CreateDirectory(backupDirectory.NotNull());
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(backupDirectory, $"gramps-db-tool-{timestamp}.sqlite.db");

        await using var source = File.OpenRead(config.DatabasePath);
        await using var destination = File.Create(backupPath);
        await source.CopyToAsync(destination, cancellationToken);

        return backupPath;
    }
}