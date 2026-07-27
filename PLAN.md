# Gramps C# MCP Server Plan

## Goal

Build a **C# MCP server** that accesses Gramps data safely, with:

* Gramps media path support via Gramps database metadata
* unsafe genealogy fields read-only
* editable media paths
* editable notes
* editable citations

---

## Architecture

```text
MCP Client
  ↓
C# Gramps MCP Server
  ↓
Gramps Data Access Layer
  ↓
Gramps database / export / config
```

Recommended first version:

```text
C# MCP Server
  ↓
Read Gramps SQLite/XML/exported data
  ↓
Write only controlled update files or supported DB fields
```

Avoid raw mutation of complex Gramps internals until the object model is fully understood.

---

## Components

## 1. MCP Server

Use a C# MCP server library or stdio-based MCP implementation.

Core tools:

```text
search_people
list_objects
find_backlinks
get_person
get_family
get_event
get_source
get_place
get_citation
get_note
get_media
get_repository
list_tags
get_tags
find_objects_by_tag
update_media
create_note
update_note
create_citation
update_citation
update_event
update_source
```

---

## 2. Gramps Configuration Reader

Purpose:

* locate Gramps config
* read configured database path
* read Gramps database media path metadata
* read Gramps database save/backup path metadata
* resolve relative media paths

Target rule:

* normal startup options belong in `gramps-db-tool.json`
* CLI/env should only select the config file or explicitly enable writes
* persisted config must not silently enable write behavior

Current target config file:

```json
{
  "databasePath": "/path/to/sqlite.db"
}
```

Planned future config file:

```json
{
  "databasePath": "/path/to/sqlite.db",
  "allowAbsoluteMediaPaths": false,
  "allowMediaPathsOutsideRoot": false
}
```

Database-derived paths:

* media base path must come from Gramps database metadata, equivalent to Gramps `get_mediapath()`
* save/backup base path must come from Gramps database metadata, equivalent to Gramps `get_save_path()`
* if required metadata is missing or empty, error instead of guessing
* do not fall back to config, current working directory, database directory, or environment variables for media or backup paths

Allowed runtime options:

```text
--config <path>
--config=<path>
GRAMPS_DB_TOOL_CONFIG=/path/to/gramps-db-tool.json
--allow-writes
GRAMPS_ALLOW_WRITES=1|true|yes
```

Removed from target design:

```text
--database <path>
-d <path>
--database=<path>
GRAMPS_SQLITE_PATH=/path/to/sqlite.db
```

Reason: database location is normal application configuration and should be recorded in the config file.

Resolution rules:

1. choose config file using `--config`, then `GRAMPS_DB_TOOL_CONFIG`, then `./gramps-db-tool.json`
2. load only `databasePath` from the selected config file
3. read media path from Gramps database metadata
4. read save/backup path from Gramps database metadata
5. error if required database metadata is missing or empty
6. do not read database, media, or backup paths from CLI/env
7. enable writes only through `--allow-writes` or `GRAMPS_ALLOW_WRITES`
8. never support `allowWrites` or an equivalent write-enabling setting in the config file

Supported config locations:

```text
Windows: %APPDATA%\gramps\
macOS:   ~/Library/Application Support/gramps/
Linux:   ~/.gramps/ or ~/.local/share/gramps/
```

Expose:

```csharp
public sealed record GrampsConfig(
    string ConfigDirectory,
    string DatabasePath
);

public sealed record GrampsDatabasePaths(
    string MediaBasePath,
    string SavePath
);
```

Later code changes:

* remove database path CLI/env resolution from `Program.cs`
* add metadata readers for Gramps media path and save path
* keep write enablement runtime-only
* update startup errors to reference `databasePath` in config
* update media/backup path errors to fail when database metadata is missing
* update README examples after implementation

---

## 3. Media Path Service

Responsibilities:

* resolve relative media paths
* validate paths
* prevent path traversal
* allow media path updates

```csharp
public interface IMediaPathService
{
    string ResolvePath(string storedPath);
    string ToRelativePath(string absolutePath);
    bool IsInsideMediaRoot(string path);
    void ValidateMediaPath(string path);
}
```

Rules:

* relative paths are resolved against the Gramps database media path metadata
* missing or empty media path metadata is an error
* absolute paths are allowed only if explicitly enabled
* reject `../`
* reject paths outside the media root unless configured

---

## 4. Read-Only Domain Model

Create C# DTOs independent of Gramps internals:

```csharp
public sealed record PersonDto(
    string Handle,
    string GrampsId,
    string DisplayName,
    IReadOnlyList<EventRefDto> Events,
    IReadOnlyList<FamilyRefDto> Families,
    IReadOnlyList<NoteRefDto> Notes,
    IReadOnlyList<CitationRefDto> Citations
);
```

Do not expose mutable setters on unsafe data.

---

## 5. Unsafe Fields: Read-Only

These must not be directly editable through MCP.

### Identity

```text
primary name
alternate names
gender
person handle
Gramps ID
```

### Relationships

```text
parents
children
spouses
family links
event ownership links
media attachment links
```

### Core Facts

```text
birth
death
burial
baptism
christening
residence
occupation
immigration
emigration
military service
```

### Sources and Places

```text
source links
citation links to facts
place assignments
place hierarchy
```

### Internals

```text
handle
change timestamp
private flag
tags
backlinks
serialized object data
```

---

## 6. Editable Fields

### Notes

Allowed:

```text
create note
update note text
delete note
attach note
detach note
```

Tools:

```text
create_note
update_note
delete_note
attach_note
detach_note
```

### Citations

Allowed:

```text
create citation
update citation text/details
attach citation
detach citation
```

Tools:

```text
create_citation
update_citation
attach_citation
detach_citation
```

### Media Paths

Allowed:

```text
update media path
convert absolute path to relative
convert relative path to absolute
```

Tool:

```text
update_media
```

---

## 7. Write Safety

Every write should:

1. create a backup
2. acquire a single-writer lock
3. validate the target object
4. validate the patch
5. apply update

Backup path rules:

* backup base path must come from Gramps database save path metadata
* missing or empty save path metadata is an error
* do not fall back to the SQLite database directory, current working directory, config, or environment variables

---

## 8. Patch-Based Write Model

Do not let tools accept arbitrary object JSON.

Prefer:

```csharp
public sealed record UpdateNoteRequest(
    string NoteHandle,
    string NewText
);

public sealed record UpdateMediaPathRequest(
    string MediaHandle,
    string NewPath,
    bool ConvertToRelative
);
```

Avoid:

```csharp
UpdatePerson(PersonDto person)
UpdateObject(Dictionary<string, object> patch)
```

---

## 9. Recommended Project Layout

```text
GrampsMcp/
  src/
    GrampsMcp.Server/
      Program.cs
      Tools/
        PersonTools.cs
        NoteTools.cs
        CitationTools.cs
        MediaTools.cs

    GrampsMcp.Core/
      Models/
      Services/
      Validation/

    GrampsMcp.Gramps/
      GrampsConfigReader.cs
      GrampsRepository.cs
      MediaPathService.cs

  tests/
    GrampsMcp.Tests/
```

---

## 10. First Milestone

Implemented read-only foundation:

```text
search_people
list_objects
find_backlinks
get_person
get_family
get_event
get_source
get_place
get_media
get_note
get_citation
get_repository
list_tags
get_tags
find_objects_by_tag
```

Implemented standalone writes in this order:

```text
update_media
create_note
update_note
create_citation
update_citation
```

---

## Bottom Line

For C#, the safest model is:

```text
C# MCP server
  ↓
read broad Gramps data
  ↓
write only notes, citations, and media paths
  ↓
block all identity, relationship, event, place, and fact edits
```

Do not expose a general Gramps object update API.
