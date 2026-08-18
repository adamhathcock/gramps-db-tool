using System.Text.Json.Serialization;

namespace GrampsDbTool.Models;

public sealed record FanChartExportDto(
    [property: JsonPropertyName("config")] FanChartConfigDto Config,
    [property: JsonPropertyName("people")] IReadOnlyDictionary<string, FanChartPersonDto> People,
    [property: JsonPropertyName("families")] IReadOnlyDictionary<string, FanChartFamilyDto> Families
);

public sealed record FanChartConfigDto(
    [property: JsonPropertyName("defaultXref")] string DefaultXref,
    [property: JsonPropertyName("generations")] int Generations,
    [property: JsonPropertyName("detailedDateGenerations")] int DetailedDateGenerations,
    [property: JsonPropertyName("showDescendants")] bool ShowDescendants,
    [property: JsonPropertyName("showPlaces")] bool ShowPlaces,
    [property: JsonPropertyName("placeParts")] int PlaceParts,
    [property: JsonPropertyName("fanDegree")] int FanDegree,
    [property: JsonPropertyName("fontScale")] int FontScale,
    [property: JsonPropertyName("hideEmptySegments")] bool HideEmptySegments,
    [property: JsonPropertyName("showFamilyColors")] bool ShowFamilyColors,
    [property: JsonPropertyName("showParentMarriageDates")] bool ShowParentMarriageDates,
    [property: JsonPropertyName("showImages")] bool ShowImages,
    [property: JsonPropertyName("showNames")] bool ShowNames,
    [property: JsonPropertyName("showSilhouettes")] bool ShowSilhouettes,
    [property: JsonPropertyName("innerArcs")] int InnerArcs,
    [property: JsonPropertyName("paternalColor")] string PaternalColor,
    [property: JsonPropertyName("maternalColor")] string MaternalColor,
    [property: JsonPropertyName("nameAbbreviation")] string NameAbbreviation
);

public sealed record FanChartPersonDto(
    [property: JsonPropertyName("sex")] string Sex,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("firstNames")] IReadOnlyList<string> FirstNames,
    [property: JsonPropertyName("lastNames")] IReadOnlyList<string> LastNames,
    [property: JsonPropertyName("preferredName")] string PreferredName,
    [property: JsonPropertyName("nickname")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Nickname,
    [property: JsonPropertyName("birth")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FanChartFactDto? Birth,
    [property: JsonPropertyName("death")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FanChartFactDto? Death,
    [property: JsonPropertyName("image")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Image,
    [property: JsonPropertyName("childFamily")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ChildFamily,
    [property: JsonPropertyName("spouseFamilies")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? SpouseFamilies
);

public sealed record FanChartFamilyDto(
    [property: JsonPropertyName("husband")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Husband,
    [property: JsonPropertyName("wife")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Wife,
    [property: JsonPropertyName("children")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Children,
    [property: JsonPropertyName("marriage")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FanChartFactDto? Marriage
);

public sealed record FanChartFactDto(
    [property: JsonPropertyName("date")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Date,
    [property: JsonPropertyName("place")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Place
);
