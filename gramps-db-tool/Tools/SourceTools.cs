using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class SourceTools(GrampsRepository repository, ObjectWriteService objectWriteService)
{
    [McpServerTool(Name = "get_source", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps sources by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<SourceDto>> GetSource(
        [Description(
            "Gramps source handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps source IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        var sources = await repository.GetSourceAsync(handles, grampsIds, cancellationToken);
        return LookupResultDto<SourceDto>.Create(sources, handles, grampsIds, "grampsId",
            static item => item.Handle, static item => item.GrampsId);
    }

    [McpServerTool(Name = "update_source", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Update one Gramps source's scalar string fields and/or complete tag list. Notes, media, citations, and other relationships remain blocked. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<SourceDto?> UpdateSource(
        [Description("Gramps source handle to update.")]
        string sourceHandle,
        [Description("Optional source title. Omit to leave existing title unchanged.")]
        string? title = null,
        [Description("Optional source author. Omit to leave existing author unchanged.")]
        string? author = null,
        [Description(
            "Optional source publication information. Omit to leave existing publication information unchanged.")]
        string? pubinfo = null,
        [Description("Optional source abbreviation. Omit to leave existing abbreviation unchanged.")]
        string? abbrev = null,
        [Description(
            "Optional complete desired final tag handle list. Included tags are kept or added; omitted tags are removed. Empty list clears all tags.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.UpdateSourceAsync(
            new UpdateSourceRequest(sourceHandle, title, author, pubinfo, abbrev, tagHandles), cancellationToken);
    }
}