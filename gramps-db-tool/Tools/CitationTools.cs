using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class CitationTools(GrampsRepository repository, ObjectWriteService objectWriteService)
{
    [McpServerTool(Name = "create_citation", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Create a standalone Gramps citation linked to an existing source, using a caller-supplied unique Gramps ID. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<CitationDto?> CreateCitation(
        [Description(
            "Unique user-visible Gramps citation ID. The server does not guess the user's configured Gramps ID prefix.")]
        string grampsId,
        [Description("Existing Gramps source handle.")]
        string sourceHandle,
        [Description("Citation page or reference text.")]
        string page = "",
        [Description("Citation confidence from 0 (very low) to 4 (very high).")]
        int confidence = 2,
        [Description("Optional complete tag handle list for the new citation.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.CreateCitationAsync(
            new CreateCitationRequest(grampsId, sourceHandle, page, confidence, tagHandles), cancellationToken);
    }

    [McpServerTool(Name = "get_citation", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps citations by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<CitationDto>> GetCitation(
        [Description(
            "Gramps citation handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps citation IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        var citations = await repository.GetCitationAsync(handles, grampsIds, cancellationToken);
        return LookupResultDto<CitationDto>.Create(citations, handles, grampsIds, "grampsId",
            static item => item.Handle, static item => item.GrampsId);
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