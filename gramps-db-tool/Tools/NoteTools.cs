using System.ComponentModel;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class NoteTools(GrampsRepository repository, ObjectWriteService objectWriteService)
{
    [McpServerTool(Name = "create_note", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Create a standalone Gramps note with a caller-supplied unique Gramps ID. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<NoteDto?> CreateNote(
        [Description(
            "Unique user-visible Gramps note ID. The server does not guess the user's configured Gramps ID prefix.")]
        string grampsId,
        [Description("Plain note text.")] string text,
        [Description("Optional complete tag handle list for the new note.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.CreateNoteAsync(new CreateNoteRequest(grampsId, text, tagHandles),
            cancellationToken);
    }

    [McpServerTool(Name = "get_note", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Get up to 100 Gramps notes by handle or Gramps ID. Supply handles or grampsIds, not both. Existing results are returned in requested order and missing values are reported.")]
    public async Task<LookupResultDto<NoteDto>> GetNote(
        [Description(
            "Gramps note handles to fetch. Required if grampsIds is not supplied. At least 1 and at most 100 handles are allowed.")]
        IReadOnlyList<string>? handles = null,
        [Description(
            "User-visible Gramps note IDs to fetch. Required if handles is not supplied. At least 1 and at most 100 IDs are allowed.")]
        IReadOnlyList<string>? grampsIds = null,
        CancellationToken cancellationToken = default)
    {
        var notes = await repository.GetNoteAsync(handles, grampsIds, cancellationToken);
        return LookupResultDto<NoteDto>.Create(notes, handles, grampsIds, "grampsId",
            static item => item.Handle, static item => item.GrampsId);
    }

    [McpServerTool(Name = "update_note", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description(
        "Update one Gramps note's text and/or complete tag list. Requires runtime write enablement and creates a database-derived backup before mutation.")]
    public Task<NoteDto?> UpdateNote(
        [Description("Gramps note handle to update.")]
        string noteHandle,
        [Description("Optional new note text. Omit to leave existing text unchanged.")]
        string? newText = null,
        [Description(
            "Optional complete desired final tag handle list. Included tags are kept or added; omitted tags are removed. Empty list clears all tags.")]
        IReadOnlyList<string>? tagHandles = null,
        CancellationToken cancellationToken = default)
    {
        return objectWriteService.UpdateNoteAsync(new UpdateNoteRequest(noteHandle, newText, tagHandles),
            cancellationToken);
    }
}