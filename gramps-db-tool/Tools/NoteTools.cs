using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class NoteTools(GrampsRepository repository)
{
    [McpServerTool(Name = "get_note", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Get one Gramps note by handle or Gramps ID. Notes are read-only in this milestone.")]
    public async Task<NoteDto> GetNote(
        [Description("Gramps note handle. Required if grampsId is not supplied.")] string? handle = null,
        [Description("User-visible Gramps note ID. Required if handle is not supplied.")] string? grampsId = null,
        CancellationToken cancellationToken = default)
    {
        var note = await repository.GetNoteAsync(handle, grampsId, cancellationToken);
        return note ?? throw new McpException("Note not found.");
    }
}
