using System.Reflection;
using GrampsDbTool.Tools;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tests;

public sealed class ToolContractTests
{
    [Fact]
    public void RegisteredToolTypesExposeExpectedToolNamesWithoutDuplicates()
    {
        var toolNames = typeof(PersonTools).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(static attribute => attribute is not null)
            .Select(static attribute => attribute!.Name)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(toolNames.Length, toolNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
        [
            "create_backup",
            "create_citation",
            "create_note",
            "export_webtrees_fan_chart",
            "find_backlinks",
            "find_objects_by_tag",
            "get_citation",
            "get_event",
            "get_family",
            "get_media",
            "get_note",
            "get_person",
            "get_place",
            "get_repository",
            "get_source",
            "get_tags",
            "list_objects",
            "list_tags",
            "search_people",
            "update_citation",
            "update_event",
            "update_media",
            "update_note",
            "update_source"
        ], toolNames);
    }
}
