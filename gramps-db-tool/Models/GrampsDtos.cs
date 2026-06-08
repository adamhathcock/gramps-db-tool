namespace GrampsDbTool.Models;

public sealed record PersonSearchResultDto(
    string Handle,
    string? GrampsId,
    string DisplayName,
    int? Gender
);

public sealed record PersonDto(
    string Handle,
    string? GrampsId,
    string DisplayName,
    int? Gender,
    IReadOnlyList<string> EventHandles,
    IReadOnlyList<string> FamilyHandles,
    IReadOnlyList<string> ParentFamilyHandles,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles
);

public sealed record MediaDto(
    string Handle,
    string? GrampsId,
    string? Path,
    string? ResolvedPath,
    string? Mime,
    string? Description,
    string? Checksum,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles
);

public sealed record NoteDto(
    string Handle,
    string? GrampsId,
    string Text,
    int? Format,
    string? Type
);

public sealed record CitationDto(
    string Handle,
    string? GrampsId,
    string? Page,
    int? Confidence,
    string? SourceHandle,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> MediaHandles
);
