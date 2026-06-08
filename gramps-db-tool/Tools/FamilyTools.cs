using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class FamilyTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_family", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps family by handle or Gramps ID. Relationship fields are read-only.")]
    public async Task<FamilyDto> GetFamily(
        [Description("Gramps family handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps family ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var family = await repository.GetFamilyAsync(handle, grampsId, cancellationToken);
        return family ?? throw new McpException("Family not found.");
    }
}
