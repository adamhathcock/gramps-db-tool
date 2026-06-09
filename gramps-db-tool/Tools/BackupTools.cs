using System.ComponentModel;
using GrampsDbTool.Models;
using GrampsDbTool.Safety;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class BackupTools(BackupService backupService, SingleWriterLock singleWriterLock)
{
    [McpServerTool(Name = "create_backup", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Create a compressed zip backup containing the Gramps SQLite database and all files under the database-derived media path. The archive is written to the database-derived save path.")]
    public async Task<BackupArchiveDto> CreateBackup(CancellationToken cancellationToken = default)
    {
        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        return await backupService.CreateArchiveAsync(cancellationToken);
    }
}
