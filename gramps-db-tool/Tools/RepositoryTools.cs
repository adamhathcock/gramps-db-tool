using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class RepositoryTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_repository", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps repositories by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<RepositoryDto>> GetRepository(
        [Description(
            "Gramps repository handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps repository IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        var repositories = await repository.GetRepositoryAsync(handles, grampsIds, cancellationToken);
        return LookupResultDto<RepositoryDto>.Create(repositories, handles, grampsIds, "grampsId",
            static item => item.Handle, static item => item.GrampsId);
    }
}