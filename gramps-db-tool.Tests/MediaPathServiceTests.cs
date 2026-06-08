using GrampsDbTool.Configuration;
using GrampsDbTool.Services;

namespace GrampsDbTool.Tests;

public sealed class MediaPathServiceTests
{
    [Fact]
    public void ResolvePathRejectsTraversalOutsideMediaRoot()
    {
        using var database = new TestDatabase();
        var service = new MediaPathService(new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var exception = Assert.Throws<ArgumentException>(() => service.ResolvePath("../outside.jpg"));
        Assert.Contains("inside", exception.Message);
    }

    [Fact]
    public void ResolvePathRejectsAbsoluteFilePath()
    {
        using var database = new TestDatabase();
        var service = new MediaPathService(new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var exception = Assert.Throws<ArgumentException>(() => service.ResolvePath(Path.Combine(database.MediaPath, "photo.jpg")));
        Assert.Contains("Absolute", exception.Message);
    }

    [Fact]
    public void ResolvePathResolvesRelativePathAgainstMediaRoot()
    {
        using var database = new TestDatabase();
        var service = new MediaPathService(new GrampsDatabasePaths(database.MediaPath, database.SavePath));

        var resolved = service.ResolvePath("photos/ada.jpg");

        Assert.Equal(Path.Combine(database.MediaPath, "photos/ada.jpg"), resolved);
    }
}
