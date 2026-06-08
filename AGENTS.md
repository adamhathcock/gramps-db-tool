# Agent Guidance

This repository builds a C# HTTP MCP server for direct, controlled access to a Gramps SQLite database.

## Safety Rules

- Read broadly, write narrowly.
- Do not expose a general Gramps object update API.
- Keep identity, relationship, event fact, place, tag, private flag, backlink, and raw serialized data edits blocked.
- Media base path must come from Gramps database metadata.
- Backup base path must come from Gramps database metadata.
- Do not fall back to config, CLI, environment, current directory, or database directory for media or backup paths.
- Writes must be enabled only at runtime with `--allow-writes` or `GRAMPS_ALLOW_WRITES=1|true|yes`.
- Do not add `allowWrites` or equivalent persisted write enablement to config.
- Do not add audit logging unless there is a user-facing requirement.

## Current MCP Endpoint

The HTTP MCP endpoint is `/gramps`.

## Write Order

Implement write tools in this order:

1. `update_media_path`
2. `create_note`
3. `update_note`
4. `create_citation`
5. `update_citation`
6. attachment tools only after standalone writes are proven

Every write must use the runtime write gate, single-writer lock, backup service, target validation, and patch validation.

## Verification

Run these before reporting success:

```bash
rtk dotnet build "gramps-db-tool/GrampsDbTool.csproj"
dotnet test "gramps-db-tool.Tests/GrampsDbTool.Tests.csproj"
```

Do not revert unrelated user changes in the working tree.
