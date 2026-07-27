using System.Text.Json;
using GrampsDbTool.Models;

namespace GrampsDbTool.Data;

internal static class JsonMapping
{
    public static string? String(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static int? Int(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out var value)
            ? value
            : null;
    }

    public static long? Long(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out var value)
            ? value
            : null;
    }

    public static bool? Bool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when property.TryGetInt32(out var value) => value != 0,
            _ => null
        };
    }

    public static IReadOnlyList<string> StringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    public static IReadOnlyList<string> RefArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("ref", out var refElement) &&
                refElement.ValueKind == JsonValueKind.String)
            {
                var value = refElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    public static IReadOnlyList<EventRefDto> EventRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref");
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new EventRefDto(handle, GrampsType(item, "role"), Bool(item, "private"),
                    StringArray(item, "citation_list"), StringArray(item, "note_list"));
        });
    }

    public static IReadOnlyList<ChildRefDto> StructuredChildRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref") ?? String(item, "child_handle");
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new ChildRefDto(handle, GrampsType(item, "frel"), GrampsType(item, "mrel"),
                    Bool(item, "private"), StringArray(item, "citation_list"), StringArray(item, "note_list"));
        });
    }

    public static IReadOnlyList<MediaRefDto> MediaRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref");
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new MediaRefDto(handle, IntArray(item, "rect"), Bool(item, "private"),
                    StringArray(item, "citation_list"), StringArray(item, "note_list"));
        });
    }

    public static IReadOnlyList<PersonRefDto> PersonRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref");
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new PersonRefDto(handle, TypeDisplayValue(item, "rel"), Bool(item, "private"),
                    StringArray(item, "citation_list"), StringArray(item, "note_list"));
        });
    }

    public static IReadOnlyList<PlaceRefDto> PlaceRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref");
            return string.IsNullOrWhiteSpace(handle) ? null : new PlaceRefDto(handle, Date(item, "date"));
        });
    }

    public static IReadOnlyList<RepositoryRefDto> RepositoryRefArray(JsonElement element, string propertyName)
    {
        return ObjectArray(element, propertyName, static item =>
        {
            var handle = String(item, "ref");
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new RepositoryRefDto(handle, String(item, "call_number"), GrampsType(item, "media_type"));
        });
    }

    public static IReadOnlyList<NoteLinkDto> NoteLinks(JsonElement note)
    {
        if (!note.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.Object ||
            !text.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var links = new List<NoteLinkDto>();
        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.Object || GrampsType(tag, "name")?.Value != 8)
            {
                continue;
            }

            var value = ScalarString(tag, "value");
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var ranges = TextRanges(tag, "ranges");
            if (value.StartsWith("gramps://", StringComparison.Ordinal))
            {
                var parts = value[9..].Split('/', 3);
                if (parts.Length == 3)
                {
                    links.Add(new NoteLinkDto("gramps", parts[0], parts[1], parts[2], ranges));
                }
            }
            else
            {
                links.Add(new NoteLinkDto("external", "www", "url", value, ranges));
            }
        }

        return links;
    }

    public static IReadOnlyList<string> ChildRefArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = String(item, "ref") ?? String(item, "child_handle");
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    public static string DisplayName(JsonElement person)
    {
        if (!person.TryGetProperty("primary_name", out var primaryName) ||
            primaryName.ValueKind != JsonValueKind.Object)
        {
            return String(person, "gramps_id") ?? String(person, "handle") ?? "Unknown";
        }

        var firstName = String(primaryName, "first_name") ?? String(primaryName, "given_name") ?? string.Empty;
        var surname = ReadPrimarySurname(primaryName);
        var displayName = string.Join(' ',
            new[] { firstName, surname }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(displayName)
            ? String(person, "gramps_id") ?? String(person, "handle") ?? "Unknown"
            : displayName;
    }

    public static string NoteText(JsonElement note)
    {
        if (!note.TryGetProperty("text", out var text))
        {
            return string.Empty;
        }

        if (text.ValueKind == JsonValueKind.String)
        {
            return text.GetString() ?? string.Empty;
        }

        if (text.ValueKind == JsonValueKind.Object)
        {
            return String(text, "string") ?? String(text, "text") ?? string.Empty;
        }

        if (text.ValueKind == JsonValueKind.Array)
        {
            return string.Join(Environment.NewLine,
                text.EnumerateArray()
                    .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                    .Where(static value => !string.IsNullOrEmpty(value)));
        }

        return string.Empty;
    }

    public static string? GrampsTypeName(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        if (property.ValueKind == JsonValueKind.Object)
        {
            return String(property, "string") ?? Int(property, "value")?.ToString();
        }

        return null;
    }

    public static GrampsTypeDto? GrampsType(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return new GrampsTypeDto(null, property.GetString());
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var customName = String(property, "string");
        return new GrampsTypeDto(Int(property, "value"),
            string.IsNullOrWhiteSpace(customName) ? null : customName);
    }

    public static DateDto? Date(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var date) || date.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        date.TryGetProperty("dateval", out var values);
        date.TryGetProperty("newyear", out var newYear);
        return new DateDto(
            Int(date, "calendar"),
            Int(date, "modifier"),
            Int(date, "quality"),
            DateInt(values, 0),
            DateInt(values, 1),
            DateInt(values, 2),
            DateBool(values, 3),
            DateInt(values, 4),
            DateInt(values, 5),
            DateInt(values, 6),
            DateBool(values, 7),
            String(date, "text"),
            Int(date, "sortval"),
            Int(date, "newyear"),
            DateInt(newYear, 0),
            DateInt(newYear, 1));
    }

    private static IReadOnlyList<T> ObjectArray<T>(JsonElement element, string propertyName,
        Func<JsonElement, T?> map) where T : class
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<T>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && map(item) is { } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<int>? IntArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<int>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<TextRangeDto> TextRanges(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ranges = new List<TextRangeDto>();
        foreach (var range in property.EnumerateArray())
        {
            if (range.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = range.EnumerateArray().ToArray();
            if (values.Length == 2 && values[0].TryGetInt32(out var start) && values[1].TryGetInt32(out var end))
            {
                ranges.Add(new TextRangeDto(start, end));
            }
        }

        return ranges;
    }

    private static string? ScalarString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static string? TypeDisplayValue(JsonElement element, string propertyName)
    {
        var type = GrampsType(element, propertyName);
        return type?.CustomName ?? type?.Value?.ToString();
    }

    private static int? DateInt(JsonElement values, int index)
    {
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index)
        {
            return null;
        }

        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    }

    private static bool? DateBool(JsonElement values, int index)
    {
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() <= index)
        {
            return null;
        }

        return values[index].ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when values[index].TryGetInt32(out var result) => result != 0,
            _ => null
        };
    }

    private static string ReadPrimarySurname(JsonElement primaryName)
    {
        if (!primaryName.TryGetProperty("surname_list", out var surnameList) ||
            surnameList.ValueKind != JsonValueKind.Array)
        {
            return String(primaryName, "surname") ?? string.Empty;
        }

        foreach (var surname in surnameList.EnumerateArray())
        {
            if (surname.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = String(surname, "surname");
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}