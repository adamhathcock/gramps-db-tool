using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class SourceTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_source", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps source by handle or Gramps ID. Source fields are read-only in this milestone.")]
    public async Task<SourceDto> GetSource(
        [Description("Gramps source handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps source ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var source = await repository.GetSourceAsync(handle, grampsId, cancellationToken);
        return source ?? throw new McpException("Source not found.");
    }
}
