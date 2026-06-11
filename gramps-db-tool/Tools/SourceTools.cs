using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class SourceTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_source", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps sources by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order. Source fields are read-only in this milestone.")]
    public Task<IReadOnlyList<SourceDto>> GetSource(
        [Description(
            "Gramps source handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps source IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetSourceAsync(handles, grampsIds, cancellationToken);
    }
}