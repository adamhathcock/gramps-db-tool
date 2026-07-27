namespace GrampsDbTool.Models;

public sealed record PageDto<T>(
    IReadOnlyList<T> Items,
    int Limit,
    int Offset,
    int ReturnedCount,
    int TotalCount,
    bool HasMore,
    int? NextOffset
);

public sealed record LookupResultDto<T>(
    IReadOnlyList<T> Items,
    string LookupBy,
    IReadOnlyList<string> MissingValues
)
{
    public static LookupResultDto<T> Create(IReadOnlyList<T> items, IReadOnlyList<string>? handles,
        IReadOnlyList<string>? alternateValues, string alternateLookupName, Func<T, string> handleSelector,
        Func<T, string?> alternateValueSelector)
    {
        var requested = handles ?? alternateValues ?? [];
        var found = handles is not null
            ? items.Select(handleSelector).ToHashSet(StringComparer.Ordinal)
            : items.Select(alternateValueSelector).OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        return new LookupResultDto<T>(items, handles is not null ? "handle" : alternateLookupName,
            requested.Where(value => !found.Contains(value)).ToArray());
    }
}

public sealed record ObjectSummaryDto(
    string ObjectType,
    string Handle,
    string? GrampsId,
    string Label,
    long? Change,
    bool? Private
);

public sealed record BacklinkDto(
    string ObjectType,
    string Handle,
    string? GrampsId,
    string Label
);

public sealed record GrampsTypeDto(
    int? Value,
    string? CustomName
);

public sealed record DateDto(
    int? Calendar,
    int? Modifier,
    int? Quality,
    int? Day,
    int? Month,
    int? Year,
    bool? IsSlash,
    int? RangeDay,
    int? RangeMonth,
    int? RangeYear,
    bool? RangeIsSlash,
    string? Text,
    int? SortValue,
    int? NewYear,
    int? CustomNewYearMonth,
    int? CustomNewYearDay
);

public sealed record EventRefDto(
    string Handle,
    GrampsTypeDto? Role,
    bool? Private,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> NoteHandles
);

public sealed record ChildRefDto(
    string Handle,
    GrampsTypeDto? FatherRelationship,
    GrampsTypeDto? MotherRelationship,
    bool? Private,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> NoteHandles
);

public sealed record MediaRefDto(
    string Handle,
    IReadOnlyList<int>? Rectangle,
    bool? Private,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> NoteHandles
);

public sealed record PersonRefDto(
    string Handle,
    string? Relationship,
    bool? Private,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> NoteHandles
);

public sealed record PlaceRefDto(
    string Handle,
    DateDto? Date
);

public sealed record RepositoryRefDto(
    string Handle,
    string? CallNumber,
    GrampsTypeDto? MediaType
);

public sealed record TextRangeDto(int Start, int End);

public sealed record NoteLinkDto(
    string Domain,
    string ObjectType,
    string Property,
    string Value,
    IReadOnlyList<TextRangeDto> Ranges
);

public sealed record PersonSearchResultDto(
    string Handle,
    string? GrampsId,
    string DisplayName,
    int? Gender,
    long? Change,
    bool? Private
);

public sealed record PersonDto(
    string Handle,
    string? GrampsId,
    string DisplayName,
    int? Gender,
    int? BirthEventIndex,
    int? DeathEventIndex,
    IReadOnlyList<EventRefDto> Events,
    IReadOnlyList<string> FamilyHandles,
    IReadOnlyList<string> ParentFamilyHandles,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> TagHandles,
    IReadOnlyList<PersonRefDto> PersonReferences,
    long? Change,
    bool? Private
);

public sealed record MediaDto(
    string Handle,
    string? GrampsId,
    string? Path,
    string? ResolvedPath,
    string? Mime,
    string? Description,
    string? Checksum,
    DateDto? Date,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record NoteDto(
    string Handle,
    string? GrampsId,
    string Text,
    int? Format,
    GrampsTypeDto? Type,
    IReadOnlyList<NoteLinkDto> Links,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record CitationDto(
    string Handle,
    string? GrampsId,
    DateDto? Date,
    string? Page,
    int? Confidence,
    string? SourceHandle,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record FamilyDto(
    string Handle,
    string? GrampsId,
    string? FatherHandle,
    string? MotherHandle,
    GrampsTypeDto? Type,
    IReadOnlyList<ChildRefDto> Children,
    IReadOnlyList<EventRefDto> Events,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record EventDto(
    string Handle,
    string? GrampsId,
    GrampsTypeDto? Type,
    DateDto? Date,
    string? Description,
    string? PlaceHandle,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record SourceDto(
    string Handle,
    string? GrampsId,
    string? Title,
    string? Author,
    string? PublicationInfo,
    string? Abbreviation,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<RepositoryRefDto> Repositories,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record PlaceDto(
    string Handle,
    string? GrampsId,
    string? Title,
    string? Longitude,
    string? Latitude,
    string? PrimaryName,
    GrampsTypeDto? Type,
    IReadOnlyList<PlaceRefDto> ParentPlaces,
    IReadOnlyList<MediaRefDto> Media,
    IReadOnlyList<string> CitationHandles,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record RepositoryDto(
    string Handle,
    string? GrampsId,
    GrampsTypeDto? Type,
    string? Name,
    IReadOnlyList<string> NoteHandles,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);

public sealed record TagDto(
    string Handle,
    string Name,
    string? Color,
    int? Priority,
    long? Change
);

public sealed record TaggedObjectDto(
    string ObjectType,
    string Handle,
    string? GrampsId,
    string Label,
    IReadOnlyList<string> TagHandles,
    long? Change,
    bool? Private
);