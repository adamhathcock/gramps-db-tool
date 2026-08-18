using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Services;

namespace GrampsDbTool.Tests;

public sealed class FanChartExportTests
{
    [Fact]
    public async Task ExportProducesWebtreesFanChartFamilyJson()
    {
        using var database = new TestDatabase(includeFanChartData: true);
        var service = await CreateServiceAsync(database);

        var export = await service.ExportAsync();

        Assert.Equal("I0001", export.Config.DefaultXref);
        Assert.Equal(6, export.Config.Generations);
        Assert.True(export.Config.ShowDescendants);
        Assert.Equal(["I0001", "I0002", "I0003"], export.People.Keys);
        Assert.Equal(["F0001"], export.Families.Keys);

        var ada = export.People["I0001"];
        Assert.Equal("M", ada.Sex);
        Assert.Equal("Ada Lovelace", ada.Name);
        Assert.Equal(["Ada"], ada.FirstNames);
        Assert.Equal(["Lovelace"], ada.LastNames);
        Assert.Equal("Ada", ada.PreferredName);
        Assert.Equal("1815-12-10", ada.Birth?.Date);
        Assert.Equal("London", ada.Birth?.Place);
        Assert.Equal("1852-02-09", ada.Death?.Date);
        Assert.Equal(Path.Combine(database.MediaPath, "photos", "ada.jpg"), ada.Image);
        Assert.Equal(["F0001"], ada.SpouseFamilies);

        var cora = export.People["I0003"];
        Assert.Equal("F", cora.Sex);
        Assert.Equal("F0001", cora.ChildFamily);

        var family = export.Families["F0001"];
        Assert.Equal("I0002", family.Husband);
        Assert.Equal("I0001", family.Wife);
        Assert.Equal(["I0003"], family.Children);
        Assert.Equal("1810-01-05", family.Marriage?.Date);
        Assert.Equal("London", family.Marriage?.Place);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(export));
        var root = document.RootElement;
        Assert.Equal("I0001", root.GetProperty("config").GetProperty("defaultXref").GetString());
        var person = root.GetProperty("people").GetProperty("I0001");
        Assert.Equal("Ada Lovelace", person.GetProperty("name").GetString());
        Assert.False(person.TryGetProperty("private", out _));
        var charles = root.GetProperty("people").GetProperty("I0002");
        Assert.False(charles.TryGetProperty("image", out _));
        Assert.False(charles.TryGetProperty("birth", out _));
    }

    [Fact]
    public async Task ExportUsesExplicitDefaultPersonAndRejectsInvalidSelection()
    {
        using var database = new TestDatabase(includeFanChartData: true);
        var service = await CreateServiceAsync(database);

        var byGrampsId = await service.ExportAsync(defaultPersonGrampsId: "I0003");
        var byHandle = await service.ExportAsync(defaultPersonHandle: "person2");
        var missing = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportAsync(defaultPersonGrampsId: "missing"));
        var both = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportAsync("person1", "I0001"));

        Assert.Equal("I0003", byGrampsId.Config.DefaultXref);
        Assert.Equal("I0002", byHandle.Config.DefaultXref);
        Assert.Contains("missing", missing.Message);
        Assert.Contains("either", both.Message);
    }

    private static async Task<FanChartExportService> CreateServiceAsync(TestDatabase database)
    {
        var config = new GrampsConfig(database.DirectoryPath, database.DatabasePath, null);
        var paths = await new GrampsMetadataReader(config).ReadDatabasePathsAsync();
        return new FanChartExportService(new GrampsConnectionFactory(config), new MediaPathService(paths));
    }
}
