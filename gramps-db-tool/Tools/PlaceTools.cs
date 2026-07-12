using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class PlaceTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_place", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps places by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order. Place hierarchy, location, and all place fields are read-only.")]
    public Task<IReadOnlyList<PlaceDto>> GetPlace(
        [Description(
            "Gramps place handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps place IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetPlaceAsync(handles, grampsIds, cancellationToken);
    }
}