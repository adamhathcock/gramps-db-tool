# gramps-db-tool

C# MCP server for reading Gramps 6 SQLite databases through source-shaped Gramps model objects.

The SQLite database is opened read-only and every connection enables `PRAGMA query_only = ON`. SQL is used only internally by typed repositories; there is no generic query endpoint.

## Build

```bash
dotnet restore
dotnet build
```

## Run

Pass the SQLite database path as an argument:

```bash
dotnet run -- --database /path/to/sqlite.db
```

Or use an environment variable:

```bash
GRAMPS_SQLITE_PATH=/path/to/sqlite.db dotnet run
```

By default the server exposes MCP over HTTP using the SDK HTTP transport.

## Model

The C# objects mirror Gramps 6 persisted JSON object state from `gramps/gen/lib/*`:

- `Person`
- `Family`
- `Event`
- `Place`
- `Source`
- `Citation`
- `Note`
- `Media`
- `Repository`
- `Tag`

SQLite materialized columns are treated as search/index projections. The canonical object payload is `json_data`.

## Tools

- `database_info`: returns SQLite version, table list, primary Gramps table availability, and metadata rows.
- `search_people`: searches indexed person columns and returns typed person summaries, or returns all people up to `maxRows` when search is empty or omitted.
- `get_person`: returns a typed `Person` by handle.
- `get_person_by_id`: returns a typed `Person` by Gramps ID.
- `get_family`: returns a typed `Family` by handle.
- `get_event`: returns a typed `Event` by handle.
- `get_place`: returns a typed `Place` by handle.
- `get_source`: returns a typed `Source` by handle.
- `get_citation`: returns a typed `Citation` by handle.
- `get_note`: returns a typed `Note` by handle.
- `get_media`: returns a typed `Media` by handle.
- `get_repository`: returns a typed `Repository` by handle.
- `get_tag`: returns a typed `Tag` by handle.

## Notes

- Intended for Gramps 6 SQLite databases such as `sqlite.db`.
- Unknown JSON properties are ignored by deserialization so the reader tolerates minor Gramps 6 schema differences.
- The implementation is a faithful read model, not a full Gramps runtime.
