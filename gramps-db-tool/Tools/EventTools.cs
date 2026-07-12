using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class EventTools(GrampsRepository repository, ObjectWriteService objectWriteService)
{
    [McpServerTool(Name = "get_event", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps events by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order. Event facts and place links are read-only.")]
    public Task<IReadOnlyList<EventDto>> GetEvent(
        [Description(
            "Gramps event handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps event IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetEventAsync(handles, grampsIds, cancellationToken);
    }

    [McpServerTool(Name = "update_event", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Update one Gramps event's description and/or complete tag list. Event type, place links, notes, citations, and media remain blocked. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<EventDto?> UpdateEvent(
        [Description("Gramps event handle to update.")]
        string eventHandle,
        [Description("Optional event description. Omit to leave existing description unchanged.")]
        string? description = null,
        [Description(
            "Optional complete desired final tag handle list. Included tags are kept or added; omitted tags are removed. Empty list clears all tags.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.UpdateEventAsync(new UpdateEventRequest(eventHandle, description, tagHandles),
            cancellationToken);
    }
}