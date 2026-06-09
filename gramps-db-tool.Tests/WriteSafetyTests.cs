using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using GrampsDbTool.Safety;
using GrampsDbTool.Services;
using Microsoft.Data.Sqlite;
using System.IO.Compression;

namespace GrampsDbTool.Tests;

public sealed class WriteSafetyTests
{
    [Fact]
    public void WriteGuardRejectsWritesWhenRuntimeFlagIsDisabled()
    {
        var guard = new WriteGuard(new RuntimeOptions("config.json", AllowWrites: false));

        var exception = Assert.Throws<InvalidOperationException>(guard.RequireWritesEnabled);
        Assert.Contains("Writes are disabled", exception.Message);
    }

    [Fact]
    public async Task BackupServiceCreatesBackupInDatabaseDerivedSavePath()
    {
        using var database = new TestDatabase();
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath,null),
            new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var backupPath = await service.CreateBackupAsync();

        Assert.True(File.Exists(backupPath));
        Assert.Equal(database.SavePath, Path.GetDirectoryName(backupPath));
    }

    [Fact]
    public async Task BackupServiceUsesConfiguredBackupPathOverDatabaseSavePath()
    {
        using var database = new TestDatabase();
        var configuredBackupPath = Path.Combine(database.DirectoryPath, "configured-backups");
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath, configuredBackupPath),
            new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var backupPath = await service.CreateBackupAsync();

        Assert.True(File.Exists(backupPath));
        Assert.Equal(configuredBackupPath, Path.GetDirectoryName(backupPath));
        Assert.Empty(Directory.GetFiles(database.SavePath, "gramps-db-tool-*.sqlite.db"));
    }

    [Fact]
    public async Task BackupServiceCreatesArchiveWithDatabaseAndMediaInDatabaseDerivedSavePath()
    {
        using var database = new TestDatabase();
        var nestedMediaDirectory = Path.Combine(database.MediaPath, "photos", "nested");
        Directory.CreateDirectory(nestedMediaDirectory);
        await File.WriteAllTextAsync(Path.Combine(database.MediaPath, "root.txt"), "root media");
        await File.WriteAllTextAsync(Path.Combine(nestedMediaDirectory, "portrait.jpg"), "nested media");
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath, BackupPath: null),
            new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var archive = await service.CreateArchiveAsync();

        Assert.True(File.Exists(archive.BackupPath));
        Assert.Equal(database.SavePath, Path.GetDirectoryName(archive.BackupPath));
        Assert.Equal(Path.GetFullPath(database.MediaPath), archive.MediaRoot);
        Assert.Equal(2, archive.MediaFileCount);

        using var zip = ZipFile.OpenRead(archive.BackupPath);
        Assert.Contains(zip.Entries, entry => entry.FullName == Path.GetFileName(database.DatabasePath));
        Assert.Contains(zip.Entries, entry => entry.FullName == "media/root.txt");
        Assert.Contains(zip.Entries, entry => entry.FullName == "media/photos/nested/portrait.jpg");
    }

    [Fact]
    public async Task BackupServiceArchiveRequiresDatabaseDerivedSavePath()
    {
        using var database = new TestDatabase(includeSavePath: false);
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath, Path.Combine(database.DirectoryPath, "configured-backups")),
            new GrampsDatabasePaths(database.MediaPath, SavePath: null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateArchiveAsync());
        Assert.Contains("save-path", exception.Message);
    }

    [Fact]
    public async Task BackupServiceArchiveRequiresDatabaseDerivedMediaPath()
    {
        using var database = new TestDatabase(includeMediaPath: false);
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath, BackupPath: null),
            new GrampsDatabasePaths(string.Empty, database.SavePath));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateArchiveAsync());
        Assert.Contains("media-path", exception.Message);
    }

    [Fact]
    public async Task UpdateMediaPathRejectsWhenWritesAreDisabled()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMediaPathAsync(new UpdateMediaPathRequest("media1", "photos/new.jpg", ConvertToRelative: false)));
        Assert.Contains("Writes are disabled", exception.Message);
    }

    [Fact]
    public async Task UpdateMediaPathCreatesBackupAndChangesOnlyMediaPathFields()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var updated = await service.UpdateMediaPathAsync(new UpdateMediaPathRequest("media1", "photos/new.jpg", ConvertToRelative: false));

        Assert.Equal("photos/new.jpg", updated.NotNull().Path);
        Assert.Equal(Path.Combine(database.MediaPath, "photos/new.jpg"), updated.ResolvedPath);
        Assert.NotEmpty(Directory.GetFiles(database.SavePath, "gramps-db-tool-*.sqlite.db"));

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, json_data FROM media WHERE handle = 'media1'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("photos/new.jpg", reader.GetString(0));
        Assert.Contains("photos/new.jpg", reader.GetString(1));
        Assert.Contains("Portrait", reader.GetString(1));
    }

    [Fact]
    public async Task UpdateMediaPathCanConvertAbsolutePathInsideMediaRootToRelative()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);
        var absolutePath = Path.Combine(database.MediaPath, "photos/relative.jpg");

        var updated = await service.UpdateMediaPathAsync(new UpdateMediaPathRequest("media1", absolutePath, ConvertToRelative: true));

        Assert.Equal(Path.Combine("photos", "relative.jpg"), updated.NotNull().Path);
    }

    private static MediaWriteService CreateMediaWriteService(TestDatabase database, bool allowWrites)
    {
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = new GrampsDatabasePaths(database.MediaPath, database.SavePath);
        var mediaPathService = new MediaPathService(paths);
        var repository = new GrampsRepository(config, mediaPathService);

        return new MediaWriteService(
            new WriteGuard(new RuntimeOptions(database.WriteConfig(), allowWrites)),
            new SingleWriterLock(),
            new BackupService(config, paths),
            new GrampsConnectionFactory(config),
            repository,
            mediaPathService);
    }
}
