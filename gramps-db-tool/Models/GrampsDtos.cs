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

public sealed record FamilyDto(
    string Handle,
    string? GrampsId,
    string? FatherHandle,
    string? MotherHandle,
    string? Type,
    IReadOnlyList<string> ChildHandles,
    IReadOnlyList<string> EventHandles,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles
);

public sealed record EventDto(
    string Handle,
    string? GrampsId,
    string? Type,
    string? Description,
    string? PlaceHandle,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> MediaHandles
);

public sealed record SourceDto(
    string Handle,
    string? GrampsId,
    string? Title,
    string? Author,
    string? PublicationInfo,
    string? Abbreviation,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> MediaHandles
);
