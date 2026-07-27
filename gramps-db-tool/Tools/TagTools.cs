using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class TagTools(GrampsRepository repository)
{
    [McpServerTool(Name = "list_tags", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("List Gramps tags sorted by priority and name with deterministic offset paging.")]
    public Task<PageDto<TagDto>> ListTags(
        [Description("Maximum number of tags to return, from 1 to 500.")]
        int limit = 100,
        [Description("Number of tags to skip. Must not be negative.")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.ListTagsAsync(limit, offset, cancellationToken);
    }

    [McpServerTool(Name = "get_tags", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps tags by handle or name. Supply handles or names, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<TagDto>> GetTags(
        [Description(
            "Gramps tag handles to fetch. Required if names is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "Gramps tag names to fetch. Required if handles is not supplied. At least 1 and at most 100 names are allowed.")]
        IReadOnlyList<string>? names = null,
        CancellationToken cancellationToken = default)
    {
        var tags = await repository.GetTagsAsync(handles, names, cancellationToken);
        return LookupResultDto<TagDto>.Create(tags, handles, names, "name",
            static item => item.Handle, static item => item.Name);
    }

    [McpServerTool(Name = "find_objects_by_tag", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Find Gramps objects that have a tag. Supply tagHandle or tagName, not both. Searches all tag-capable object types unless objectTypes is supplied.")]
    public Task<PageDto<TaggedObjectDto>> FindObjectsByTag(
        [Description("Gramps tag handle. Required if tagName is not supplied.")]
        string? tagHandle = null,
        [Description("Gramps tag name. Required if tagHandle is not supplied.")]
        string? tagName = null,
        [Description(
            "Optional object types to include: person, family, event, place, source, citation, media, repository, note.")]
        IReadOnlyList<string>? objectTypes = null,
        [Description("Maximum number of tagged objects to return, from 1 to 500.")]
        int limit = 100,
        [Description("Number of matching tagged objects to skip before returning results. Must not be negative.")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.FindObjectsByTagAsync(tagHandle, tagName, objectTypes, limit, offset, cancellationToken);
    }
}