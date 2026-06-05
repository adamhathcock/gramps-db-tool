---
name: gramps-schema
description: Use ONLY when working with Gramps 6 SQLite schema, sqlite.db, gramps.gen.db, table layouts, metadata, indexes, or Gramps object serialization.
---

# Gramps 6 SQLite Schema

Use this skill for Gramps 6 database questions, especially `sqlite.db`, `gramps.gen.db`, object tables, metadata, and reference maps.

## Scope

- Gramps 6 current SQLite schema only.
- Based on upstream Gramps source, not the wiki page.
- Current schema version: `22`.
- Storage backend: `sqlite.db` or `:memory:`.

## Mental Model

- Each primary object type has one table.
- Each table has a `handle` primary key.
- The main object payload is stored in a backend-managed data column.
- Secondary scalar fields are materialized as SQL columns from the object JSON schema.
- There are no SQL foreign keys; backlinks are tracked in `reference`.

## Primary Tables

### `person`

- Columns: `handle`, `given_name`, `surname`, plus schema-backed fields.
- Scalar fields: `gramps_id`, `gender`, `death_ref_index`, `birth_ref_index`, `change`, `private`.
- Related data: names, events, families, media, addresses, attributes, URLs, LDS ordinances, citations, notes, tags, person refs, FamilySearch sync.
- Indexes: `gramps_id`, `surname`, `given_name`.

### `family`

- Columns: `handle`, plus schema-backed fields.
- Scalar fields: `gramps_id`, `father_handle`, `mother_handle`, `change`, `private`.
- Related data: child refs, event refs, media, attributes, LDS ordinances, citations, notes, tags.
- Indexes: `gramps_id`.

### `event`

- Scalar fields: `gramps_id`, `type`, `date`, `description`, `place`, `change`, `private`.
- Related data: citations, notes, media, attributes, tags.
- Index: `gramps_id`.

### `place`

- Scalar fields: `gramps_id`, `title`, `long`, `lat`, `code`, `change`, `private`.
- Extra column: `enclosed_by`.
- Related data: place refs, name, alternate names, type, alternate locations, URLs, media, citations, notes, tags.
- Indexes: `title`, `enclosed_by`, `gramps_id`.

### `source`

- Scalar fields: `gramps_id`, `title`, `author`, `pubinfo`, `abbrev`, `change`, `private`.
- Related data: notes, media, source attributes, repository refs, tags.
- Indexes: `title`, `gramps_id`.

### `citation`

- Scalar fields: `gramps_id`, `date`, `page`, `confidence`, `source_handle`, `change`, `private`.
- Related data: notes, media, source attributes, tags.
- Indexes: `page`, `gramps_id`.

### `media`

- Scalar fields: `gramps_id`, `path`, `mime`, `desc`, `checksum`, `change`, `date`, `private`.
- Related data: attributes, citations, notes, tags.
- Indexes: `desc`, `gramps_id`.

### `repository`

- Scalar fields: `gramps_id`, `type`, `name`, `change`, `private`.
- Related data: notes, addresses, URLs, tags.
- Index: `gramps_id`.

### `note`

- Scalar fields: `gramps_id`, `text`, `format`, `type`, `change`, `private`.
- Related data: tags.
- Index: `gramps_id`.

### `tag`

- Scalar fields: `name`, `color`, `priority`, `change`.
- No `gramps_id` column.
- No `private` column.
- Index: `name`.

## Auxiliary Tables

### `reference`

- Columns: `obj_handle`, `obj_class`, `ref_handle`, `ref_class`.
- Purpose: derived backlink map for cross-object references.
- Indexes: `obj_handle`, `ref_handle`.
- Important: this is maintained by application code, not a foreign-key engine.

### `name_group`

- Columns: `name`, `grouping`.
- Purpose: surname grouping lookup.

### `metadata`

- Columns: `setting`, `json_data`, `value`.
- Purpose: version, researcher, bookmarks, custom type sets, and map indexes.
- `use_json_data()` checks whether `json_data` exists.

### `gender_stats`

- Columns: `given_name`, `female`, `male`, `unknown`.
- Purpose: cached name/gender statistics.

## Current Schema Rules

- `handle` is capped at 50 characters in primary tables.
- `change` is an integer timestamp.
- `private` is boolean/integer in SQLite.
- Optional handles may be nullable, for example `family.father_handle`, `family.mother_handle`, `event.place`.
- Secondary columns are generated from JSON schema properties of type `string`, `integer`, `number`, or `boolean`.
- Arrays and nested objects remain inside the payload column.

## Indexes To Remember

- Person: `person_gramps_id`, `person_surname`, `person_given_name`.
- Family: `family_gramps_id`.
- Event: `event_gramps_id`.
- Place: `place_title`, `place_enclosed_by`, `place_gramps_id`.
- Source: `source_title`, `source_gramps_id`.
- Citation: `citation_page`, `citation_gramps_id`.
- Media: `media_desc`, `media_gramps_id`.
- Repository: `repository_gramps_id`.
- Note: `note_gramps_id`.
- Tag: `tag_name`.
- Reference: `reference_obj_handle`, `reference_ref_handle`.

## Version Notes

- Gramps 6 database version is `22`.
- Upgrade `21 -> 22` ensures every `Person` has `familysearch_sync` in JSON data.
- New databases are initialized with JSON storage support and then populated with metadata.
- Older blob-era details are out of scope except where they affect current Gramps 6 behavior.

## Query Caveats

- Sort order uses SQLite collation registered by Gramps.
- The backend registers a `regexp(expr, value)` SQL function.
- `reference` should be treated as an index/map, not the canonical object graph.
- `gender_stats` is cache data and can be rebuilt or refreshed.

## Source Anchors

- `gramps/plugins/db/dbapi/dbapi.py`
- `gramps/plugins/db/dbapi/sqlite.py`
- `gramps/gen/db/generic.py`
- `gramps/gen/db/upgrade.py`
- `gramps/gen/db/dbconst.py`
- `gramps/gen/lib/tableobj.py`
- `gramps/gen/lib/person.py`
- `gramps/gen/lib/family.py`
- `gramps/gen/lib/event.py`
- `gramps/gen/lib/place.py`
- `gramps/gen/lib/src.py`
- `gramps/gen/lib/citation.py`
- `gramps/gen/lib/media.py`
- `gramps/gen/lib/note.py`
- `gramps/gen/lib/repo.py`
- `gramps/gen/lib/tag.py`
