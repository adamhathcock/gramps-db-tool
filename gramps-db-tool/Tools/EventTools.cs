using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class EventTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_event", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps event by handle or Gramps ID. Event facts and place links are read-only.")]
    public async Task<EventDto> GetEvent(
        [Description("Gramps event handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps event ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var @event = await repository.GetEventAsync(handle, grampsId, cancellationToken);
        return @event ?? throw new McpException("Event not found.");
    }
}
