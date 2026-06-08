using GrampsDbTool.Configuration;

namespace GrampsDbTool.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void LoadRuntimeOptionsRejectsDatabaseArgument()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ConfigLoader.LoadRuntimeOptions(["--database", "db.sqlite"]));
        Assert.Contains("databasePath", exception.Message);
    }

    [Fact]
    public void LoadConfigRejectsAllowWritesInConfig()
    {
        using var database = new TestDatabase();
        var configPath = database.WriteConfig("""
            {
              "databasePath": "sqlite.db",
              "allowWrites": true
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => ConfigLoader.LoadConfig(configPath));
        Assert.Contains("allowWrites", exception.Message);
    }

    [Fact]
    public void LoadConfigResolvesRelativeDatabasePathAgainstConfigDirectory()
    {
        using var database = new TestDatabase();
        var configPath = database.WriteConfig("""
            {
              "databasePath": "sqlite.db"
            }
            """);

        var config = ConfigLoader.LoadConfig(configPath);

        Assert.Equal(database.DatabasePath, config.DatabasePath);
    }
}
