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
            new GrampsConfig(database.DirectoryPath, database.DatabasePath, null),
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
            new GrampsConfig(database.DirectoryPath, database.DatabasePath,
                Path.Combine(database.DirectoryPath, "configured-backups")),
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
    public async Task UpdateMediaRejectsWhenWritesAreDisabled()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMediaAsync(new UpdateMediaRequest("media1", "photos/new.jpg", ConvertToRelative: false,
                TagHandles: null)));
        Assert.Contains("Writes are disabled", exception.Message);
    }

    [Fact]
    public async Task UpdateMediaCreatesBackupAndChangesPathWhilePreservingTags()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var updated = await service.UpdateMediaAsync(new UpdateMediaRequest("media1", "photos/new.jpg",
            ConvertToRelative: false, TagHandles: null));

        Assert.Equal("photos/new.jpg", updated.NotNull().Path);
        Assert.Equal(Path.Combine(database.MediaPath, "photos/new.jpg"), updated.ResolvedPath);
        Assert.Equal(["tag1", "tag2"], updated.TagHandles);
        Assert.NotEmpty(Directory.GetFiles(database.SavePath, "gramps-db-tool-*.sqlite.db"));

        using var connection =
            new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, json_data FROM media WHERE handle = 'media1'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("photos/new.jpg", reader.GetString(0));
        Assert.Contains("photos/new.jpg", reader.GetString(1));
        Assert.Contains("Portrait", reader.GetString(1));
        Assert.Contains("tag1", reader.GetString(1));
        Assert.Contains("tag2", reader.GetString(1));
    }

    [Fact]
    public async Task UpdateMediaCanConvertAbsolutePathInsideMediaRootToRelative()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);
        var absolutePath = Path.Combine(database.MediaPath, "photos/relative.jpg");

        var updated =
            await service.UpdateMediaAsync(new UpdateMediaRequest("media1", absolutePath, ConvertToRelative: true,
                TagHandles: null));

        Assert.Equal(Path.Combine("photos", "relative.jpg"), updated.NotNull().Path);
    }

    [Fact]
    public async Task UpdateMediaCanReplaceTagsOnlyWhilePreservingPath()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var updated = await service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null,
            ConvertToRelative: false, TagHandles: ["tag2"]));

        Assert.Equal("photos/ada.jpg", updated.NotNull().Path);
        Assert.Equal(["tag2"], updated.TagHandles);

        using var connection =
            new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT path, json_extract(json_data, '$.tag_list[0]'), json_array_length(json_extract(json_data, '$.tag_list')) FROM media WHERE handle = 'media1'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("photos/ada.jpg", reader.GetString(0));
        Assert.Equal("tag2", reader.GetString(1));
        Assert.Equal(1, reader.GetInt32(2));
    }

    [Fact]
    public async Task UpdateMediaCanChangePathAndReplaceTags()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var updated = await service.UpdateMediaAsync(new UpdateMediaRequest("media1", "photos/new.jpg",
            ConvertToRelative: false, TagHandles: ["tag1"]));

        Assert.Equal("photos/new.jpg", updated.NotNull().Path);
        Assert.Equal(["tag1"], updated.TagHandles);
    }

    [Fact]
    public async Task UpdateMediaCanClearTags()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var updated =
            await service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null, ConvertToRelative: false,
                TagHandles: []));

        Assert.Empty(updated.NotNull().TagHandles);
    }

    [Fact]
    public async Task UpdateMediaRejectsMissingPathAndTags()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null, ConvertToRelative: false,
                TagHandles: null)));

        Assert.Contains("path and/or tag", exception.Message);
    }

    [Fact]
    public async Task UpdateMediaRejectsInvalidTagHandles()
    {
        using var database = new TestDatabase();
        var service = CreateMediaWriteService(database, allowWrites: true);

        var blankException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null, ConvertToRelative: false,
                TagHandles: [""])));
        var duplicateException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null, ConvertToRelative: false,
                TagHandles: ["tag1", "tag1"])));
        var missingException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMediaAsync(new UpdateMediaRequest("media1", NewPath: null, ConvertToRelative: false,
                TagHandles: ["missing"])));

        Assert.Contains("must not be empty", blankException.Message);
        Assert.Contains("Duplicate", duplicateException.Message);
        Assert.Contains("Unknown tag", missingException.Message);
    }

    [Fact]
    public async Task UpdateNoteChangesTextAndTags()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var updated = await service.UpdateNoteAsync(new UpdateNoteRequest("note1", "Updated note", ["tag1"]));

        Assert.Equal("Updated note", updated.NotNull().Text);
        Assert.Equal(["tag1"], updated.TagHandles);
        Assert.NotEmpty(Directory.GetFiles(database.SavePath, "gramps-db-tool-*.sqlite.db"));
        Assert.Equal("Updated note", ReadJsonValue(database, "note", "note1", "$.text.string"));
    }

    [Fact]
    public async Task UpdateCitationChangesScalarsAndTagsButPreservesRelationships()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var updated = await service.UpdateCitationAsync(new UpdateCitationRequest("citation1", "p. 2", 4, ["tag2"]));

        Assert.Equal("p. 2", updated.NotNull().Page);
        Assert.Equal(4, updated.Confidence);
        Assert.Equal("source1", updated.SourceHandle);
        Assert.Equal(["note1"], updated.NoteHandles);
        Assert.Equal("media1", Assert.Single(updated.MediaHandles));
        Assert.Equal(["tag2"], updated.TagHandles);
        Assert.Equal("source1", ReadJsonValue(database, "citation", "citation1", "$.source_handle"));
        Assert.Equal("note1", ReadJsonValue(database, "citation", "citation1", "$.note_list[0]"));
        Assert.Equal("media1", ReadJsonValue(database, "citation", "citation1", "$.media_list[0].ref"));
    }

    [Fact]
    public async Task UpdateEventChangesDescriptionAndTagsButPreservesRelationships()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var updated = await service.UpdateEventAsync(new UpdateEventRequest("event1", "Updated birth", ["tag2"]));

        Assert.Equal("Updated birth", updated.NotNull().Description);
        Assert.Equal("place1", updated.PlaceHandle);
        Assert.Equal(["note1"], updated.NoteHandles);
        Assert.Equal(["citation1"], updated.CitationHandles);
        Assert.Equal("media1", Assert.Single(updated.MediaHandles));
        Assert.Equal(["tag2"], updated.TagHandles);
        Assert.Equal("Birth", ReadJsonValue(database, "event", "event1", "$.type.string"));
        Assert.Equal("place1", ReadJsonValue(database, "event", "event1", "$.place"));
        Assert.Equal("note1", ReadJsonValue(database, "event", "event1", "$.note_list[0]"));
        Assert.Equal("citation1", ReadJsonValue(database, "event", "event1", "$.citation_list[0]"));
        Assert.Equal("media1", ReadJsonValue(database, "event", "event1", "$.media_list[0].ref"));
    }

    [Fact]
    public async Task UpdateSourceChangesScalarStringsAndTagsButPreservesRelationships()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var updated = await service.UpdateSourceAsync(new UpdateSourceRequest("source1", "New Register", "New Author",
            "New Pub", "NR", ["tag1"]));

        Assert.Equal("New Register", updated.NotNull().Title);
        Assert.Equal("New Author", updated.Author);
        Assert.Equal("New Pub", updated.PublicationInfo);
        Assert.Equal("NR", updated.Abbreviation);
        Assert.Equal(["note1"], updated.NoteHandles);
        Assert.Equal("media1", Assert.Single(updated.MediaHandles));
        Assert.Equal(["tag1"], updated.TagHandles);
        Assert.Equal("note1", ReadJsonValue(database, "source", "source1", "$.note_list[0]"));
        Assert.Equal("media1", ReadJsonValue(database, "source", "source1", "$.media_list[0].ref"));
    }

    [Fact]
    public async Task ObjectUpdatesRejectWhenWritesAreDisabled()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateEventAsync(new UpdateEventRequest("event1", "Updated", null)));

        Assert.Contains("Writes are disabled", exception.Message);
    }

    [Fact]
    public async Task ObjectUpdatesRejectMissingUpdateFieldsAndInvalidTags()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var missingFieldsException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSourceAsync(new UpdateSourceRequest("source1", null, null, null, null, null)));
        var blankTagException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateNoteAsync(new UpdateNoteRequest("note1", null, [""])));
        var duplicateTagException = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateCitationAsync(new UpdateCitationRequest("citation1", null, null, ["tag1", "tag1"])));
        var missingTagException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateEventAsync(new UpdateEventRequest("event1", null, ["missing"])));

        Assert.Contains("Supply", missingFieldsException.Message);
        Assert.Contains("must not be empty", blankTagException.Message);
        Assert.Contains("Duplicate", duplicateTagException.Message);
        Assert.Contains("Unknown tag", missingTagException.Message);
    }

    [Fact]
    public async Task ObjectUpdatesRejectMissingTarget()
    {
        using var database = new TestDatabase();
        var service = CreateObjectWriteService(database, allowWrites: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateNoteAsync(new UpdateNoteRequest("missing", "Updated", null)));

        Assert.Contains("not found", exception.Message);
    }

    private static MediaWriteService CreateMediaWriteService(TestDatabase database, bool allowWrites)
    {
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
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

    private static ObjectWriteService CreateObjectWriteService(TestDatabase database, bool allowWrites)
    {
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = new GrampsDatabasePaths(database.MediaPath, database.SavePath);
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        return new ObjectWriteService(
            new WriteGuard(new RuntimeOptions(database.WriteConfig(), allowWrites)),
            new SingleWriterLock(),
            new BackupService(config, paths),
            new GrampsConnectionFactory(config),
            repository);
    }

    private static string? ReadJsonValue(TestDatabase database, string tableName, string handle, string jsonPath)
    {
        using var connection =
            new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT json_extract(json_data, $jsonPath) FROM {tableName} WHERE handle = $handle";
        command.Parameters.AddWithValue("$jsonPath", jsonPath);
        command.Parameters.AddWithValue("$handle", handle);
        return command.ExecuteScalar() as string;
    }
}