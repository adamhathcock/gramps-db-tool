using System.ComponentModel;
using System.Text.Json.Nodes;
using GrampsDbTool.Data;
using GrampsDbTool.Models;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Mcp;

[McpServerToolType]
public sealed class DatabaseTools(GrampsContext context)
{
    [McpServerTool, Description("Returns SQLite, metadata, and table availability information for the configured Gramps database.")]
    public DatabaseInfo DatabaseInfo() => context.GetDatabaseInfo();

    [McpServerTool, Description("Creates a consistent SQLite backup beside the configured database file.")]
    public DatabaseBackupResult BackupDatabase(
        [Description("Optional file-name-safe suffix. Defaults to a UTC timestamp.")] string? suffix = null,
        [Description("Whether to overwrite an existing backup path with the same suffix.")] bool overwrite = false) => context.BackupDatabase(suffix, overwrite);

    [McpServerTool, Description("Applies an RFC 7396 JSON Merge Patch to a Gramps record. Writes require --allow-writes or GRAMPS_ALLOW_WRITES=true.")]
    public RecordUpdateResult MergePatchRecord(
        [Description("Object table to update: person, family, event, place, source, citation, media, repository, note, or tag.")] string objectType,
        [Description("Handle of the record to update.")] string handle,
        [Description("JSON Merge Patch object. Null property values remove properties; arrays are replaced wholesale.")] JsonObject patch,
        [Description("Optional expected current change timestamp for optimistic concurrency.")] long? expectedChange = null,
        [Description("Whether to set the record change timestamp to the current Unix time.")] bool updateChange = true,
        [Description("If true, computes the patch result without writing to the database.")] bool dryRun = false) =>
        context.MergePatchRecord(objectType, handle, patch, expectedChange, updateChange, dryRun);

    [McpServerTool, Description("Searches Gramps people by handle, Gramps ID, given name, or surname using the source-shaped Person model. Empty or omitted search returns all people up to maxRows.")]
    public IReadOnlyList<PersonSummary> SearchPeople(
        [Description("Text to search for in person handle, Gramps ID, given name, and surname. Empty or null returns all people.")] string? search = null,
        [Description("Maximum people to return. Clamped to 1-100.")] int maxRows = 25) => context.SearchPeople(search, maxRows);

    [McpServerTool, Description("Gets a Gramps Person by handle from the person table json_data payload.")]
    public Person? GetPerson([Description("The Gramps person handle.")] string handle) => context.People.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Person by Gramps ID from the person table json_data payload.")]
    public Person? GetPersonById([Description("The Gramps person ID, for example I0001.")] string grampsId) => context.People.GetByGrampsId(grampsId);

    [McpServerTool, Description("Gets a Gramps Family by handle from the family table json_data payload.")]
    public Family? GetFamily([Description("The Gramps family handle.")] string handle) => context.Families.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Event by handle from the event table json_data payload.")]
    public Event? GetEvent([Description("The Gramps event handle.")] string handle) => context.Events.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Place by handle from the place table json_data payload.")]
    public Place? GetPlace([Description("The Gramps place handle.")] string handle) => context.Places.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Source by handle from the source table json_data payload.")]
    public Source? GetSource([Description("The Gramps source handle.")] string handle) => context.Sources.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Citation by handle from the citation table json_data payload.")]
    public Citation? GetCitation([Description("The Gramps citation handle.")] string handle) => context.Citations.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Note by handle from the note table json_data payload.")]
    public Note? GetNote([Description("The Gramps note handle.")] string handle) => context.Notes.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Media object by handle from the media table json_data payload.")]
    public Media? GetMedia([Description("The Gramps media handle.")] string handle) => context.Media.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Repository by handle from the repository table json_data payload.")]
    public Repository? GetRepository([Description("The Gramps repository handle.")] string handle) => context.Repositories.GetByHandle(handle);

    [McpServerTool, Description("Gets a Gramps Tag by handle from the tag table json_data payload.")]
    public Tag? GetTag([Description("The Gramps tag handle.")] string handle) => context.Tags.GetByHandle(handle);
}
