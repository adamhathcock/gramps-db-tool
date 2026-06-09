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
    [Description("Get up to 100 Gramps media objects by handle or Gramps ID, including resolved media file paths. Supply handles or grampsIds, not both. Existing results are returned in requested order.")]
    public Task<IReadOnlyList<MediaDto>> GetMedia(
        [Description("Gramps media handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")] IReadOnlyList<string>? handles = null,
        [Description("User-visible Gramps media IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")] IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetMediaAsync(handles, grampsIds, cancellationToken);
    }

    [McpServerTool(Name = "update_media", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Update one Gramps media object's path and/or complete tag list. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<MediaDto?> UpdateMedia(
        [Description("Gramps media handle to update.")] string mediaHandle,
        [Description("Optional new media path. Relative paths are resolved against the Gramps media root. Absolute paths require convertToRelative and must be inside the media root.")] string? newPath = null,
        [Description("Convert an absolute path inside the media root to a stored relative path before saving.")] bool convertToRelative = false,
        [Description("Optional complete desired final tag handle list. Included tags are kept or added; omitted tags are removed. Empty list clears all tags.")] IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return mediaWriteService.UpdateMediaAsync(new UpdateMediaRequest(mediaHandle, newPath, convertToRelative, tagHandles), cancellationToken);
    }
}
