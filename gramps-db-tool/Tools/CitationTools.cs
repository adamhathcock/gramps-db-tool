using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class CitationTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_citation", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps citation by handle or Gramps ID. Citations are read-only in this milestone.")]
    public async Task<CitationDto> GetCitation(
        [Description("Gramps citation handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps citation ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var citation = await repository.GetCitationAsync(handle, grampsId, cancellationToken);
        return citation ?? throw new McpException("Citation not found.");
    }
}
