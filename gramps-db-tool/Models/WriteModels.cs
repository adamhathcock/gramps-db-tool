namespace GrampsDbTool.Models;

public sealed record UpdateMediaRequest(
    string MediaHandle,
    string? NewPath,
    bool ConvertToRelative,
    IReadOnlyList<string>? TagHandles
);

public sealed record UpdateNoteRequest(
    string NoteHandle,
    string? NewText,
    IReadOnlyList<string>? TagHandles
);

public sealed record UpdateCitationRequest(
    string CitationHandle,
    string? Page,
    int? Confidence,
    IReadOnlyList<string>? TagHandles
);

public sealed record UpdateEventRequest(
    string EventHandle,
    string? Description,
    IReadOnlyList<string>? TagHandles
);

public sealed record UpdateSourceRequest(
    string SourceHandle,
    string? Title,
    string? Author,
    string? Pubinfo,
    string? Abbrev,
    IReadOnlyList<string>? TagHandles
);