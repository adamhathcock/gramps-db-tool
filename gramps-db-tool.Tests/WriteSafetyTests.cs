using GrampsDbTool.Configuration;
using GrampsDbTool.Safety;

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
    public async Task BackupServiceRefusesMissingSavePath()
    {
        using var database = new TestDatabase();
        var service = new BackupService(
            new GrampsConfig(database.DirectoryPath, database.DatabasePath),
            new GrampsDatabasePaths(database.MediaPath, SavePath: null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBackupAsync());
        Assert.Contains("save path", exception.Message);
    }
}
