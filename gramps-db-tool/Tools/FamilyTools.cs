using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class FamilyTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_family", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps families by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<FamilyDto>> GetFamily(
        [Description("Gramps family handles. Required if grampsIds is not supplied.")]
        IReadOnlyList<string>? handles = null,
        [Description("User-visible Gramps family IDs. Required if handles is not supplied.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        var families = await repository.GetFamilyAsync(handles, grampsIds, cancellationToken);
        return LookupResultDto<FamilyDto>.Create(families, handles, grampsIds, "grampsId",
            static family => family.Handle, static family => family.GrampsId);
    }
}