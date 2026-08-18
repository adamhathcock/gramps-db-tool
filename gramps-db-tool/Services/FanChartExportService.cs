using System.Text.Json;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Services;

public sealed class FanChartExportService(GrampsConnectionFactory connectionFactory, IMediaPathService mediaPathService)
{
    private const int MarriageEventType = 1;
    private const int GregorianCalendar = 0;
    private const int ExactDateModifier = 0;
    private const int TextOnlyDateModifier = 6;

    public async Task<FanChartExportDto> ExportAsync(string? defaultPersonHandle = null,
        string? defaultPersonGrampsId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(defaultPersonHandle) == string.IsNullOrWhiteSpace(defaultPersonGrampsId) &&
            (defaultPersonHandle is not null || defaultPersonGrampsId is not null))
        {
            throw new ArgumentException("Supply either default person handle or Gramps ID, but not both.");
        }

        await using var connection = connectionFactory.CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var people = await ReadRecordsAsync(connection, transaction, "person", MapPerson, cancellationToken);
        var families = await ReadRecordsAsync(connection, transaction, "family", MapFamily, cancellationToken);
        var events = await ReadRecordsAsync(connection, transaction, "event", MapEvent, cancellationToken);
        var places = await ReadRecordsAsync(connection, transaction, "place", MapPlace, cancellationToken);
        var media = await ReadRecordsAsync(connection, transaction, "media", MapMedia, cancellationToken);
        var defaultPersonMetadataHandle = await ReadMetadataStringAsync(connection, transaction, "default-person-handle",
            cancellationToken);

        var personXrefs = CreateXrefs(people.Values, static person => person.Handle, static person => person.GrampsId,
            "person");
        var familyXrefs = CreateXrefs(families.Values, static family => family.Handle, static family => family.GrampsId,
            "family");
        var defaultXref = ResolveDefaultXref(people, personXrefs, defaultPersonHandle, defaultPersonGrampsId,
            defaultPersonMetadataHandle);

        var peopleByXref = new SortedDictionary<string, FanChartPersonDto>(StringComparer.Ordinal);
        foreach (var person in people.Values.OrderBy(static person => person.GrampsId ?? person.Handle,
                     StringComparer.Ordinal).ThenBy(static person => person.Handle, StringComparer.Ordinal))
        {
            var xref = personXrefs[person.Handle];
            peopleByXref.Add(xref, CreatePerson(person, familyXrefs, events, places, media));
        }

        var familiesByXref = new SortedDictionary<string, FanChartFamilyDto>(StringComparer.Ordinal);
        foreach (var family in families.Values.OrderBy(static family => family.GrampsId ?? family.Handle,
                     StringComparer.Ordinal).ThenBy(static family => family.Handle, StringComparer.Ordinal))
        {
            var xref = familyXrefs[family.Handle];
            familiesByXref.Add(xref, CreateFamily(family, personXrefs, events, places));
        }

        return new FanChartExportDto(CreateConfig(defaultXref), peopleByXref, familiesByXref);
    }

    private FanChartPersonDto CreatePerson(PersonRecord person, IReadOnlyDictionary<string, string> familyXrefs,
        IReadOnlyDictionary<string, EventRecord> events, IReadOnlyDictionary<string, PlaceRecord> places,
        IReadOnlyDictionary<string, MediaRecord> media)
    {
        var birth = CreateFact(person.BirthEventHandle, events, places);
        var death = CreateFact(person.DeathEventHandle, events, places);
        var childFamily = person.ParentFamilyHandles.Select(familyXrefs.GetValueOrDefault)
            .FirstOrDefault(static familyXref => familyXref is not null);
        var spouseFamilies = person.FamilyHandles.Select(familyXrefs.GetValueOrDefault)
            .Where(static familyXref => familyXref is not null)
            .Select(static familyXref => familyXref!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new FanChartPersonDto(person.Sex, person.Name.FullName, person.Name.FirstNames, person.Name.LastNames,
            person.Name.PreferredName, person.Name.Nickname, birth, death, ResolveImage(person.MediaHandles, media),
            childFamily, spouseFamilies.Length == 0 ? null : spouseFamilies);
    }

    private static FanChartFamilyDto CreateFamily(FamilyRecord family,
        IReadOnlyDictionary<string, string> personXrefs, IReadOnlyDictionary<string, EventRecord> events,
        IReadOnlyDictionary<string, PlaceRecord> places)
    {
        var children = family.ChildHandles.Select(personXrefs.GetValueOrDefault)
            .Where(static childXref => childXref is not null)
            .Select(static childXref => childXref!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var marriageEventHandle = family.EventHandles
            .FirstOrDefault(handle => events.TryGetValue(handle, out var @event) && @event.TypeValue == MarriageEventType);

        return new FanChartFamilyDto(personXrefs.GetValueOrDefault(family.FatherHandle ?? string.Empty),
            personXrefs.GetValueOrDefault(family.MotherHandle ?? string.Empty), children.Length == 0 ? null : children,
            CreateFact(marriageEventHandle, events, places));
    }

    private string? ResolveImage(IReadOnlyList<string> mediaHandles, IReadOnlyDictionary<string, MediaRecord> media)
    {
        foreach (var handle in mediaHandles)
        {
            if (!media.TryGetValue(handle, out var item) || string.IsNullOrWhiteSpace(item.Path) ||
                string.IsNullOrWhiteSpace(item.Mime) || !item.Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var path = mediaPathService.ResolvePath(item.Path);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (ArgumentException)
            {
                // A malformed media record must not prevent a read-only library export.
            }
        }

        return null;
    }

    private static FanChartFactDto? CreateFact(string? eventHandle, IReadOnlyDictionary<string, EventRecord> events,
        IReadOnlyDictionary<string, PlaceRecord> places)
    {
        if (string.IsNullOrWhiteSpace(eventHandle) || !events.TryGetValue(eventHandle, out var @event))
        {
            return null;
        }

        var date = FormatDate(@event.Date);
        var place = @event.PlaceHandle is not null && places.TryGetValue(@event.PlaceHandle, out var placeRecord)
            ? placeRecord.Name
            : null;
        return string.IsNullOrWhiteSpace(date) && string.IsNullOrWhiteSpace(place)
            ? null
            : new FanChartFactDto(date, place);
    }

    private static string? FormatDate(DateDto? date)
    {
        if (date is null)
        {
            return null;
        }

        if (date.Modifier == TextOnlyDateModifier)
        {
            return EmptyToNull(date.Text);
        }

        var start = FormatDatePart(date.Day, date.Month, date.Year, date.IsSlash);
        if (string.IsNullOrWhiteSpace(start))
        {
            return EmptyToNull(date.Text);
        }

        if (date.Calendar == GregorianCalendar && date.Modifier == ExactDateModifier && date.Quality == ExactDateModifier)
        {
            return start;
        }

        var prefix = date.Quality switch
        {
            1 => "est ",
            2 => "calc ",
            _ => string.Empty
        };
        var suffix = date.Calendar is not null and not GregorianCalendar
            ? $" ({CalendarName(date.Calendar.Value)})"
            : string.Empty;

        return date.Modifier switch
        {
            1 => $"{prefix}bef {start}{suffix}",
            2 => $"{prefix}aft {start}{suffix}",
            3 => $"{prefix}abt {start}{suffix}",
            4 => $"{prefix}between {start} and {FormatDatePart(date.RangeDay, date.RangeMonth, date.RangeYear, date.RangeIsSlash) ?? start}{suffix}",
            5 => $"{prefix}from {start} to {FormatDatePart(date.RangeDay, date.RangeMonth, date.RangeYear, date.RangeIsSlash) ?? start}{suffix}",
            7 => $"{prefix}from {start}{suffix}",
            8 => $"{prefix}to {start}{suffix}",
            _ => $"{prefix}{start}{suffix}"
        };
    }

    private static string? FormatDatePart(int? day, int? month, int? year, bool? isSlash)
    {
        if (year is null or <= 0)
        {
            return null;
        }

        if (isSlash == true)
        {
            return month is > 0 && day is > 0
                ? $"{year - 1:0000}/{year % 10}-{month:00}-{day:00}"
                : $"{year - 1:0000}/{year % 10}";
        }

        return month is > 0 && day is > 0
            ? $"{year:0000}-{month:00}-{day:00}"
            : month is > 0
                ? $"{year:0000}-{month:00}"
                : $"{year:0000}";
    }

    private static string CalendarName(int calendar)
    {
        return calendar switch
        {
            1 => "Julian",
            2 => "Hebrew",
            3 => "French Republican",
            4 => "Persian",
            5 => "Islamic",
            6 => "Swedish",
            _ => "Unknown"
        };
    }

    private static FanChartConfigDto CreateConfig(string defaultXref)
    {
        return new FanChartConfigDto(defaultXref, 6, 2, true, true, 1, 210, 100, false, true, true, true, true,
            false, 3, "#70a9cf", "#d06f94", "GIVEN");
    }

    private static string ResolveDefaultXref(IReadOnlyDictionary<string, PersonRecord> people,
        IReadOnlyDictionary<string, string> personXrefs, string? defaultPersonHandle, string? defaultPersonGrampsId,
        string? defaultPersonMetadataHandle)
    {
        if (!string.IsNullOrWhiteSpace(defaultPersonHandle))
        {
            return personXrefs.GetValueOrDefault(defaultPersonHandle) ??
                   throw new ArgumentException($"Gramps person handle '{defaultPersonHandle}' was not found.",
                       nameof(defaultPersonHandle));
        }

        if (!string.IsNullOrWhiteSpace(defaultPersonGrampsId))
        {
            var person = people.Values.FirstOrDefault(person =>
                string.Equals(person.GrampsId, defaultPersonGrampsId, StringComparison.Ordinal));
            return person is null
                ? throw new ArgumentException($"Gramps person ID '{defaultPersonGrampsId}' was not found.",
                    nameof(defaultPersonGrampsId))
                : personXrefs[person.Handle];
        }

        if (!string.IsNullOrWhiteSpace(defaultPersonMetadataHandle) &&
            personXrefs.TryGetValue(defaultPersonMetadataHandle, out var metadataXref))
        {
            return metadataXref;
        }

        return people.Values.OrderBy(static person => person.GrampsId ?? person.Handle, StringComparer.Ordinal)
            .ThenBy(static person => person.Handle, StringComparer.Ordinal)
            .Select(person => personXrefs[person.Handle])
            .FirstOrDefault() ?? string.Empty;
    }

    private static Dictionary<string, string> CreateXrefs<T>(IEnumerable<T> records, Func<T, string> getHandle,
        Func<T, string?> getGrampsId, string recordType)
    {
        var xrefs = new Dictionary<string, string>(StringComparer.Ordinal);
        var handlesByXref = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var handle = getHandle(record);
            var xref = EmptyToNull(getGrampsId(record)) ?? handle;
            if (handlesByXref.TryGetValue(xref, out var existingHandle))
            {
                throw new InvalidOperationException(
                    $"Cannot export {recordType} xref '{xref}' because handles '{existingHandle}' and '{handle}' collide.");
            }

            handlesByXref.Add(xref, handle);
            xrefs.Add(handle, xref);
        }

        return xrefs;
    }

    private static async Task<Dictionary<string, T>> ReadRecordsAsync<T>(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tableName, Func<string, string?, JsonElement, T> map,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"SELECT handle, gramps_id, json_data FROM {tableName}";

        var records = new Dictionary<string, T>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var handle = reader.GetString(0);
            var grampsId = reader.IsDBNull(1) ? null : reader.GetString(1);
            using var document = JsonDocument.Parse(reader.GetString(2));
            records.Add(handle, map(handle, grampsId, document.RootElement));
        }

        return records;
    }

    private static async Task<string?> ReadMetadataStringAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string setting, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT json_data FROM metadata WHERE setting = $setting";
        command.Parameters.AddWithValue("$setting", setting);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string json || string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.String
            ? root.GetString()
            : JsonMapping.String(root, "value");
    }

    private static PersonRecord MapPerson(string handle, string? grampsId, JsonElement root)
    {
        var events = JsonMapping.RefArray(root, "event_ref_list");
        return new PersonRecord(handle, grampsId, MapName(root, grampsId ?? handle), MapSex(JsonMapping.Int(root, "gender")),
            JsonMapping.StringArray(root, "family_list"), JsonMapping.StringArray(root, "parent_family_list"),
            JsonMapping.RefArray(root, "media_list"), EventHandleAt(events, JsonMapping.Int(root, "birth_ref_index")),
            EventHandleAt(events, JsonMapping.Int(root, "death_ref_index")));
    }

    private static FamilyRecord MapFamily(string handle, string? grampsId, JsonElement root)
    {
        return new FamilyRecord(handle, grampsId, JsonMapping.String(root, "father_handle"),
            JsonMapping.String(root, "mother_handle"), JsonMapping.ChildRefArray(root, "child_ref_list"),
            JsonMapping.RefArray(root, "event_ref_list"));
    }

    private static EventRecord MapEvent(string handle, string? grampsId, JsonElement root)
    {
        var type = JsonMapping.GrampsType(root, "type");
        return new EventRecord(handle, grampsId, type?.Value, JsonMapping.Date(root, "date"), JsonMapping.String(root, "place"));
    }

    private static PlaceRecord MapPlace(string handle, string? grampsId, JsonElement root)
    {
        var primaryName = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.Object
            ? JsonMapping.String(name, "value")
            : null;
        return new PlaceRecord(handle, grampsId, EmptyToNull(JsonMapping.String(root, "title")) ?? primaryName);
    }

    private static MediaRecord MapMedia(string handle, string? grampsId, JsonElement root)
    {
        return new MediaRecord(handle, grampsId, JsonMapping.String(root, "path"), JsonMapping.String(root, "mime"));
    }

    private static FanChartName MapName(JsonElement person, string fallback)
    {
        if (!person.TryGetProperty("primary_name", out var name) || name.ValueKind != JsonValueKind.Object)
        {
            return new FanChartName(fallback, [], [], fallback, null);
        }

        var firstNames = SplitNamePart(JsonMapping.String(name, "first_name") ?? JsonMapping.String(name, "given_name"));
        var lastNames = ReadLastNames(name);
        var preferredName = EmptyToNull(JsonMapping.String(name, "call")) ?? firstNames.FirstOrDefault() ?? fallback;
        var nickname = EmptyToNull(JsonMapping.String(name, "nick"));
        var fullName = string.Join(' ', firstNames.Concat(lastNames).Append(JsonMapping.String(name, "suffix") ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value)));

        return new FanChartName(string.IsNullOrWhiteSpace(fullName) ? fallback : fullName, firstNames, lastNames,
            preferredName, nickname);
    }

    private static IReadOnlyList<string> ReadLastNames(JsonElement name)
    {
        if (!name.TryGetProperty("surname_list", out var surnames) || surnames.ValueKind != JsonValueKind.Array)
        {
            return SplitNamePart(JsonMapping.String(name, "surname"));
        }

        var values = new List<string>();
        foreach (var surname in surnames.EnumerateArray())
        {
            if (surname.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = string.Join(' ', new[] { JsonMapping.String(surname, "prefix"), JsonMapping.String(surname, "surname") }
                .Where(static part => !string.IsNullOrWhiteSpace(part)));
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> SplitNamePart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? EventHandleAt(IReadOnlyList<string> events, int? index)
    {
        return index is >= 0 && index < events.Count ? events[index.Value] : null;
    }

    private static string MapSex(int? gender)
    {
        return gender switch
        {
            1 => "M",
            0 => "F",
            _ => "U"
        };
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record PersonRecord(
        string Handle,
        string? GrampsId,
        FanChartName Name,
        string Sex,
        IReadOnlyList<string> FamilyHandles,
        IReadOnlyList<string> ParentFamilyHandles,
        IReadOnlyList<string> MediaHandles,
        string? BirthEventHandle,
        string? DeathEventHandle
    );

    private sealed record FamilyRecord(
        string Handle,
        string? GrampsId,
        string? FatherHandle,
        string? MotherHandle,
        IReadOnlyList<string> ChildHandles,
        IReadOnlyList<string> EventHandles
    );

    private sealed record EventRecord(string Handle, string? GrampsId, int? TypeValue, DateDto? Date, string? PlaceHandle);

    private sealed record PlaceRecord(string Handle, string? GrampsId, string? Name);

    private sealed record MediaRecord(string Handle, string? GrampsId, string? Path, string? Mime);

    private sealed record FanChartName(string FullName, IReadOnlyList<string> FirstNames,
        IReadOnlyList<string> LastNames, string PreferredName, string? Nickname);
}
