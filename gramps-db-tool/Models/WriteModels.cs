namespace GrampsDbTool.Models;

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
