# Primary And Table Objects

This file records JSON object field names and raw tuple order for Gramps table
objects. The JSON shape is preferred for other applications. Tuple order is the
`serialize()` order and is needed for blob/pickle records.

Field names that end in `_list` are arrays. Handles refer to other top-level
objects unless noted. `GrampsType` values are objects in JSON and `(value,
string)` tuples in blob serialization.

## Person

Source: `gramps/gen/lib/person.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `gender`, `primary_name`,
`alternate_names`, `death_ref_index`, `birth_ref_index`, `event_ref_list`,
`family_list`, `parent_family_list`, `media_list`, `address_list`,
`attribute_list`, `urls`, `lds_ord_list`, `citation_list`, `note_list`,
`change`, `tag_list`, `private`, `person_ref_list`, `familysearch_sync`.

Gender codes: `FEMALE = 0`, `MALE = 1`, `UNKNOWN = 2`, `OTHER = 3`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `gender` | integer 0-3 |
| 3 | `primary_name` | `Name` |
| 4 | `alternate_names` | array of `Name` |
| 5 | `death_ref_index` | integer index into `event_ref_list`, `-1` when unset |
| 6 | `birth_ref_index` | integer index into `event_ref_list`, `-1` when unset |
| 7 | `event_ref_list` | array of `EventRef` |
| 8 | `family_list` | array of `Family` handles where person is spouse/parent |
| 9 | `parent_family_list` | array of `Family` handles where person is child |
| 10 | `media_list` | array of `MediaRef` |
| 11 | `address_list` | array of `Address` |
| 12 | `attribute_list` | array of `Attribute` |
| 13 | `urls` | array of `Url` |
| 14 | `lds_ord_list` | array of `LdsOrd` |
| 15 | `citation_list` | array of `Citation` handles |
| 16 | `note_list` | array of `Note` handles |
| 17 | `change` | integer timestamp |
| 18 | `tag_list` | array of `Tag` handles |
| 19 | `private` | boolean |
| 20 | `person_ref_list` | array of `PersonRef` |
| 21 | `familysearch_sync` | `FamilySearchSync` |

## Family

Source: `gramps/gen/lib/family.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `father_handle`,
`mother_handle`, `child_ref_list`, `type`, `event_ref_list`, `media_list`,
`attribute_list`, `lds_ord_list`, `citation_list`, `note_list`, `change`,
`tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `father_handle` | `Person` handle/null |
| 3 | `mother_handle` | `Person` handle/null |
| 4 | `child_ref_list` | array of `ChildRef` to `Person` |
| 5 | `type` | `FamilyRelType` |
| 6 | `event_ref_list` | array of `EventRef` |
| 7 | `media_list` | array of `MediaRef` |
| 8 | `attribute_list` | array of `Attribute` |
| 9 | `lds_ord_list` | array of `LdsOrd` |
| 10 | `citation_list` | array of `Citation` handles |
| 11 | `note_list` | array of `Note` handles |
| 12 | `change` | integer timestamp |
| 13 | `tag_list` | array of `Tag` handles |
| 14 | `private` | boolean |

## Event

Source: `gramps/gen/lib/event.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `type`, `date`, `description`,
`place`, `citation_list`, `note_list`, `media_list`, `attribute_list`,
`change`, `tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `type` | `EventType` |
| 3 | `date` | `Date` |
| 4 | `description` | string |
| 5 | `place` | `Place` handle/null |
| 6 | `citation_list` | array of `Citation` handles |
| 7 | `note_list` | array of `Note` handles |
| 8 | `media_list` | array of `MediaRef` |
| 9 | `attribute_list` | array of `Attribute` |
| 10 | `change` | integer timestamp |
| 11 | `tag_list` | array of `Tag` handles |
| 12 | `private` | boolean |

## Place

Source: `gramps/gen/lib/place.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `title`, `long`, `lat`,
`placeref_list`, `name`, `alt_names`, `place_type`, `code`, `alt_loc`, `urls`,
`media_list`, `citation_list`, `note_list`, `change`, `tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `title` | string |
| 3 | `long` | longitude string |
| 4 | `lat` | latitude string |
| 5 | `placeref_list` | array of `PlaceRef` to parent/containing places |
| 6 | `name` | `PlaceName` |
| 7 | `alt_names` | array of `PlaceName` |
| 8 | `place_type` | `PlaceType` |
| 9 | `code` | string |
| 10 | `alt_loc` | array of `Location` |
| 11 | `urls` | array of `Url` |
| 12 | `media_list` | array of `MediaRef` |
| 13 | `citation_list` | array of `Citation` handles |
| 14 | `note_list` | array of `Note` handles |
| 15 | `change` | integer timestamp |
| 16 | `tag_list` | array of `Tag` handles |
| 17 | `private` | boolean |

## Source

Source: `gramps/gen/lib/src.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `title`, `author`, `pubinfo`,
`note_list`, `media_list`, `abbrev`, `change`, `attribute_list`,
`reporef_list`, `tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `title` | string |
| 3 | `author` | string |
| 4 | `pubinfo` | string |
| 5 | `note_list` | array of `Note` handles |
| 6 | `media_list` | array of `MediaRef` |
| 7 | `abbrev` | string |
| 8 | `change` | integer timestamp |
| 9 | `attribute_list` | array of `SrcAttribute` |
| 10 | `reporef_list` | array of `RepoRef` to `Repository` |
| 11 | `tag_list` | array of `Tag` handles |
| 12 | `private` | boolean |

## Citation

Source: `gramps/gen/lib/citation.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `date`, `page`, `confidence`,
`source_handle`, `note_list`, `media_list`, `attribute_list`, `change`,
`tag_list`, `private`.

Confidence codes: `VERY_LOW = 0`, `LOW = 1`, `NORMAL = 2`, `HIGH = 3`,
`VERY_HIGH = 4`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `date` | `Date` |
| 3 | `page` | string |
| 4 | `confidence` | integer 0-4 |
| 5 | `source_handle` | `Source` handle/null |
| 6 | `note_list` | array of `Note` handles |
| 7 | `media_list` | array of `MediaRef` |
| 8 | `attribute_list` | array of `SrcAttribute` |
| 9 | `change` | integer timestamp |
| 10 | `tag_list` | array of `Tag` handles |
| 11 | `private` | boolean |

## Media

Source: `gramps/gen/lib/media.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `path`, `mime`, `desc`,
`checksum`, `attribute_list`, `citation_list`, `note_list`, `change`, `date`,
`tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `path` | path or URI string |
| 3 | `mime` | MIME type string |
| 4 | `desc` | description string |
| 5 | `checksum` | checksum string |
| 6 | `attribute_list` | array of `Attribute` |
| 7 | `citation_list` | array of `Citation` handles |
| 8 | `note_list` | array of `Note` handles |
| 9 | `change` | integer timestamp |
| 10 | `date` | `Date` |
| 11 | `tag_list` | array of `Tag` handles |
| 12 | `private` | boolean |

`thumb` exists in memory but is not persisted by `serialize()`.

## Repository

Source: `gramps/gen/lib/repo.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `type`, `name`, `note_list`,
`address_list`, `urls`, `change`, `tag_list`, `private`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `type` | `RepositoryType` |
| 3 | `name` | string |
| 4 | `note_list` | array of `Note` handles |
| 5 | `address_list` | array of `Address` |
| 6 | `urls` | array of `Url` |
| 7 | `change` | integer timestamp |
| 8 | `tag_list` | array of `Tag` handles |
| 9 | `private` | boolean |

## Note

Source: `gramps/gen/lib/note.py`.

JSON fields: `_class`, `handle`, `gramps_id`, `text`, `format`, `type`,
`change`, `tag_list`, `private`.

Format codes: `FLOWED = 0`, `FORMATTED = 1`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `gramps_id` | string/null |
| 2 | `text` | `StyledText` |
| 3 | `format` | integer 0-1 |
| 4 | `type` | `NoteType` |
| 5 | `change` | integer timestamp |
| 6 | `tag_list` | array of `Tag` handles |
| 7 | `private` | boolean |

## Tag

Source: `gramps/gen/lib/tag.py`.

JSON fields: `_class`, `handle`, `name`, `color`, `priority`, `change`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `handle` | handle string/null |
| 1 | `name` | string |
| 2 | `color` | color string such as `#000000000000` |
| 3 | `priority` | integer |
| 4 | `change` | integer timestamp |
