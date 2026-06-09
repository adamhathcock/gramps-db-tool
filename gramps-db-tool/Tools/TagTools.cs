using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class TagTools(GrampsRepository repository)
{
    [McpServerTool(Name = "list_tags", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("List all Gramps tags sorted by priority and name.")]
    public Task<IReadOnlyList<TagDto>> ListTags(CancellationToken cancellationToken = default)
    {
        return repository.ListTagsAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_tags", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get up to 100 Gramps tags by handle or name. Supply handles or names, not both. Existing results are returned in requested order.")]
    public Task<IReadOnlyList<TagDto>> GetTags(
        [Description("Gramps tag handles to fetch. Required if names is not supplied. At least 1 and at most 100 handles are allowed.")] IReadOnlyList<string>? handles = null,
        [Description("Gramps tag names to fetch. Required if handles is not supplied. At least 1 and at most 100 names are allowed.")] IReadOnlyList<string>? names = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetTagsAsync(handles, names, cancellationToken);
    }

    [McpServerTool(Name = "find_objects_by_tag", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Find Gramps objects that have a tag. Supply tagHandle or tagName, not both. Searches all tag-capable object types unless objectTypes is supplied.")]
    public Task<IReadOnlyList<TaggedObjectDto>> FindObjectsByTag(
        [Description("Gramps tag handle. Required if tagName is not supplied.")] string? tagHandle = null,
        [Description("Gramps tag name. Required if tagHandle is not supplied.")] string? tagName = null,
        [Description("Optional object types to include: person, family, event, place, source, citation, media, repository, note.")] IReadOnlyList<string>? objectTypes = null,
        [Description("Maximum number of tagged objects to return, from 1 to 500.")] int limit = 100,
        [Description("Number of matching tagged objects to skip before returning results. Negative values are treated as 0.")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.FindObjectsByTagAsync(tagHandle, tagName, objectTypes, limit, offset, cancellationToken);
    }
}
