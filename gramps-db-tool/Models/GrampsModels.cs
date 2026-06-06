using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrampsDbTool.Models;

public abstract record GrampsObject
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("handle")]
    public string Handle { get; init; } = string.Empty;

    [JsonPropertyName("gramps_id")]
    public string? GrampsId { get; init; }

    [JsonPropertyName("change")]
    public long Change { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("tag_list")]
    public IReadOnlyList<string> TagHandles { get; init; } = [];
}

public sealed record Person : GrampsObject
{
    [JsonPropertyName("gender")]
    public int Gender { get; init; }

    [JsonPropertyName("primary_name")]
    public Name? PrimaryName { get; init; }

    [JsonPropertyName("alternate_names")]
    public IReadOnlyList<Name> AlternateNames { get; init; } = [];

    [JsonPropertyName("death_ref_index")]
    public int DeathRefIndex { get; init; } = -1;

    [JsonPropertyName("birth_ref_index")]
    public int BirthRefIndex { get; init; } = -1;

    [JsonPropertyName("event_ref_list")]
    public IReadOnlyList<EventRef> EventRefs { get; init; } = [];

    [JsonPropertyName("family_list")]
    public IReadOnlyList<string> FamilyHandles { get; init; } = [];

    [JsonPropertyName("parent_family_list")]
    public IReadOnlyList<string> ParentFamilyHandles { get; init; } = [];

    [JsonPropertyName("media_list")]
    public IReadOnlyList<MediaRef> MediaRefs { get; init; } = [];

    [JsonPropertyName("address_list")]
    public IReadOnlyList<Address> Addresses { get; init; } = [];

    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];

    [JsonPropertyName("urls")]
    public IReadOnlyList<Url> Urls { get; init; } = [];

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("person_ref_list")]
    public IReadOnlyList<PersonRef> PersonRefs { get; init; } = [];

    [JsonPropertyName("familysearch_sync")]
    public JsonElement? FamilySearchSync { get; init; }
}

public sealed record Family : GrampsObject
{
    [JsonPropertyName("father_handle")]
    public string? FatherHandle { get; init; }

    [JsonPropertyName("mother_handle")]
    public string? MotherHandle { get; init; }

    [JsonPropertyName("child_ref_list")]
    public IReadOnlyList<ChildRef> ChildRefs { get; init; } = [];

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("complete")]
    public int Complete { get; init; }

    [JsonPropertyName("event_ref_list")]
    public IReadOnlyList<EventRef> EventRefs { get; init; } = [];

    [JsonPropertyName("media_list")]
    public IReadOnlyList<MediaRef> MediaRefs { get; init; } = [];

    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];
}

public sealed record Event : GrampsObject
{
    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }

    [JsonPropertyName("place")]
    public string? PlaceHandle { get; init; }

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("media_list")]
    public IReadOnlyList<MediaRef> MediaRefs { get; init; } = [];

    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];
}

public sealed record Place : GrampsObject
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("long")]
    public string? Longitude { get; init; }

    [JsonPropertyName("lat")]
    public string? Latitude { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("name")]
    public PlaceName? Name { get; init; }

    [JsonPropertyName("alt_names")]
    public IReadOnlyList<PlaceName> AlternateNames { get; init; } = [];

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("placeref_list")]
    public IReadOnlyList<PlaceRef> PlaceRefs { get; init; } = [];

    [JsonPropertyName("location_list")]
    public IReadOnlyList<Location> Locations { get; init; } = [];

    [JsonPropertyName("urls")]
    public IReadOnlyList<Url> Urls { get; init; } = [];

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];
}

public sealed record Source : GrampsObject
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("pubinfo")]
    public string? PublicationInfo { get; init; }

    [JsonPropertyName("abbrev")]
    public string? Abbreviation { get; init; }

    [JsonPropertyName("reporef_list")]
    public IReadOnlyList<RepoRef> RepositoryRefs { get; init; } = [];

    [JsonPropertyName("media_list")]
    public IReadOnlyList<MediaRef> MediaRefs { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];
}

public sealed record Citation : GrampsObject
{
    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }

    [JsonPropertyName("source_handle")]
    public string? SourceHandle { get; init; }

    [JsonPropertyName("page")]
    public string? Page { get; init; }

    [JsonPropertyName("confidence")]
    public int Confidence { get; init; }

    [JsonPropertyName("media_list")]
    public IReadOnlyList<MediaRef> MediaRefs { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];
}

public sealed record Note : GrampsObject
{
    [JsonPropertyName("text")]
    public StyledText? Text { get; init; }

    [JsonPropertyName("format")]
    public int Format { get; init; }

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }
}

public sealed record Media : GrampsObject
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("mime")]
    public string? Mime { get; init; }

    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }
}

public sealed record Repository : GrampsObject
{
    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("address_list")]
    public IReadOnlyList<Address> Addresses { get; init; } = [];

    [JsonPropertyName("urls")]
    public IReadOnlyList<Url> Urls { get; init; } = [];
}

public sealed record Tag
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("handle")]
    public string Handle { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("change")]
    public long Change { get; init; }
}

public sealed record Name
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("surname_list")]
    public IReadOnlyList<Surname> Surnames { get; init; } = [];

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("suffix")]
    public string? Suffix { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("group_as")]
    public string? GroupAs { get; init; }

    [JsonPropertyName("sort_as")]
    public int SortAs { get; init; }

    [JsonPropertyName("display_as")]
    public int DisplayAs { get; init; }

    [JsonPropertyName("call")]
    public string? Call { get; init; }

    [JsonPropertyName("nick")]
    public string? Nickname { get; init; }

    [JsonPropertyName("famnick")]
    public string? FamilyNickname { get; init; }
}

public sealed record Surname
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("surname")]
    public string? Value { get; init; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    [JsonPropertyName("primary")]
    public bool Primary { get; init; }

    [JsonPropertyName("origintype")]
    public GrampsType? OriginType { get; init; }

    [JsonPropertyName("connector")]
    public string? Connector { get; init; }
}

public sealed record EventRef : ReferenceBase
{
    [JsonPropertyName("role")]
    public GrampsType? Role { get; init; }
}

public sealed record ChildRef : ReferenceBase
{
    [JsonPropertyName("frel")]
    public GrampsType? FatherRelation { get; init; }

    [JsonPropertyName("mrel")]
    public GrampsType? MotherRelation { get; init; }
}

public sealed record PersonRef : ReferenceBase
{
    [JsonPropertyName("rel")]
    public string? Relation { get; init; }
}

public sealed record MediaRef : ReferenceBase
{
    [JsonPropertyName("attribute_list")]
    public IReadOnlyList<GrampsAttribute> Attributes { get; init; } = [];

    [JsonPropertyName("rect")]
    public IReadOnlyList<int>? Rectangle { get; init; }
}

public sealed record RepoRef : ReferenceBase
{
    [JsonPropertyName("call_number")]
    public string? CallNumber { get; init; }

    [JsonPropertyName("media_type")]
    public GrampsType? MediaType { get; init; }
}

public sealed record PlaceRef : ReferenceBase
{
    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }
}

public abstract record ReferenceBase
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];

    [JsonPropertyName("ref")]
    public string? Ref { get; init; }
}

public sealed record GrampsDate
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("calendar")]
    public int Calendar { get; init; }

    [JsonPropertyName("modifier")]
    public int Modifier { get; init; }

    [JsonPropertyName("quality")]
    public int Quality { get; init; }

    [JsonPropertyName("dateval")]
    public IReadOnlyList<JsonElement> DateValue { get; init; } = [];

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("sortval")]
    public long SortValue { get; init; }

    [JsonPropertyName("newyear")]
    public int NewYear { get; init; }
}

public sealed record GrampsType
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("value")]
    public int Value { get; init; }

    [JsonPropertyName("string")]
    public string? String { get; init; }
}

public sealed record StyledText
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("string")]
    public string? String { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<StyledTextTag> Tags { get; init; } = [];
}

public sealed record StyledTextTag
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("ranges")]
    public IReadOnlyList<IReadOnlyList<int>> Ranges { get; init; } = [];
}

public sealed record GrampsAttribute
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("citation_list")]
    public IReadOnlyList<string> CitationHandles { get; init; } = [];

    [JsonPropertyName("note_list")]
    public IReadOnlyList<string> NoteHandles { get; init; } = [];
}

public sealed record Address
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }

    [JsonPropertyName("street")]
    public string? Street { get; init; }

    [JsonPropertyName("locality")]
    public string? Locality { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("county")]
    public string? County { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("postal")]
    public string? Postal { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }
}

public sealed record Url
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("private")]
    public bool Private { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public GrampsType? Type { get; init; }
}

public sealed record PlaceName
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("date")]
    public GrampsDate? Date { get; init; }

    [JsonPropertyName("lang")]
    public string? Language { get; init; }
}

public sealed record Location
{
    [JsonPropertyName("_class")]
    public string? Class { get; init; }

    [JsonPropertyName("street")]
    public string? Street { get; init; }

    [JsonPropertyName("locality")]
    public string? Locality { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("county")]
    public string? County { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("postal")]
    public string? Postal { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("parish")]
    public string? Parish { get; init; }
}

public sealed record PersonSummary(
    string Handle,
    string? GrampsId,
    string? GivenName,
    string? Surname,
    int Gender,
    int BirthRefIndex,
    int DeathRefIndex,
    long Change,
    bool Private);

public sealed record MetadataEntry(string Setting, string? JsonData, string? Value);

public sealed record DatabaseInfo(
    string? DatabasePath,
    string? SqliteVersion,
    IReadOnlyList<string> Tables,
    IReadOnlyDictionary<string, bool> GrampsTables,
    IReadOnlyList<MetadataEntry> Metadata);
