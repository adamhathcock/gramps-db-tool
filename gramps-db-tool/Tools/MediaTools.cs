using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class MediaTools(GrampsRepository repository, MediaWriteService mediaWriteService)
{
    [McpServerTool(Name = "get_media", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get up to 100 Gramps media objects by handle, including resolved media file paths. Existing results are returned in requested order.")]
    public Task<IReadOnlyList<MediaDto>> GetMedia(
        [Description("Gramps media handles to fetch. At least 1 and at most 100 handles are allowed.")] IReadOnlyList<string>? handles,
        CancellationToken cancellationToken = default)
    {
        return repository.GetMediaAsync(handles, cancellationToken);
    }

    [McpServerTool(Name = "update_media_path", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Update one Gramps media object's path. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<MediaDto?> UpdateMediaPath(
        [Description("Gramps media handle to update.")] string mediaHandle,
        [Description("New media path. Relative paths are resolved against the Gramps media root. Absolute paths require convertToRelative and must be inside the media root.")] string newPath,
        [Description("Convert an absolute path inside the media root to a stored relative path before saving.")] bool convertToRelative = false,
        CancellationToken cancellationToken = default)
    {
        return mediaWriteService.UpdateMediaPathAsync(new UpdateMediaPathRequest(mediaHandle, newPath, convertToRelative), cancellationToken);
    }
}
