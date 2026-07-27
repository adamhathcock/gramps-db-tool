using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class ObjectTools(GrampsRepository repository)
{
    [McpServerTool(Name = "list_objects", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "List or search Gramps primary objects with deterministic offset paging. Includes people, families, events, places, sources, citations, media, repositories, notes, and tags.")]
    public Task<PageDto<ObjectSummaryDto>> ListObjects(
        [Description("Optional text matched against handle, Gramps ID, and the object's primary display label.")]
        string query = "",
        [Description(
            "Optional object types to include: person, family, event, place, source, citation, media, repository, note, tag.")]
        IReadOnlyList<string>? objectTypes = null,
        [Description("Maximum number of objects to return, from 1 to 500.")]
        int limit = 100,
        [Description("Number of matching objects to skip. Must not be negative.")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.ListObjectsAsync(query, objectTypes, limit, offset, cancellationToken);
    }

    [McpServerTool(Name = "find_backlinks", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Find primary Gramps objects that reference a handle, using the Gramps reference map and deterministic offset paging.")]
    public Task<PageDto<BacklinkDto>> FindBacklinks(
        [Description("Handle referenced by the objects to return.")]
        string handle,
        [Description(
            "Optional referring object types to include: person, family, event, place, source, citation, media, repository, note, tag.")]
        IReadOnlyList<string>? objectTypes = null,
        [Description("Maximum number of backlinks to return, from 1 to 500.")]
        int limit = 100,
        [Description("Number of matching backlinks to skip. Must not be negative.")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return repository.FindBacklinksAsync(handle, objectTypes, limit, offset, cancellationToken);
    }
}