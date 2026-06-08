using System.Text.Json;

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
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
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
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("ref", out var refElement) && refElement.ValueKind == JsonValueKind.String)
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
        if (!person.TryGetProperty("primary_name", out var primaryName) || primaryName.ValueKind != JsonValueKind.Object)
        {
            return String(person, "gramps_id") ?? String(person, "handle") ?? "Unknown";
        }

        var firstName = String(primaryName, "first_name") ?? String(primaryName, "given_name") ?? string.Empty;
        var surname = ReadPrimarySurname(primaryName);
        var displayName = string.Join(' ', new[] { firstName, surname }.Where(static value => !string.IsNullOrWhiteSpace(value)));

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
            return string.Join(Environment.NewLine, text.EnumerateArray().Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).Where(static value => !string.IsNullOrEmpty(value)));
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

    private static string ReadPrimarySurname(JsonElement primaryName)
    {
        if (!primaryName.TryGetProperty("surname_list", out var surnameList) || surnameList.ValueKind != JsonValueKind.Array)
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
