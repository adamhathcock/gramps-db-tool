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
        var reader = new GrampsMetadataReader(new GrampsConfig(database.DirectoryPath, database.DatabasePath,null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadDatabasePathsAsync());
        Assert.Contains("media-path", exception.Message);
    }

    [Fact]
    public async Task RepositoryMapsReadOnlyObjects()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var people = await repository.SearchPeopleAsync("Ada");
        var firstPage = await repository.SearchPeopleAsync(string.Empty, limit: 1);
        var secondPage = await repository.SearchPeopleAsync(string.Empty, limit: 1, offset: 1);
        var person = await repository.GetPersonAsync("person1", null);
        var family = await repository.GetFamilyAsync("family1", null);
        var @event = await repository.GetEventAsync("event1", null);
        var source = await repository.GetSourceAsync("source1", null);
        var media = await repository.GetMediaAsync("media1", null);
        var mediaBatch = await repository.GetMediaAsync(["media2", "missing", "media1"]);
        var mediaBatchByGrampsId = await repository.GetMediaAsync(null, ["O0002", "missing", "O0001"]);
        var note = await repository.GetNoteAsync("note1", null);
        var citation = await repository.GetCitationAsync("citation1", null);

        Assert.Single(people);
        Assert.Equal("Charles Babbage", Assert.Single(firstPage).DisplayName);
        Assert.Equal("Ada Lovelace", Assert.Single(secondPage).DisplayName);
        Assert.Equal("Ada Lovelace", person?.DisplayName);
        Assert.Equal("media1", Assert.Single(person!.MediaHandles));
        Assert.Equal("person3", Assert.Single(family!.ChildHandles));
        Assert.Equal("Birth", @event?.Type);
        Assert.Equal("Register", source?.Title);
        Assert.Equal(Path.Combine(database.MediaPath, "photos/ada.jpg"), media?.ResolvedPath);
        Assert.Collection(mediaBatch,
            item => Assert.Equal("media2", item.Handle),
            item => Assert.Equal("media1", item.Handle));
        Assert.Collection(mediaBatchByGrampsId,
            item => Assert.Equal("media2", item.Handle),
            item => Assert.Equal("media1", item.Handle));
        Assert.Equal("A note", note?.Text);
        Assert.Equal("source1", citation?.SourceHandle);
        Assert.Equal(["tag1"], person!.TagHandles);
        Assert.Equal(["tag2"], family!.TagHandles);
        Assert.Equal(["tag1"], @event!.TagHandles);
        Assert.Equal(["tag2"], source!.TagHandles);
        Assert.Equal(["tag1", "tag2"], media!.TagHandles);
        Assert.Equal(["tag2"], note!.TagHandles);
        Assert.Equal(["tag1"], citation!.TagHandles);
    }

    [Fact]
    public async Task RepositoryListsAndGetsTags()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var tags = await repository.ListTagsAsync();
        var tagsByHandle = await repository.GetTagsAsync(["tag2", "missing", "tag1"]);
        var tagsByName = await repository.GetTagsAsync(null, ["Missing Media", "missing", "Needs Review"]);

        Assert.Collection(tags,
            tag => Assert.Equal("Needs Review", tag.Name),
            tag => Assert.Equal("Missing Media", tag.Name));
        Assert.Collection(tagsByHandle,
            tag => Assert.Equal("tag2", tag.Handle),
            tag => Assert.Equal("tag1", tag.Handle));
        Assert.Collection(tagsByName,
            tag => Assert.Equal("tag2", tag.Handle),
            tag => Assert.Equal("tag1", tag.Handle));
    }

    [Fact]
    public async Task RepositoryFindsObjectsByTag()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var taggedObjects = await repository.FindObjectsByTagAsync("tag1", null);
        var mediaOnly = await repository.FindObjectsByTagAsync(null, "Needs Review", ["media"]);

        Assert.Contains(taggedObjects, item => item.ObjectType == "person" && item.Handle == "person1");
        Assert.Contains(taggedObjects, item => item.ObjectType == "event" && item.Handle == "event1");
        Assert.Contains(taggedObjects, item => item.ObjectType == "place" && item.Handle == "place1");
        Assert.Contains(taggedObjects, item => item.ObjectType == "citation" && item.Handle == "citation1");
        Assert.Contains(taggedObjects, item => item.ObjectType == "media" && item.Handle == "media1" && item.TagHandles.SequenceEqual(["tag1", "tag2"]));
        Assert.Single(mediaOnly);
        Assert.Equal("media1", mediaOnly[0].Handle);
    }

    [Fact]
    public async Task RepositoryRejectsTooManyMediaHandles()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var handles = Enumerable.Range(0, 101).Select(index => $"media{index}").ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(handles));
        Assert.Contains("At most 100", exception.Message);
    }

    [Fact]
    public async Task RepositoryRejectsInvalidMediaLookupArguments()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var bothException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(["media1"], ["O0001"]));
        var neitherException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync((IReadOnlyList<string>?)null, null));
        var emptyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(null, [""]));
        var tooManyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(null, Enumerable.Range(0, 101).Select(index => $"O{index:0000}").ToArray()));

        Assert.Contains("either", bothException.Message);
        Assert.Contains("either", neitherException.Message);
        Assert.Contains("must not be empty", emptyException.Message);
        Assert.Contains("At most 100", tooManyException.Message);
    }

    [Fact]
    public async Task RepositoryRejectsInvalidTagLookupArguments()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath,null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var bothException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(["tag1"], ["Needs Review"]));
        var neitherException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(null, null));
        var emptyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(null, [""]));
        var tooManyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(Enumerable.Range(0, 101).Select(index => $"tag{index}").ToArray()));

        Assert.Contains("either", bothException.Message);
        Assert.Contains("either", neitherException.Message);
        Assert.Contains("must not be empty", emptyException.Message);
        Assert.Contains("At most 100", tooManyException.Message);
    }
}
