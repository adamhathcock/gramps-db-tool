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

    [McpServerTool, Description("Copies a local file into the Gramps media directory as a new Media object and optionally links it to a Person. Writes require --allow-writes or GRAMPS_ALLOW_WRITES=true.")]
    public ImportMediaResult ImportMedia(
        [Description("Local source file path readable by the MCP server.")] string sourcePath,
        [Description("Optional media description.")] string? description = null,
        [Description("Optional MIME type. Inferred from file extension when omitted.")] string? mime = null,
        [Description("Optional destination file name. Defaults to the source file name. Must not include a directory path.")] string? fileName = null,
        [Description("Optional person handle to link the new media to.")] string? personHandle = null,
        [Description("Whether to mark the media object and person media reference private.")] bool @private = false,
        [Description("If true, validates and returns planned records without copying files or writing database changes.")] bool dryRun = false) =>
        context.ImportMedia(sourcePath, description, mime, fileName, personHandle, @private, dryRun);

    [McpServerTool, Description("Gets a Gramps record by object type and handle from the table json_data payload.")]
    public object? GetRecord(
        [Description("Object table to retrieve: person, family, event, place, source, citation, media, repository, note, or tag.")] string objectType,
        [Description("The Gramps record handle.")] string handle) => context.GetRecord(objectType, handle);

    [McpServerTool, Description("Gets a Gramps record by object type and Gramps ID from the table json_data payload. Tags do not have Gramps IDs.")]
    public object? GetRecordById(
        [Description("Object table to retrieve: person, family, event, place, source, citation, media, repository, or note.")] string objectType,
        [Description("The Gramps ID, for example I0001 or O0001.")] string grampsId) => context.GetRecordById(objectType, grampsId);

    [McpServerTool, Description("Searches Gramps people by handle, Gramps ID, given name, or surname using the source-shaped Person model. Empty or omitted search returns all people up to maxRows.")]
    public IReadOnlyList<PersonSummary> SearchPeople(
        [Description("Text to search for in person handle, Gramps ID, given name, and surname. Empty or null returns all people.")] string? search = null,
        [Description("Maximum people to return. Clamped to 1-100.")] int maxRows = 25) => context.SearchPeople(search, maxRows);

}
