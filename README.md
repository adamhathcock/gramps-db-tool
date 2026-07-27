# Gramps DB Tool

Gramps DB Tool is an HTTP MCP server for reading a Gramps SQLite database from outside Gramps itself. It is intended for AI-assisted genealogy cleanup while keeping Gramps as the primary application for detailed editing, visualization, and normal day-to-day work.

The project favors a narrow safety model: read broadly, write cautiously, and never expose a general-purpose Gramps object mutation API.

## What It Does Today

- Serves MCP over HTTP at `/gramps`.
- Reads Gramps SQLite JSON-backed tables directly.
- Resolves media paths from Gramps database metadata, not from the working directory.
- Keeps identity, relationship, event type/date/place, repository, backlink, private-flag, and raw serialized data edits blocked.
- Exposes controlled scalar and tag updates, guarded by runtime write enablement and database-derived backups.

Current read-only tools:

- `search_people`
- `list_objects`
- `find_backlinks`
- `get_person`
- `get_family`
- `get_event`
- `get_source`
- `get_place`
- `get_citation`
- `get_note`
- `get_media`
- `get_repository`
- `list_tags`
- `get_tags`
- `find_objects_by_tag`

Current write tools:

- `create_backup`
- `update_media`
- `create_note`
- `update_note`
- `create_citation`
- `update_citation`
- `update_event`
- `update_source`

`list_objects` searches or enumerates all ten Gramps primary object types: people, families, events, places, sources, citations, media, repositories, notes, and tags. Use the returned handles with the typed `get_*` tools.

Paged tools return `items`, `limit`, `offset`, `returnedCount`, `totalCount`, `hasMore`, and `nextOffset`. Paged limits are from 1 to 500 and offsets must not be negative.

Batch `get_*` tools accept up to 100 handles or Gramps IDs, preserve requested order for found objects, and return `missingValues` for identifiers that were not found. `get_tags` uses names instead of Gramps IDs.

Read DTOs expose structured Gramps dates, type values, privacy markers, change timestamps, and embedded event, child, media, person, place, and repository references. Note results include internal Gramps and external styled-text links.

`create_note` and `create_citation` require the caller to supply a unique Gramps ID. Gramps ID prefix preferences are not stored in the database and may differ between installations, so the server does not guess or persist them. New citations require an existing source handle.

## Safety Model

Writes are disabled by default and can only be enabled at runtime with `--allow-writes` or `GRAMPS_ALLOW_WRITES=1|true|yes`. The config file is intentionally not allowed to enable writes.

Write tools are limited to controlled edits:

- media paths and media tags
- note text and tags
- citation page, confidence, and tags
- standalone note and citation creation with explicit Gramps IDs
- event description and tags
- source scalar fields and tags

The server blocks direct edits to names, IDs, relationships, event type/date/place, places, repositories, backlinks, private flags, and raw serialized object data. Tag edits are allowed only through explicit controlled update tools; there is no general tag/object mutation API.

## Configuration

Create `gramps-db-tool.json`:

```json
{
  "databasePath": "/path/to/gramps/sqlite.db"
}
```

Supported runtime options:

```text
--config <path>
--config=<path>
GRAMPS_DB_TOOL_CONFIG=/path/to/gramps-db-tool.json
--allow-writes
GRAMPS_ALLOW_WRITES=1|true|yes
```

Database paths are deliberately not accepted from CLI or environment variables. Put `databasePath` in the config file instead.

## Run Locally

```bash
dotnet run --project "gramps-db-tool/GrampsDbTool.csproj" -- --config "/absolute/path/to/gramps-db-tool.json" --urls http://localhost:5000
```

Health check:

```text
http://localhost:5000/health
```

MCP endpoint:

```text
http://localhost:5000/gramps
```

## OpenCode MCP Config

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "gramps": {
      "type": "remote",
      "url": "http://localhost:5000/gramps",
      "enabled": true
    }
  }
}
```

## Development

Build:

```bash
dotnet build "gramps-db-tool/GrampsDbTool.csproj"
```

Test:

```bash
dotnet test "gramps-db-tool.Tests/GrampsDbTool.Tests.csproj"
```

## Roadmap

Next write milestones:

1. attachment tools only after standalone creates are proven

Every write must pass through the runtime write gate, single-writer lock, database-derived backup service, and validation.
