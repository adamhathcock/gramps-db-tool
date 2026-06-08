namespace GrampsDbTool.Models;

public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string ToolName,
    string ObjectType,
    string Handle,
    string BeforeJson,
    string AfterJson
);

public sealed record UpdateMediaPathRequest(
    string MediaHandle,
    string NewPath,
    bool ConvertToRelative
);

public sealed record UpdateNoteRequest(
    string NoteHandle,
    string NewText
);

public sealed record UpdateCitationRequest(
    string CitationHandle,
    string? Page,
    int? Confidence
);
