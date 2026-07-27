using System.Text.Json;
using System.Text.Json.Nodes;
using GrampsDbTool.Models;
using GrampsDbTool.Safety;
using Microsoft.Data.Sqlite;

namespace GrampsDbTool.Data;

public sealed class ObjectWriteService(
    WriteGuard writeGuard,
    SingleWriterLock singleWriterLock,
    BackupService backupService,
    GrampsConnectionFactory connectionFactory,
    GrampsRepository repository)
{
    public async Task<NoteDto?> CreateNoteAsync(CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.GrampsId, "Note Gramps ID");
        ValidateHandle(request.Text, "Note text");
        ValidateTagHandles(request.TagHandles);
        writeGuard.RequireWritesEnabled();

        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        await backupService.CreateBackupAsync(cancellationToken);

        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = connectionFactory.CreateReadWriteConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await RequireGrampsIdAvailableAsync(connection, transaction, "note", request.GrampsId, cancellationToken);
        await RequireTagsExistAsync(connection, transaction, request.TagHandles ?? [], cancellationToken);
        var handle = await CreateUniqueHandleAsync(connection, transaction, "note", cancellationToken);
        var tagList = CreateStringArray(request.TagHandles);
        var node = new JsonObject
        {
            ["_class"] = "Note",
            ["handle"] = handle,
            ["gramps_id"] = request.GrampsId,
            ["text"] = new JsonObject
            {
                ["_class"] = "StyledText",
                ["string"] = request.Text,
                ["tags"] = new JsonArray()
            },
            ["format"] = 0,
            ["type"] = new JsonObject
            {
                ["_class"] = "NoteType",
                ["value"] = 1,
                ["string"] = string.Empty
            },
            ["change"] = change,
            ["tag_list"] = tagList,
            ["private"] = false
        };

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
                              INSERT INTO note (handle, json_data, gramps_id, format, change, private)
                              VALUES ($handle, $json, $grampsId, 0, $change, 0)
                              """;
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json",
            node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        command.Parameters.AddWithValue("$grampsId", request.GrampsId);
        command.Parameters.AddWithValue("$change", change);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertReferencesAsync(connection, transaction, handle, "Note", "Tag", request.TagHandles ?? [],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await repository.GetNoteAsync(handle, null, cancellationToken);
    }

    public async Task<CitationDto?> CreateCitationAsync(CreateCitationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.GrampsId, "Citation Gramps ID");
        ValidateHandle(request.SourceHandle, "Source handle");
        if (request.Page is null)
        {
            throw new ArgumentException("Citation page must not be null.");
        }

        if (request.Confidence is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Confidence),
                "Citation confidence must be from 0 to 4.");
        }

        ValidateTagHandles(request.TagHandles);
        writeGuard.RequireWritesEnabled();

        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        await backupService.CreateBackupAsync(cancellationToken);

        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = connectionFactory.CreateReadWriteConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await RequireGrampsIdAvailableAsync(connection, transaction, "citation", request.GrampsId,
            cancellationToken);
        await RequireObjectExistsAsync(connection, transaction, "source", request.SourceHandle, cancellationToken);
        await RequireTagsExistAsync(connection, transaction, request.TagHandles ?? [], cancellationToken);
        var handle = await CreateUniqueHandleAsync(connection, transaction, "citation", cancellationToken);
        var node = new JsonObject
        {
            ["_class"] = "Citation",
            ["handle"] = handle,
            ["gramps_id"] = request.GrampsId,
            ["date"] = new JsonObject
            {
                ["_class"] = "Date",
                ["calendar"] = 0,
                ["modifier"] = 0,
                ["quality"] = 0,
                ["dateval"] = new JsonArray(0, 0, 0, false),
                ["text"] = string.Empty,
                ["sortval"] = 0,
                ["newyear"] = 0,
                ["format"] = null
            },
            ["page"] = request.Page,
            ["confidence"] = request.Confidence,
            ["source_handle"] = request.SourceHandle,
            ["note_list"] = new JsonArray(),
            ["media_list"] = new JsonArray(),
            ["attribute_list"] = new JsonArray(),
            ["change"] = change,
            ["tag_list"] = CreateStringArray(request.TagHandles),
            ["private"] = false
        };

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
                              INSERT INTO citation
                                  (handle, json_data, gramps_id, page, confidence, source_handle, change, private)
                              VALUES
                                  ($handle, $json, $grampsId, $page, $confidence, $sourceHandle, $change, 0)
                              """;
        command.Parameters.AddWithValue("$handle", handle);
        command.Parameters.AddWithValue("$json",
            node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        command.Parameters.AddWithValue("$grampsId", request.GrampsId);
        command.Parameters.AddWithValue("$page", request.Page);
        command.Parameters.AddWithValue("$confidence", request.Confidence);
        command.Parameters.AddWithValue("$sourceHandle", request.SourceHandle);
        command.Parameters.AddWithValue("$change", change);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertReferencesAsync(connection, transaction, handle, "Citation", "Source", [request.SourceHandle],
            cancellationToken);
        await InsertReferencesAsync(connection, transaction, handle, "Citation", "Tag", request.TagHandles ?? [],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await repository.GetCitationAsync(handle, null, cancellationToken);
    }

    public async Task<NoteDto?> UpdateNoteAsync(UpdateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.NoteHandle, "Note handle");
        ValidateOptionalString(request.NewText, "New note text");
        ValidateUpdateSupplied(request.NewText is not null, request.TagHandles, "note text and/or tag handles");

        return await UpdateObjectAsync(
            "note",
            request.NoteHandle,
            request.TagHandles,
            static (node, change) => node["change"] = change,
            command =>
            {
                command.CommandText = """
                                      UPDATE note
                                      SET json_data = $json, change = $change
                                      WHERE handle = $handle
                                      """;
            },
            node =>
            {
                if (request.NewText is not null)
                {
                    node["text"] = new JsonObject
                    {
                        ["_class"] = "StyledText",
                        ["string"] = request.NewText,
                        ["tags"] = new JsonArray()
                    };
                }
            },
            () => repository.GetNoteAsync(request.NoteHandle, null, cancellationToken),
            cancellationToken,
            clearNonTagReferences: request.NewText is not null);
    }

    public async Task<CitationDto?> UpdateCitationAsync(UpdateCitationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.CitationHandle, "Citation handle");
        ValidateOptionalString(request.Page, "Citation page");
        ValidateUpdateSupplied(request.Page is not null || request.Confidence is not null, request.TagHandles,
            "citation page, confidence, and/or tag handles");

        return await UpdateObjectAsync(
            "citation",
            request.CitationHandle,
            request.TagHandles,
            static (node, change) => node["change"] = change,
            command =>
            {
                command.CommandText = """
                                      UPDATE citation
                                      SET json_data = $json, page = $page, confidence = $confidence, change = $change
                                      WHERE handle = $handle
                                      """;
                command.Parameters.AddWithValue("$page", (object?)request.Page ?? DBNull.Value);
                command.Parameters.AddWithValue("$confidence", (object?)request.Confidence ?? DBNull.Value);
            },
            node =>
            {
                if (request.Page is not null)
                {
                    node["page"] = request.Page;
                }

                if (request.Confidence is not null)
                {
                    node["confidence"] = request.Confidence;
                }
            },
            () => repository.GetCitationAsync(request.CitationHandle, null, cancellationToken),
            cancellationToken,
            node =>
            {
                if (request.Page is null)
                {
                    request = request with { Page = node["page"]?.GetValue<string>() };
                }

                if (request.Confidence is null)
                {
                    request = request with { Confidence = node["confidence"]?.GetValue<int>() };
                }
            });
    }

    public async Task<EventDto?> UpdateEventAsync(UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.EventHandle, "Event handle");
        ValidateOptionalString(request.Description, "Event description");
        ValidateUpdateSupplied(request.Description is not null, request.TagHandles,
            "event description and/or tag handles");

        return await UpdateObjectAsync(
            "event",
            request.EventHandle,
            request.TagHandles,
            static (node, change) => node["change"] = change,
            command =>
            {
                command.CommandText = """
                                      UPDATE event
                                      SET json_data = $json, description = $description, change = $change
                                      WHERE handle = $handle
                                      """;
                command.Parameters.AddWithValue("$description", (object?)request.Description ?? DBNull.Value);
            },
            node =>
            {
                if (request.Description is not null)
                {
                    node["description"] = request.Description;
                }
            },
            () => repository.GetEventAsync(request.EventHandle, null, cancellationToken),
            cancellationToken,
            node =>
            {
                if (request.Description is null)
                {
                    request = request with { Description = node["description"]?.GetValue<string>() };
                }
            });
    }

    public async Task<SourceDto?> UpdateSourceAsync(UpdateSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHandle(request.SourceHandle, "Source handle");
        ValidateOptionalString(request.Title, "Source title");
        ValidateOptionalString(request.Author, "Source author");
        ValidateOptionalString(request.Pubinfo, "Source publication information");
        ValidateOptionalString(request.Abbrev, "Source abbreviation");
        ValidateUpdateSupplied(
            request.Title is not null || request.Author is not null || request.Pubinfo is not null ||
            request.Abbrev is not null,
            request.TagHandles,
            "source title, author, publication information, abbreviation, and/or tag handles");

        return await UpdateObjectAsync(
            "source",
            request.SourceHandle,
            request.TagHandles,
            static (node, change) => node["change"] = change,
            command =>
            {
                command.CommandText = """
                                      UPDATE source
                                      SET json_data = $json, title = $title, author = $author, pubinfo = $pubinfo, abbrev = $abbrev, change = $change
                                      WHERE handle = $handle
                                      """;
                command.Parameters.AddWithValue("$title", (object?)request.Title ?? DBNull.Value);
                command.Parameters.AddWithValue("$author", (object?)request.Author ?? DBNull.Value);
                command.Parameters.AddWithValue("$pubinfo", (object?)request.Pubinfo ?? DBNull.Value);
                command.Parameters.AddWithValue("$abbrev", (object?)request.Abbrev ?? DBNull.Value);
            },
            node =>
            {
                if (request.Title is not null)
                {
                    node["title"] = request.Title;
                }

                if (request.Author is not null)
                {
                    node["author"] = request.Author;
                }

                if (request.Pubinfo is not null)
                {
                    node["pubinfo"] = request.Pubinfo;
                }

                if (request.Abbrev is not null)
                {
                    node["abbrev"] = request.Abbrev;
                }
            },
            () => repository.GetSourceAsync(request.SourceHandle, null, cancellationToken),
            cancellationToken,
            node =>
            {
                request = request with
                {
                    Title = request.Title ?? node["title"]?.GetValue<string>(),
                    Author = request.Author ?? node["author"]?.GetValue<string>(),
                    Pubinfo = request.Pubinfo ?? node["pubinfo"]?.GetValue<string>(),
                    Abbrev = request.Abbrev ?? node["abbrev"]?.GetValue<string>()
                };
            });
    }

    private async Task<T?> UpdateObjectAsync<T>(
        string tableName,
        string handle,
        IReadOnlyList<string>? tagHandles,
        Action<JsonObject, long> setChange,
        Action<SqliteCommand> configureUpdateCommand,
        Action<JsonObject> patchJson,
        Func<Task<T?>> readUpdated,
        CancellationToken cancellationToken,
        Action<JsonObject>? beforePatch = null,
        bool clearNonTagReferences = false)
    {
        ValidateTagHandles(tagHandles);
        writeGuard.RequireWritesEnabled();

        using var lockHandle = await singleWriterLock.AcquireAsync(cancellationToken);
        await backupService.CreateBackupAsync(cancellationToken);

        var change = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = connectionFactory.CreateReadWriteConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (tagHandles is not null)
        {
            await RequireTagsExistAsync(connection, transaction, tagHandles, cancellationToken);
        }

        var beforeJson = await ReadObjectJsonAsync(connection, transaction, tableName, handle, cancellationToken);
        var node = JsonNode.Parse(beforeJson)?.AsObject()
                   ?? throw new InvalidOperationException($"{tableName} JSON data is not an object.");

        beforePatch?.Invoke(node);
        patchJson(node);
        if (tagHandles is not null)
        {
            var tagList = new JsonArray();
            foreach (var tagHandle in tagHandles)
            {
                tagList.Add(tagHandle);
            }

            node["tag_list"] = tagList;
        }

        setChange(node, change);
        var afterJson = node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        configureUpdateCommand(command);
        command.Parameters.AddWithValue("$json", afterJson);
        command.Parameters.AddWithValue("$change", change);
        command.Parameters.AddWithValue("$handle", handle);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                $"{tableName} update failed because the target row was not updated exactly once.");
        }

        await DeleteReferencesAsync(connection, transaction, handle, clearNonTagReferences ? null : "Tag",
            cancellationToken);
        await InsertReferencesAsync(connection, transaction, handle, ToSentenceCase(tableName), "Tag",
            ReadStringArray(node, "tag_list"), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await readUpdated();
    }

    private static async Task<string> ReadObjectJsonAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tableName, string handle,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"SELECT json_data FROM {tableName} WHERE handle = $handle LIMIT 1";
        command.Parameters.AddWithValue("$handle", handle);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string json || string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{tableName} object not found or does not contain JSON data.");
        }

        return json;
    }

    private static async Task<string> CreateUniqueHandleAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tableName, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var handle = Guid.NewGuid().ToString("N");
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = $"SELECT 1 FROM {tableName} WHERE handle = $handle LIMIT 1";
            command.Parameters.AddWithValue("$handle", handle);
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Unable to generate a unique {tableName} handle.");
    }

    private static async Task RequireGrampsIdAvailableAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tableName, string grampsId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"SELECT 1 FROM {tableName} WHERE gramps_id = $grampsId LIMIT 1";
        command.Parameters.AddWithValue("$grampsId", grampsId);
        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException($"A {tableName} with Gramps ID '{grampsId}' already exists.");
        }
    }

    private static async Task RequireObjectExistsAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string tableName, string handle,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = $"SELECT 1 FROM {tableName} WHERE handle = $handle LIMIT 1";
        command.Parameters.AddWithValue("$handle", handle);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            throw new InvalidOperationException($"Unknown {tableName} handle: {handle}.");
        }
    }

    private static JsonArray CreateStringArray(IReadOnlyList<string>? values)
    {
        var array = new JsonArray();
        foreach (var value in values ?? [])
        {
            array.Add(value);
        }

        return array;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject node, string propertyName)
    {
        if (node[propertyName] is not JsonArray array)
        {
            return [];
        }

        return array.Select(static item => item?.GetValue<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value)).OfType<string>().ToArray();
    }

    private static async Task DeleteReferencesAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string objectHandle, string? referenceClass,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = referenceClass is null
            ? "DELETE FROM reference WHERE obj_handle = $objectHandle"
            : "DELETE FROM reference WHERE obj_handle = $objectHandle AND ref_class = $referenceClass";
        command.Parameters.AddWithValue("$objectHandle", objectHandle);
        if (referenceClass is not null)
        {
            command.Parameters.AddWithValue("$referenceClass", referenceClass);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertReferencesAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, string objectHandle, string objectClass, string referenceClass,
        IReadOnlyList<string> referenceHandles, CancellationToken cancellationToken)
    {
        foreach (var referenceHandle in referenceHandles)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                                  INSERT INTO reference (obj_handle, obj_class, ref_handle, ref_class)
                                  VALUES ($objectHandle, $objectClass, $referenceHandle, $referenceClass)
                                  """;
            command.Parameters.AddWithValue("$objectHandle", objectHandle);
            command.Parameters.AddWithValue("$objectClass", objectClass);
            command.Parameters.AddWithValue("$referenceHandle", referenceHandle);
            command.Parameters.AddWithValue("$referenceClass", referenceClass);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string ToSentenceCase(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static void ValidateHandle(string handle, string name)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new ArgumentException($"{name} is required.");
        }
    }

    private static void ValidateOptionalString(string? value, string name)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }
    }

    private static void ValidateUpdateSupplied(bool hasScalarUpdate, IReadOnlyList<string>? tagHandles,
        string description)
    {
        if (!hasScalarUpdate && tagHandles is null)
        {
            throw new ArgumentException($"Supply {description}.");
        }
    }

    private static void ValidateTagHandles(IReadOnlyList<string>? tagHandles)
    {
        if (tagHandles is null)
        {
            return;
        }

        if (tagHandles.Any(static handle => string.IsNullOrWhiteSpace(handle)))
        {
            throw new ArgumentException("Tag handles must not be empty.");
        }

        var duplicate = tagHandles.GroupBy(static handle => handle, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate tag handle supplied: {duplicate}.");
        }
    }

    private static async Task RequireTagsExistAsync(SqliteConnection connection,
        System.Data.Common.DbTransaction transaction, IReadOnlyList<string> tagHandles,
        CancellationToken cancellationToken)
    {
        if (tagHandles.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        var parameters = tagHandles.Select((handle, index) => new { Handle = handle, Parameter = $"$tag{index}" })
            .ToArray();
        command.CommandText =
            $"SELECT handle FROM tag WHERE handle IN ({string.Join(", ", parameters.Select(static parameter => parameter.Parameter))})";
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Parameter, parameter.Handle);
        }

        var existingHandles = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            existingHandles.Add(reader.GetString(0));
        }

        var missingHandles = tagHandles.Where(handle => !existingHandles.Contains(handle)).ToArray();
        if (missingHandles.Length > 0)
        {
            throw new InvalidOperationException($"Unknown tag handle(s): {string.Join(", ", missingHandles)}.");
        }
    }
}