using System.Text.Json;
using GrampsDbTool.Configuration;
using GrampsDbTool.Models;

namespace GrampsDbTool.Safety;

public sealed class AuditLogService(GrampsConfig config)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var auditLogPath = Path.Combine(config.ConfigDirectory, "gramps-db-tool.audit.jsonl");
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(auditLogPath, line, cancellationToken);
    }
}
