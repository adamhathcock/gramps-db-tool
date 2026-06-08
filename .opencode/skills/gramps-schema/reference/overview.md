# Gramps Schema Overview

This is a source-indexed snapshot of the current Gramps schema surface for
cross-language consumers. The in-memory classes live under `gramps/gen/lib`.
Database access is defined by `gramps/gen/db/base.py` and implemented by
backends such as `gramps/gen/db/generic.py` and `gramps/plugins/db/dbapi`.

## Object Categories

Primary/table objects are stored in top-level database tables and are addressed
by `handle`.

| Class | Table name | Key constant | User ID | Source |
| --- | --- | --- | --- | --- |
| `Person` | `person` | `PERSON_KEY = 0` | `gramps_id` | `gramps/gen/lib/person.py` |
| `Family` | `family` | `FAMILY_KEY = 1` | `gramps_id` | `gramps/gen/lib/family.py` |
| `Source` | `source` | `SOURCE_KEY = 2` | `gramps_id` | `gramps/gen/lib/src.py` |
| `Event` | `event` | `EVENT_KEY = 3` | `gramps_id` | `gramps/gen/lib/event.py` |
| `Media` | `media` | `MEDIA_KEY = 4` | `gramps_id` | `gramps/gen/lib/media.py` |
| `Place` | `place` | `PLACE_KEY = 5` | `gramps_id` | `gramps/gen/lib/place.py` |
| `Repository` | `repository` | `REPOSITORY_KEY = 6` | `gramps_id` | `gramps/gen/lib/repo.py` |
| Reference map | `reference` | `REFERENCE_KEY = 7` | none | backend-maintained |
| `Note` | `note` | `NOTE_KEY = 8` | `gramps_id` | `gramps/gen/lib/note.py` |
| `Tag` | `tag` | `TAG_KEY = 9` | none | `gramps/gen/lib/tag.py` |
| `Citation` | `citation` | `CITATION_KEY = 10` | `gramps_id` | `gramps/gen/lib/citation.py` |

The class/key/name maps are in `gramps/gen/db/dbconst.py` as
`CLASS_TO_KEY_MAP`, `KEY_TO_CLASS_MAP`, and `KEY_TO_NAME_MAP`.

## Identifiers

`handle` is the database primary key for every table object. The code documents
handles as strings with `maxLength: 50` in JSON schemas. Some handle listing
methods may return bytes for speed in legacy/blob paths.

`gramps_id` is the user-visible identifier on all primary objects except `Tag`.
ID prefixes are configurable per object type through the DB API.

`change` is an integer timestamp in the format returned by `time.time()`.

`private` is a boolean privacy marker on primary objects that inherit
`PrivacyBase`. `Tag` does not have `private` or `gramps_id`.

`tag_list`, `citation_list`, and `note_list` are arrays of handles to primary
objects. Embedded refs such as `EventRef`, `MediaRef`, `ChildRef`, `PlaceRef`,
and `RepoRef` store their target handle in `ref`.

## Serialization Formats

There are two backend data formats in `gramps/gen/lib/serialize.py`:

| Serializer | Data field | Metadata field | Stored shape |
| --- | --- | --- | --- |
| `BlobSerializer` | `blob_data` | `value` | Pickled nested tuples/lists returned by `serialize()` |
| `JSONSerializer` | `json_data` | `json_data` | JSON object dictionaries from object state |

`DbGeneric.load(..., json_data=True)` creates new schemas with JSON data by
default. `DbGeneric.use_json_data()` decides whether an opened backend uses JSON
or blob data, then `set_serializer("json")` or `set_serializer("blob")` selects
the serializer.

For new external applications, use JSON object field names and the class
`get_schema()` methods as the preferred shape. Tuple order remains important for
raw blob/pickle reads, undo data, and code that calls `serialize()` directly.

## JSON Object Rules

Most model classes implement `get_schema()` returning JSON Schema-like
dictionaries. Object dictionaries include `_class` with the concrete class name.
Private Python attributes are exposed with public JSON names via
`get_object_state()` overrides when needed.

Gramps type classes inheriting `GrampsType` serialize as `(value, string)` and
have JSON fields:

| Field | Meaning |
| --- | --- |
| `_class` | Concrete type class such as `EventType` or `NameType` |
| `value` | Integer standard/custom type code |
| `string` | Custom type string; empty for non-custom values |

Common `GrampsType` subclasses include `AttributeType`, `ChildRefType`,
`EventRoleType`, `EventType`, `FamilyRelType`, `NameOriginType`, `NameType`,
`NoteType`, `PlaceType`, `RepositoryType`, `SourceMediaType`, and `UrlType`.

## Version And Metadata

`DbGeneric.VERSION` is currently `(22, 0, 0)` in `gramps/gen/db/generic.py`.
The persisted schema version is metadata key `version`, returned by
`get_schema_version()` and set by `set_schema_version()`.

Important metadata accessed by `DbGeneric.load()` includes `name_formats`,
`researcher`, `bookmarks`, and object ID prefix state. Backends provide
`_get_metadata()` and `_set_metadata()`.

## Source Maintenance Checklist

When updating this schema snapshot, check these source areas:

- Primary class `serialize()`, `unserialize()`, `get_schema()` methods.
- Secondary object `serialize()`, `unserialize()`, `get_schema()` methods.
- `gramps/gen/db/dbconst.py` key maps.
- `gramps/gen/db/generic.py` table map, serializer behavior, and `VERSION`.
- Schema migrations in `gramps/gen/db/upgrade.py`.
