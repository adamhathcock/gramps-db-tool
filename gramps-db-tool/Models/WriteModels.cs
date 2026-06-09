namespace GrampsDbTool.Models;

public sealed record UpdateMediaRequest(
    string MediaHandle,
    string? NewPath,
    bool ConvertToRelative,
    IReadOnlyList<string>? TagHandles
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
