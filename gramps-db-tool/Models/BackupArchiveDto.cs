namespace GrampsDbTool.Models;

public sealed record BackupArchiveDto(
    string BackupPath,
    string DatabaseEntry,
    string MediaRoot,
    int MediaFileCount,
    DateTimeOffset CreatedAtUtc
);
