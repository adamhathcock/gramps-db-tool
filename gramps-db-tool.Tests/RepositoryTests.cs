using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Services;

namespace GrampsDbTool.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task MetadataReaderRequiresMediaPath()
    {
        using var database = new TestDatabase(includeMediaPath: false);
        var reader = new GrampsMetadataReader(new GrampsConfig(database.DirectoryPath, database.DatabasePath));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadDatabasePathsAsync());
        Assert.Contains("media-path", exception.Message);
    }

    [Fact]
    public async Task RepositoryMapsReadOnlyObjects()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var people = await repository.SearchPeopleAsync("Ada");
        var person = await repository.GetPersonAsync("person1", null);
        var family = await repository.GetFamilyAsync("family1", null);
        var @event = await repository.GetEventAsync("event1", null);
        var source = await repository.GetSourceAsync("source1", null);
        var media = await repository.GetMediaAsync("media1", null);
        var note = await repository.GetNoteAsync("note1", null);
        var citation = await repository.GetCitationAsync("citation1", null);

        Assert.Single(people);
        Assert.Equal("Ada Lovelace", person?.DisplayName);
        Assert.Equal("person3", Assert.Single(family!.ChildHandles));
        Assert.Equal("Birth", @event?.Type);
        Assert.Equal("Register", source?.Title);
        Assert.Equal(Path.Combine(database.MediaPath, "photos/ada.jpg"), media?.ResolvedPath);
        Assert.Equal("A note", note?.Text);
        Assert.Equal("source1", citation?.SourceHandle);
    }
}
