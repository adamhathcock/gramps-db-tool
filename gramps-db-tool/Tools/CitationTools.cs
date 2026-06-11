using System.ComponentModel;
using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class CitationTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_citation", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps citations by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order. Citations are read-only in this milestone.")]
    public Task<IReadOnlyList<CitationDto>> GetCitation(
        [Description(
            "Gramps citation handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps citation IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetCitationAsync(handles, grampsIds, cancellationToken);
    }
}