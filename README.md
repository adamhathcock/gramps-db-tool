# Gramps DB Tool

Gramps DB Tool is an HTTP MCP server for reading a Gramps SQLite database from outside Gramps itself. It is intended for AI-assisted genealogy cleanup while keeping Gramps as the primary application for detailed editing, visualization, and normal day-to-day work.

The project favors a narrow safety model: read broadly, write cautiously, and never expose a general-purpose Gramps object mutation API.

## What It Does Today

- Serves MCP over HTTP at `/gramps`.
- Reads Gramps SQLite JSON-backed tables directly.
- Resolves media paths from Gramps database metadata, not from the working directory.
- Keeps identity, relationship, fact, event, source, and place data read-only.
- Includes write-safety infrastructure, but does not expose write tools yet.

Current read-only tools:

- `search_people`
- `get_person`
- `get_family`
- `get_event`
- `get_source`
- `get_citation`
- `get_note`
- `get_media`

## Safety Model

Writes are disabled by default and can only be enabled at runtime with `--allow-writes` or `GRAMPS_ALLOW_WRITES=1`. The config file is intentionally not allowed to enable writes.

Future write tools are planned only for controlled edits:

- media paths
- notes
- citations

The server will continue to block direct edits to names, IDs, relationships, event facts, places, tags, backlinks, and raw serialized object data.

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

1. `update_media_path`
2. `create_note`
3. `update_note`
4. `create_citation`
5. `update_citation`

Before any write tool is exposed, writes must pass through the runtime write gate, single-writer lock, backup service, validation, and audit logging.
