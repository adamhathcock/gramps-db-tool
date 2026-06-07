using System.Text.Json.Nodes;

namespace GrampsDbTool.Models;

public sealed record DatabaseBackupResult(
    string SourcePath,
    string BackupPath,
    long FileSizeBytes,
    DateTimeOffset CreatedAt);

public sealed record RecordUpdateResult(
    string ObjectType,
    string Handle,
    bool DryRun,
    long? OldChange,
    long? NewChange,
    IReadOnlyList<string> UpdatedColumns,
    int ReferencesUpdated,
    JsonObject Record);
