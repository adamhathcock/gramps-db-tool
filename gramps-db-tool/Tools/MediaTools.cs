using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class MediaTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_media", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps media object by handle or Gramps ID, including the resolved media file path.")]
    public async Task<MediaDto> GetMedia(
        [Description("Gramps media handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps media ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var media = await repository.GetMediaAsync(handle, grampsId, cancellationToken);
        return media ?? throw new McpException("Media object not found.");
    }
}
