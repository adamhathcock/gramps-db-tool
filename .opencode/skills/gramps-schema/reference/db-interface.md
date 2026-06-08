# Database Interface

The abstract DB API is in `gramps/gen/db/base.py`. Backends implement
`DbReadBase` and `DbWriteBase`. `DbGeneric` in `gramps/gen/db/generic.py`
provides the common implementation used by DBAPI-style backends.

## Open, Close, Metadata

| Method | Meaning |
| --- | --- |
| `load(directory, callback, mode=None, force_schema_upgrade=False, force_bsddb_upgrade=False)` | Open a database |
| `close()` | Close the database |
| `is_open()` | Return whether the database is open |
| `version_supported()` | Return whether the opened file version is supported |
| `get_schema_version()` | Implemented in `DbGeneric`, returns metadata `version` as int |
| `set_schema_version(value)` | Implemented in `DbGeneric`, writes metadata `version` |
| `get_dbid()` | Unique ID for this database on this computer |
| `get_dbname()` | Name for this database on this computer |
| `get_save_path()` | Save path or empty string |
| `get_summary()` | Summary dictionary, including people count/version/data version when possible |
| `requires_login()` | True for backends that need login; default False |
| `get_researcher()` / `set_researcher(owner)` | Database owner/researcher metadata |
| `get_mediapath()` / `set_mediapath(mediapath)` | Default media path |

## Object Access Pattern

For object type `X`, the DB surface generally includes:

| Operation | Method pattern |
| --- | --- |
| Get by handle | `get_x_from_handle(handle)` |
| Get by Gramps ID | `get_x_from_gramps_id(gramps_id)` |
| Test handle | `has_x_handle(handle)` |
| Test Gramps ID | `has_x_gramps_id(gramps_id)` |
| Get handles | `get_x_handles(...)` |
| Iterate handles | `iter_x_handles()` |
| Iterate objects | `iter_xs()` or object-specific plural |
| Cursor | `get_x_cursor()` |
| Count | `get_number_of_xs()` or object-specific plural |
| Raw data | `get_raw_x_data(handle)` |
| Add | `add_x(obj, transaction, set_gid=True)` for primary objects except tag |
| Commit | `commit_x(obj, transaction, change_time=None)` |
| Remove | Implemented by concrete DB classes, available in `DbGeneric` table map |

Concrete plural names are not always mechanically regular. Use the method names
below rather than generated guesses.

## Read Methods By Object

| Object | By handle | By Gramps ID | Handles | Iter objects | Iter handles | Cursor | Count | Raw |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Person` | `get_person_from_handle` | `get_person_from_gramps_id` | `get_person_handles` | `iter_people` | `iter_person_handles` | `get_person_cursor` | `get_number_of_people` | `get_raw_person_data` |
| `Family` | `get_family_from_handle` | `get_family_from_gramps_id` | `get_family_handles` | `iter_families` | `iter_family_handles` | `get_family_cursor` | `get_number_of_families` | `get_raw_family_data` |
| `Event` | `get_event_from_handle` | `get_event_from_gramps_id` | `get_event_handles` | `iter_events` | `iter_event_handles` | `get_event_cursor` | `get_number_of_events` | `get_raw_event_data` |
| `Place` | `get_place_from_handle` | `get_place_from_gramps_id` | `get_place_handles` | `iter_places` | `iter_place_handles` | `get_place_cursor`, `get_place_tree_cursor` | `get_number_of_places` | `get_raw_place_data` |
| `Source` | `get_source_from_handle` | `get_source_from_gramps_id` | `get_source_handles` | `iter_sources` | `iter_source_handles` | `get_source_cursor` | `get_number_of_sources` | `get_raw_source_data` |
| `Citation` | `get_citation_from_handle` | `get_citation_from_gramps_id` | `get_citation_handles` | `iter_citations` | `iter_citation_handles` | `get_citation_cursor` | `get_number_of_citations` | `get_raw_citation_data` |
| `Media` | `get_media_from_handle` | `get_media_from_gramps_id` | `get_media_handles` | `iter_media` | `iter_media_handles` | `get_media_cursor` | `get_number_of_media` | `get_raw_media_data` |
| `Repository` | `get_repository_from_handle` | `get_repository_from_gramps_id` | `get_repository_handles` | `iter_repositories` | `iter_repository_handles` | `get_repository_cursor` | `get_number_of_repositories` | `get_raw_repository_data` |
| `Note` | `get_note_from_handle` | `get_note_from_gramps_id` | `get_note_handles` | `iter_notes` | `iter_note_handles` | `get_note_cursor` | `get_number_of_notes` | `get_raw_note_data` |
| `Tag` | `get_tag_from_handle`, `get_tag_from_name` | none | `get_tag_handles` | `iter_tags` | `iter_tag_handles` | `get_tag_cursor` | `get_number_of_tags` | `get_raw_tag_data` |

Handle getters raise `HandleError` if the handle does not exist, except proxies
may return `None` for filtered-out objects. Gramps ID getters return `None` when
not found.

## Write Methods

`DbWriteBase` defines add and commit methods for all table objects:

- `add_person`, `add_family`, `add_event`, `add_place`, `add_source`,
  `add_citation`, `add_media`, `add_repository`, `add_note`, `add_tag`.
- `commit_person`, `commit_family`, `commit_event`, `commit_place`,
  `commit_source`, `commit_citation`, `commit_media`, `commit_repository`,
  `commit_note`, `commit_tag`.

Add methods for primary objects accept `set_gid=True` except `add_tag`, which
does not use Gramps IDs. Commit methods accept optional `change_time`.

Transactions use `DbTxn` from `gramps/gen/db/txn.py`. Undo/redo storage is
available through `get_undodb()`, `undo(update_history)`, and
`redo(update_history)` in concrete implementations.

## ID Prefixes

ID prefix methods configure user-visible `gramps_id` generation:

- `set_prefixes(person, media, family, source, citation, place, event, repository, note)`.
- `set_person_id_prefix`, `set_family_id_prefix`, `set_event_id_prefix`.
- `set_place_id_prefix`, `set_source_id_prefix`, `set_citation_id_prefix`.
- `set_media_id_prefix`, `set_repository_id_prefix`, `set_note_id_prefix`.

Next-ID methods:

- `find_next_person_gramps_id`, `find_next_family_gramps_id`.
- `find_next_event_gramps_id`, `find_next_place_gramps_id`.
- `find_next_source_gramps_id`, `find_next_citation_gramps_id`.
- `find_next_media_gramps_id`, `find_next_repository_gramps_id`.
- `find_next_note_gramps_id`.

Existence methods are `has_*_gramps_id()` for all primary objects except `Tag`.

## Bookmarks

Bookmark getters return handle lists:

- `get_bookmarks()` for person bookmarks.
- `get_family_bookmarks`, `get_event_bookmarks`, `get_place_bookmarks`.
- `get_source_bookmarks`, `get_citation_bookmarks`.
- `get_media_bookmarks`, `get_note_bookmarks`, `get_repo_bookmarks`.

Bookmark session state methods include `db_has_bm_changes()` and
`report_bm_change()`.

## Type Discovery

The DB API exposes custom/in-use type discovery:

- `get_child_reference_types`.
- `get_event_roles`, `get_event_types`, `get_family_event_types`,
  `get_person_event_types`.
- `get_event_attribute_types`, `get_family_attribute_types`,
  `get_media_attribute_types`, `get_person_attribute_types`,
  `get_source_attribute_types`.
- `get_family_relation_types`, `get_name_types`, `get_note_types`,
  `get_origin_types`, `get_place_types`, `get_repository_types`,
  `get_source_media_types`, `get_url_types`.

`get_family_event_types()` and `get_person_event_types()` are deprecated in
favor of `get_event_types()`.

## Names And Surnames

Name grouping methods:

- `get_surname_list()` returns locale-sorted surnames.
- `get_name_group_keys()` returns defined grouped names.
- `get_name_group_mapping(surname)` returns a grouping name for a surname.
- `has_name_group_key(key)` tests if a group key exists.

`add_to_surname_list(person, batch_transaction, name)` is part of write-side
surname index maintenance.

## Signals And Rebuild

`request_rebuild()` notifies clients that all dependent internal data should be
rebuilt. `DbGeneric.__signals__` defines add/update/delete/rebuild signals for
`person`, `family`, `event`, `place`, `repository`, `source`, `citation`,
`media`, `note`, and `tag`, plus long-operation and home-person signals.

## Dynamic Table Function Map

`DbGeneric.__tables` maps class names to handle, ID, cursor, add, commit,
iterate, count, raw, and delete functions. Use this when implementing generic
tools that operate over all table objects.

`DbReadBase.method(fmt, *args)` is a convenience wrapper that lowercases args
and calls `getattr(self, fmt % args, None)`.
