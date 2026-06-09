using GrampsDbTool.Configuration;
using GrampsDbTool.Models;
using System.IO.Compression;

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

    public async Task<BackupArchiveDto> CreateArchiveAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(config.DatabasePath))
        {
            throw new FileNotFoundException($"Gramps SQLite database not found: {config.DatabasePath}", config.DatabasePath);
        }

        if (string.IsNullOrWhiteSpace(databasePaths.SavePath))
        {
            throw new InvalidOperationException("Gramps database metadata 'save-path' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(databasePaths.MediaBasePath))
        {
            throw new InvalidOperationException("Gramps database metadata 'media-path' is missing or empty.");
        }

        var mediaRoot = Path.GetFullPath(databasePaths.MediaBasePath);
        if (!Directory.Exists(mediaRoot))
        {
            throw new DirectoryNotFoundException($"Gramps media path not found: {mediaRoot}");
        }

        var backupDirectory = databasePaths.SavePath;
        Directory.CreateDirectory(backupDirectory.NotNull());
        var createdAtUtc = DateTimeOffset.UtcNow;
        var timestamp = createdAtUtc.ToString("yyyyMMddHHmmss");
        var backupPath = Path.GetFullPath(Path.Combine(backupDirectory, $"gramps-db-tool-{timestamp}.zip"));
        var databaseEntry = Path.GetFileName(config.DatabasePath);
        var mediaFileCount = 0;

        await using var fileStream = File.Create(backupPath);
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(config.DatabasePath, databaseEntry, CompressionLevel.Optimal);

            foreach (var mediaFile in Directory.EnumerateFiles(mediaRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullMediaFile = Path.GetFullPath(mediaFile);
                if (fullMediaFile.Equals(backupPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var entryName = Path.Combine("media", Path.GetRelativePath(mediaRoot, fullMediaFile))
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                archive.CreateEntryFromFile(fullMediaFile, entryName, CompressionLevel.Optimal);
                mediaFileCount++;
            }
        }

        return new BackupArchiveDto(backupPath, databaseEntry, mediaRoot, mediaFileCount, createdAtUtc);
    }
}
