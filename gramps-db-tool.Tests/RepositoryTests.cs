using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Services;
using GrampsDbTool.Tools;

namespace GrampsDbTool.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task MetadataReaderRequiresMediaPath()
    {
        using var database = new TestDatabase(includeMediaPath: false);
        var reader = new GrampsMetadataReader(new GrampsConfig(database.DirectoryPath, database.DatabasePath, null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadDatabasePathsAsync());
        Assert.Contains("media-path", exception.Message);
    }

    [Fact]
    public async Task RepositoryMapsReadOnlyObjects()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var people = await repository.SearchPeopleAsync("Ada");
        var firstPage = await repository.SearchPeopleAsync(string.Empty, limit: 1);
        var secondPage = await repository.SearchPeopleAsync(string.Empty, limit: 1, offset: 1);
        var person = await repository.GetPersonAsync("person1", null);
        var family = await repository.GetFamilyAsync("family1", null);
        var @event = await repository.GetEventAsync("event1", null);
        var source = await repository.GetSourceAsync("source1", null);
        var place = await repository.GetPlaceAsync(["place1"]);
        var media = await repository.GetMediaAsync("media1", null);
        var mediaBatch = await repository.GetMediaAsync(["media2", "missing", "media1"]);
        var mediaBatchByGrampsId = await repository.GetMediaAsync(null, ["O0002", "missing", "O0001"]);
        var note = await repository.GetNoteAsync("note1", null);
        var noteBatch = await repository.GetNoteAsync(["note1", "missing"]);
        var noteBatchByGrampsId = await repository.GetNoteAsync(null, ["N0001", "missing"]);
        var citation = await repository.GetCitationAsync("citation1", null);
        var citationBatch = await repository.GetCitationAsync(["citation1", "missing"]);
        var citationBatchByGrampsId = await repository.GetCitationAsync(null, ["C0001", "missing"]);
        var eventBatch = await repository.GetEventAsync(["event1", "missing"]);
        var eventBatchByGrampsId = await repository.GetEventAsync(null, ["E0001", "missing"]);
        var sourceBatch = await repository.GetSourceAsync(["source1", "missing"]);
        var sourceBatchByGrampsId = await repository.GetSourceAsync(null, ["S0001", "missing"]);

        Assert.Single(people.Items);
        Assert.Equal(1, people.TotalCount);
        Assert.Equal("Charles Babbage", Assert.Single(firstPage.Items).DisplayName);
        Assert.True(firstPage.HasMore);
        Assert.Equal(1, firstPage.NextOffset);
        Assert.Equal("Ada Lovelace", Assert.Single(secondPage.Items).DisplayName);
        Assert.Equal("Ada Lovelace", person?.DisplayName);
        Assert.Equal("media1", Assert.Single(person!.Media).Handle);
        Assert.Equal("person3", Assert.Single(family!.Children).Handle);
        Assert.Equal("Birth", @event?.Type?.CustomName);
        Assert.Equal(1815, @event?.Date?.Year);
        Assert.Equal("Register", source?.Title);
        Assert.Equal("London", Assert.Single(place).Title);
        Assert.Equal("place2", Assert.Single(Assert.Single(place).ParentPlaces).Handle);
        Assert.Equal(3, Assert.Single(Assert.Single(place).ParentPlaces).Date?.CustomNewYearMonth);
        Assert.Equal(25, Assert.Single(Assert.Single(place).ParentPlaces).Date?.CustomNewYearDay);
        Assert.Equal("City", Assert.Single(place).Type?.CustomName);
        Assert.Equal("-0.1276", Assert.Single(place).Longitude);
        Assert.Equal("51.5072", Assert.Single(place).Latitude);
        Assert.Equal("London", Assert.Single(place).PrimaryName);
        Assert.Equal("media1", Assert.Single(Assert.Single(place).Media).Handle);
        Assert.Equal(["citation1"], Assert.Single(place).CitationHandles);
        Assert.Equal(["note1"], Assert.Single(place).NoteHandles);
        Assert.Equal(Path.Combine(database.MediaPath, "photos/ada.jpg"), media?.ResolvedPath);
        Assert.Collection(mediaBatch,
            item => Assert.Equal("media2", item.Handle),
            item => Assert.Equal("media1", item.Handle));
        Assert.Collection(mediaBatchByGrampsId,
            item => Assert.Equal("media2", item.Handle),
            item => Assert.Equal("media1", item.Handle));
        Assert.Equal("A note", note?.Text);
        Assert.Equal("note1", Assert.Single(noteBatch).Handle);
        Assert.Equal("note1", Assert.Single(noteBatchByGrampsId).Handle);
        Assert.Equal("source1", citation?.SourceHandle);
        Assert.Equal(1843, citation?.Date?.Year);
        Assert.Equal("citation1", Assert.Single(citationBatch).Handle);
        Assert.Equal("citation1", Assert.Single(citationBatchByGrampsId).Handle);
        Assert.Equal("event1", Assert.Single(eventBatch).Handle);
        Assert.Equal("event1", Assert.Single(eventBatchByGrampsId).Handle);
        Assert.Equal("source1", Assert.Single(sourceBatch).Handle);
        Assert.Equal("source1", Assert.Single(sourceBatchByGrampsId).Handle);
        Assert.Equal(["tag1"], person.TagHandles);
        Assert.Equal(["tag2"], family.TagHandles);
        Assert.Equal(["tag1"], @event!.TagHandles);
        Assert.Equal(["tag2"], source!.TagHandles);
        Assert.Equal(["tag1", "tag2"], media!.TagHandles);
        Assert.Equal(["tag2"], note!.TagHandles);
        Assert.Equal(["tag1"], citation!.TagHandles);
        Assert.True(person.Private);
        Assert.Equal("Primary", Assert.Single(person.Events).Role?.CustomName);
        Assert.Equal("person2", Assert.Single(person.PersonReferences).Handle);
        Assert.Equal("media1", Assert.Single(family.Media).Handle);
        Assert.Equal("repo1", Assert.Single(source.Repositories).Handle);
        Assert.Equal("person1", Assert.Single(note.Links).Value);
    }

    [Fact]
    public async Task RepositoryListsAndGetsTags()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var tags = await repository.ListTagsAsync();
        var tagsByHandle = await repository.GetTagsAsync(["tag2", "missing", "tag1"]);
        var tagsByName = await repository.GetTagsAsync(null, ["Missing Media", "missing", "Needs Review"]);

        Assert.Equal(2, tags.TotalCount);
        Assert.Collection(tags.Items,
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
    public async Task RepositoryGetsReadOnlyObjectsInBatches()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var events = await repository.GetEventAsync(["event1", "missing"]);
        var eventsByGrampsId = await repository.GetEventAsync(null, ["E0001", "missing"]);
        var sources = await repository.GetSourceAsync(["source1", "missing"]);
        var sourcesByGrampsId = await repository.GetSourceAsync(null, ["S0001", "missing"]);
        var places = await repository.GetPlaceAsync(["place1", "missing"]);
        var placesByGrampsId = await repository.GetPlaceAsync(null, ["P0001", "missing"]);
        var notes = await repository.GetNoteAsync(["note1", "missing"]);
        var notesByGrampsId = await repository.GetNoteAsync(null, ["N0001", "missing"]);
        var citations = await repository.GetCitationAsync(["citation1", "missing"]);
        var citationsByGrampsId = await repository.GetCitationAsync(null, ["C0001", "missing"]);

        Assert.Equal("event1", Assert.Single(events).Handle);
        Assert.Equal("event1", Assert.Single(eventsByGrampsId).Handle);
        Assert.Equal("source1", Assert.Single(sources).Handle);
        Assert.Equal("source1", Assert.Single(sourcesByGrampsId).Handle);
        Assert.Equal("place1", Assert.Single(places).Handle);
        Assert.Equal("place1", Assert.Single(placesByGrampsId).Handle);
        Assert.Equal("note1", Assert.Single(notes).Handle);
        Assert.Equal("note1", Assert.Single(notesByGrampsId).Handle);
        Assert.Equal("citation1", Assert.Single(citations).Handle);
        Assert.Equal("citation1", Assert.Single(citationsByGrampsId).Handle);
    }

    [Fact]
    public async Task RepositoryFindsObjectsByTag()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var taggedObjects = await repository.FindObjectsByTagAsync("tag1", null);
        var mediaOnly = await repository.FindObjectsByTagAsync(null, "Needs Review", ["media"]);

        Assert.Contains(taggedObjects.Items, item => item.ObjectType == "person" && item.Handle == "person1");
        Assert.Contains(taggedObjects.Items, item => item.ObjectType == "event" && item.Handle == "event1");
        Assert.Contains(taggedObjects.Items, item => item.ObjectType == "place" && item.Handle == "place1");
        Assert.Contains(taggedObjects.Items, item => item.ObjectType == "citation" && item.Handle == "citation1");
        Assert.Contains(taggedObjects.Items,
            item => item.ObjectType == "media" && item.Handle == "media1" &&
                    item.TagHandles.SequenceEqual(["tag1", "tag2"]));
        Assert.Single(mediaOnly.Items);
        Assert.Equal("media1", mediaOnly.Items[0].Handle);
    }

    [Fact]
    public async Task RepositoryListsEveryPrimaryObjectTypeWithPaging()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var firstPage = await repository.ListObjectsAsync(limit: 3);
        var repositoryPage = await repository.ListObjectsAsync("Archive", ["repository"]);
        var repositories = await repository.GetRepositoryAsync(["repo1"]);

        Assert.Equal(14, firstPage.TotalCount);
        Assert.Equal(3, firstPage.ReturnedCount);
        Assert.True(firstPage.HasMore);
        Assert.Equal(3, firstPage.NextOffset);
        var repositorySummary = Assert.Single(repositoryPage.Items);
        Assert.Equal("repository", repositorySummary.ObjectType);
        Assert.Equal("repo1", repositorySummary.Handle);
        var repositoryDto = Assert.Single(repositories);
        Assert.Equal("Archive", repositoryDto.Name);
        Assert.Equal("Archive", repositoryDto.Type?.CustomName);
        Assert.Equal(["note1"], repositoryDto.NoteHandles);
    }

    [Fact]
    public async Task RepositoryFindsPagedBacklinksAndAcceptsDuplicateFilters()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var backlinks = await repository.FindBacklinksAsync("media1", limit: 1);
        var people = await repository.FindBacklinksAsync("media1", ["person", "person"]);

        Assert.Equal(2, backlinks.TotalCount);
        Assert.Single(backlinks.Items);
        Assert.True(backlinks.HasMore);
        var person = Assert.Single(people.Items);
        Assert.Equal("person1", person.Handle);
    }

    [Fact]
    public async Task BatchToolReportsMissingLookupValues()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));
        var tools = new RepositoryTools(repository);

        var result = await tools.GetRepository(["repo1", "missing"]);

        Assert.Equal("handle", result.LookupBy);
        Assert.Equal("repo1", Assert.Single(result.Items).Handle);
        Assert.Equal(["missing"], result.MissingValues);
    }

    [Fact]
    public async Task RepositoryRejectsTooManyMediaHandles()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
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
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var bothException =
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(["media1"], ["O0001"]));
        var neitherException =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                repository.GetMediaAsync(null));
        var emptyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMediaAsync(null, [""]));
        var tooManyException = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetMediaAsync(null, Enumerable.Range(0, 101).Select(index => $"O{index:0000}").ToArray()));

        Assert.Contains("either", bothException.Message);
        Assert.Contains("either", neitherException.Message);
        Assert.Contains("must not be empty", emptyException.Message);
        Assert.Contains("At most 100", tooManyException.Message);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("source")]
    [InlineData("place")]
    [InlineData("note")]
    [InlineData("citation")]
    public async Task RepositoryRejectsInvalidBatchLookupArguments(string objectType)
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var bothException =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                GetBatchAsync(repository, objectType, ["handle1"], ["ID0001"]));
        var neitherException =
            await Assert.ThrowsAsync<ArgumentException>(() => GetBatchAsync(repository, objectType, null, null));
        var emptyException =
            await Assert.ThrowsAsync<ArgumentException>(() => GetBatchAsync(repository, objectType, null, [""]));
        var tooManyException = await Assert.ThrowsAsync<ArgumentException>(() => GetBatchAsync(repository, objectType,
            Enumerable.Range(0, 101).Select(index => $"handle{index}").ToArray(), null));

        Assert.Contains("either", bothException.Message);
        Assert.Contains("either", neitherException.Message);
        Assert.Contains("must not be empty", emptyException.Message);
        Assert.Contains("At most 100", tooManyException.Message);
    }

    [Fact]
    public async Task RepositoryRejectsInvalidTagLookupArguments()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        var bothException =
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(["tag1"], ["Needs Review"]));
        var neitherException = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetTagsAsync(null));
        var emptyException = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetTagsAsync(null, [""]));
        var tooManyException = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetTagsAsync(Enumerable.Range(0, 101).Select(index => $"tag{index}").ToArray()));

        Assert.Contains("either", bothException.Message);
        Assert.Contains("either", neitherException.Message);
        Assert.Contains("must not be empty", emptyException.Message);
        Assert.Contains("At most 100", tooManyException.Message);
    }

    [Fact]
    public async Task RepositoryRejectsInvalidPagingArguments()
    {
        using var database = new TestDatabase();
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        var repository = new GrampsRepository(config, new MediaPathService(paths));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ListObjectsAsync(limit: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ListTagsAsync(offset: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SearchPeopleAsync("", limit: 501));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.FindBacklinksAsync("media1", offset: -1));
    }

    private static Task GetBatchAsync(GrampsRepository repository, string objectType, IReadOnlyList<string>? handles,
        IReadOnlyList<string>? grampsIds)
    {
        return objectType switch
        {
            "event" => repository.GetEventAsync(handles, grampsIds),
            "source" => repository.GetSourceAsync(handles, grampsIds),
            "place" => repository.GetPlaceAsync(handles, grampsIds),
            "note" => repository.GetNoteAsync(handles, grampsIds),
            "citation" => repository.GetCitationAsync(handles, grampsIds),
            _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null)
        };
    }
}
