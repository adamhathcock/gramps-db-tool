# Gramps DB Tool Plan

## Goal

Provide direct, controlled HTTP MCP access to a Gramps JSON-backed SQLite database for genealogy review and narrowly scoped maintenance.

The governing model is:

```text
read broadly
write narrowly
keep Gramps as the primary genealogy application
```

The server must not expose a general object mutation API or raw serialized records.

## Current Status

The server:

- targets .NET 8
- serves HTTP MCP at `/gramps`
- reads Gramps JSON-backed SQLite tables directly
- resolves media and backup locations from Gramps database metadata
- opens repository read connections in read-only mode
- disables writes unless enabled at runtime
- protects writes with validation, a single-writer lock, and a pre-mutation backup
- maintains Gramps reference-map rows for supported creates and tag changes

There are currently 23 MCP tools: 15 read-only tools and 8 write or operational tools.

## Implemented Tools

### Discovery And Navigation

```text
search_people
list_objects
find_backlinks
list_tags
find_objects_by_tag
```

`list_objects` can enumerate or search all ten primary Gramps object types:

```text
person
family
event
place
source
citation
media
repository
note
tag
```

`find_backlinks` uses the Gramps `reference` table to find objects that reference a handle.

### Typed Retrieval

```text
get_person
get_family
get_event
get_place
get_source
get_citation
get_media
get_repository
get_note
get_tags
```

The batch getters accept up to 100 handles or Gramps IDs, preserve requested order for found objects, and report identifiers that were not found. Tags use names instead of Gramps IDs.

### Controlled Writes

```text
update_media
create_note
update_note
create_citation
update_citation
update_event
update_source
```

### Backup

```text
create_backup
```

## Read Contracts

### Paging

Paged tools return:

```text
items
limit
offset
returnedCount
totalCount
hasMore
nextOffset
```

Rules:

- limits must be from 1 to 500
- offsets must not be negative
- count and page reads use one SQLite read transaction
- ordering includes deterministic handle tiebreakers

### Domain DTOs

Read DTOs are independent of mutable Gramps internals and expose typed projections rather than raw JSON.

Implemented structured data includes:

- Gramps type numeric values and custom names
- exact, partial, ranged, textual, and custom-new-year date fields
- privacy markers and change timestamps
- event references and roles
- child references and parent relationship types
- media references and crop rectangles
- person associations
- dated place references
- repository references, call numbers, and media types
- internal Gramps and external styled-note links

Repository objects are fully retrievable, and source DTOs expose their repository references.

## Write Scope

### Media

`update_media` may update:

- stored media path
- complete media tag list
- conversion of an absolute path inside the database-derived media root to a relative path

It must not permit paths outside the database-derived media root.

### Notes

`create_note` creates a standalone plain-text note with optional tags.

`update_note` may update:

- plain note text
- complete note tag list

Changing note text intentionally replaces styled text with a valid plain `StyledText` object and removes stale styled-link backlinks.

### Citations

`create_citation` creates a standalone citation linked to an existing source, with optional tags.

`update_citation` may update:

- page or reference text
- confidence
- complete citation tag list

Source-link mutation remains blocked after creation.

### Events

`update_event` may update:

- description
- complete event tag list

Event type, date, place, ownership, notes, citations, and media links remain blocked.

### Sources

`update_source` may update:

- title
- author
- publication information
- abbreviation
- complete source tag list

Repository, note, media, citation, and other relationship edits remain blocked.

## Explicit Gramps IDs

`create_note` and `create_citation` require the caller to provide a unique Gramps ID.

This is deliberate:

- Gramps stores allocation counters in database metadata
- effective ID prefix templates come from global Gramps preferences, not the database
- prefixes can be customized
- defaults differ between Gramps versions

The server must not guess an ID prefix or persist an application-specific prefix. Creation validates ID uniqueness transactionally and generates a collision-checked opaque handle.

## Blocked Mutations

The MCP surface must continue to block direct edits to:

- person names, alternate names, gender, handles, and Gramps IDs
- parents, children, spouses, family links, and person associations
- event type, date, place, role, and ownership links
- place identity, coordinates, names, and hierarchy
- source, citation, note, media, and repository attachment relationships
- repository objects
- private flags
- backlinks except internal maintenance required by an allowed write
- raw JSON, blob, pickle, and generic serialized object data

There must be no generic `update_object`, arbitrary JSON patch, or direct reference mutation tool.

## Write Safety

Writes are enabled only with:

```text
--allow-writes
GRAMPS_ALLOW_WRITES=1|true|yes
```

Persisted configuration must never enable writes.

Every write must:

1. validate request shape and allowed fields
2. enforce the runtime write gate
3. acquire the single-writer lock
4. create a backup in the database-derived save path
5. open a read-write connection and transaction
6. validate target objects, Gramps IDs, source handles, and tag handles
7. apply only the explicit patch
8. synchronize affected reference-map rows
9. commit atomically
10. return the typed updated object

## Configuration And Paths

The application config selects the database:

```json
{
  "databasePath": "/path/to/gramps/sqlite.db"
}
```

The config file can be selected with:

```text
--config <path>
--config=<path>
GRAMPS_DB_TOOL_CONFIG=/path/to/gramps-db-tool.json
```

Rules:

- database location belongs in `gramps-db-tool.json`
- database paths are not accepted from CLI or environment variables
- media base path must come from Gramps database metadata
- backup base path must come from Gramps database metadata
- no media or backup fallback may use config, environment, current directory, or database directory
- missing required database metadata must fail rather than guess

### Known Path-Safety Issue

`ConfigLoader` and `Program.cs` currently support a configured `backupPath` fallback when database save-path metadata is empty. This conflicts with the required database-derived backup-path rule and must be removed before treating path safety as complete.

## Project Structure

```text
gramps-db-tool/
  Configuration/
  Data/
  Models/
  Safety/
  Services/
  Tools/
  Program.cs

gramps-db-tool.Tests/
  repository, mapping, configuration, media-path, tool-contract,
  and write-safety tests
```

## Verification

Required verification commands:

```bash
rtk dotnet build "gramps-db-tool/GrampsDbTool.csproj"
dotnet test "gramps-db-tool.Tests/GrampsDbTool.Tests.csproj"
```

Latest verified result:

```text
build: 0 errors, 0 warnings
tests: 50 passed, 0 failed
```

The current .NET tests validate paging, discovery, repositories, backlinks, structured mappings, missing-key reporting, creation, write gates, backups, validation, and reference-map synchronization.

Residual integration coverage still needed:

- list and call tools through a live MCP transport test
- deserialize newly created note and citation records with the matching Python Gramps JSON serializer
- validate against a real supported Gramps database fixture

## Next Milestones

1. Remove the configured backup-path fallback and enforce database-derived save-path metadata.
2. Add live MCP transport contract tests.
3. Add Python-Gramps compatibility tests for created records.
4. Prove standalone note and citation creation against a real Gramps database.
5. Consider explicit attachment tools only after standalone creation is proven.

Attachment tools must remain narrow and typed. They must never become a generic relationship update API.
