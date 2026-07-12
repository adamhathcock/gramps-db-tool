using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class CitationTools(GrampsRepository repository, ObjectWriteService objectWriteService)
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

    [McpServerTool(Name = "update_citation", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Update one Gramps citation's page, confidence, and/or complete tag list. Source links, notes, and media remain blocked. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<CitationDto?> UpdateCitation(
        [Description("Gramps citation handle to update.")]
        string citationHandle,
        [Description("Optional citation page. Omit to leave existing page unchanged.")]
        string? page = null,
        [Description("Optional citation confidence. Omit to leave existing confidence unchanged.")]
        int? confidence = null,
        [Description(
            "Optional complete desired final tag handle list. Included tags are kept or added; omitted tags are removed. Empty list clears all tags.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.UpdateCitationAsync(
            new UpdateCitationRequest(citationHandle, page, confidence, tagHandles), cancellationToken);
    }
}