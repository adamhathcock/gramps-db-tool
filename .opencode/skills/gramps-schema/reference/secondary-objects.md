# Secondary And Embedded Objects

Secondary objects are embedded inside primary object records. They do not have
top-level database tables unless they point to a primary object through a
handle. Their raw tuple order comes from each class `serialize()` method.

## Common Base Tuples

Many secondary objects compose these base tuple fragments:

| Base | JSON field | Tuple shape | Source |
| --- | --- | --- | --- |
| `PrivacyBase` | `private` | boolean | `privacybase.py` |
| `CitationBase` | `citation_list` | array of `Citation` handles | `citationbase.py` |
| `NoteBase` | `note_list` | array of `Note` handles | `notebase.py` |
| `MediaBase` | `media_list` | array of `MediaRef` | `mediabase.py` |
| `AttributeBase` | `attribute_list` | array of `Attribute` | `attrbase.py` |
| `SrcAttributeBase` | `attribute_list` | array of `SrcAttribute` | `attrbase.py` |
| `AddressBase` | `address_list` | array of `Address` | `addressbase.py` |
| `UrlBase` | `urls` | array of `Url` | `urlbase.py` |
| `EventBase` | `event_ref_list` | array of `EventRef` | `eventbase.py` |
| `LdsOrdBase` | `lds_ord_list` | array of `LdsOrd` | `ldsordbase.py` |
| `RefBase` | `ref` | handle string/null | `refbase.py` |

## Date

Source: `gramps/gen/lib/date.py`.

JSON fields: `_class`, `calendar`, `modifier`, `quality`, `dateval`, `text`,
`sortval`, `newyear`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `calendar` | integer calendar code |
| 1 | `modifier` | integer modifier code |
| 2 | `quality` | integer quality code |
| 3 | `dateval` | tuple/list date value |
| 4 | `text` | date text string; can be blanked by `no_text_date=True` |
| 5 | `sortval` | integer sort value |
| 6 | `newyear` | integer new-year rule |

Legacy blob data may contain a six-field date tuple without `newyear`; current
`unserialize()` still accepts that form and defaults `newyear` to `0`.

## GrampsType

Source: `gramps/gen/lib/grampstype.py`.

JSON fields: `_class`, `value`, `string`.

Tuple order: `(value, string)`. When `value` is not custom, `string` is reset to
an empty string by `unserialize()`.

## Name

Source: `gramps/gen/lib/name.py`.

JSON fields: `_class`, `private`, `citation_list`, `note_list`, `date`,
`first_name`, `surname_list`, `suffix`, `title`, `type`, `group_as`, `sort_as`,
`display_as`, `call`, `nick`, `famnick`.

Tuple order:

| Index | Field | Shape |
| --- | --- | --- |
| 0 | `private` | boolean |
| 1 | `citation_list` | array of `Citation` handles |
| 2 | `note_list` | array of `Note` handles |
| 3 | `date` | `Date` |
| 4 | `first_name` | string |
| 5 | `surname_list` | array of `Surname` |
| 6 | `suffix` | string |
| 7 | `title` | string |
| 8 | `type` | `NameType` |
| 9 | `group_as` | string |
| 10 | `sort_as` | integer |
| 11 | `display_as` | integer |
| 12 | `call` | call name string |
| 13 | `nick` | nickname string |
| 14 | `famnick` | family nickname string |

## Surname

Source: `gramps/gen/lib/surname.py`.

JSON fields: `_class`, `surname`, `prefix`, `primary`, `origintype`,
`connector`.

Tuple order: `(surname, prefix, primary, origintype, connector)`.

## EventRef

Source: `gramps/gen/lib/eventref.py`.

JSON fields: `_class`, `private`, `citation_list`, `note_list`,
`attribute_list`, `ref`, `role`.

Tuple order: `(private, citation_list, note_list, attribute_list, ref, role)`.
`ref` is an `Event` handle. `role` is `EventRoleType`.

## ChildRef

Source: `gramps/gen/lib/childref.py`.

JSON fields: `_class`, `private`, `citation_list`, `note_list`, `ref`, `frel`,
`mrel`.

Tuple order: `(private, citation_list, note_list, ref, frel, mrel)`. `ref` is a
`Person` handle. `frel` and `mrel` are `ChildRefType` values for father and
mother relationship type.

## MediaRef

Source: `gramps/gen/lib/mediaref.py`.

JSON fields: `_class`, `private`, `citation_list`, `note_list`,
`attribute_list`, `ref`, `rect`.

Tuple order: `(private, citation_list, note_list, attribute_list, ref, rect)`.
`ref` is a `Media` handle. `rect` is `null` or a four-integer array.

## PersonRef

Source: `gramps/gen/lib/personref.py`.

JSON fields: `_class`, `private`, `citation_list`, `note_list`,
`attribute_list`, `ref`, `rel`.

Tuple order follows the class serializer and stores privacy, citations, notes,
attributes, referenced `Person` handle, and relationship string/type data.

## Attribute And SrcAttribute

Sources: `gramps/gen/lib/attribute.py`, `gramps/gen/lib/srcattribute.py`.

`AttributeRoot` tuple order is `(private, type, value)`. `Attribute` uses
`AttributeType`; `SrcAttribute` uses `SrcAttributeType`. JSON fields include
`_class`, `private`, `type`, and `value`.

## Address

Source: `gramps/gen/lib/address.py`.

Address combines `PrivacyBase`, `CitationBase`, `NoteBase`, `DateBase`, and
location fields. JSON fields include `_class`, `private`, `citation_list`,
`note_list`, `date`, `street`, `locality`, `city`, `county`, `state`,
`country`, `postal`, and `phone`.

## Location

Source: `gramps/gen/lib/location.py`.

JSON fields include `_class`, `street`, `locality`, `city`, `county`, `state`,
`country`, `postal`, and `phone`. It is used by `Place.alt_loc`.

## Url

Source: `gramps/gen/lib/url.py`.

JSON fields include `_class`, `private`, `path`, `desc`, and `type`.
`type` is `UrlType`.

## PlaceName

Source: `gramps/gen/lib/placename.py`.

JSON fields include `_class`, `value`, `date`, `lang`, and `private` where
present in the class schema. It is used by `Place.name` and `Place.alt_names`.

## PlaceRef

Source: `gramps/gen/lib/placeref.py`.

JSON fields include `_class`, `ref`, and `date`. `ref` is a `Place` handle.
It is used by `Place.placeref_list`.

## RepoRef

Source: `gramps/gen/lib/reporef.py`.

JSON fields include `_class`, `ref`, `call_number`, and `media_type`.
`ref` is a `Repository` handle. `media_type` is `SourceMediaType`.

## LdsOrd

Source: `gramps/gen/lib/ldsord.py`.

JSON fields include ordinance type/status/date/place/family/citation/note data.
References may include `place` as a `Place` handle and `famc` as a `Family`
handle.

## StyledText And StyledTextTag

Sources: `gramps/gen/lib/styledtext.py`, `gramps/gen/lib/styledtexttag.py`.

`StyledText` JSON fields: `_class`, `string`, `tags`.

`StyledText` tuple order: `(string, tags)`.

`StyledTextTag` stores a style type and ranges. Ranges are start/end integer
pairs over the styled string.

## FamilySearchSync

Sources: `gramps/gen/lib/fs/familysearchsync.py`,
`gramps/gen/lib/fs/familysearchsyncbase.py`.

`Person.familysearch_sync` stores FamilySearch synchronization status. The DB
read API exposes `get_familysearch_person_status(person_handle, default=None)`,
which returns keys such as `fsid`, `is_root`, `status_ts`, `confirmed_ts`,
`gramps_modified_ts`, `fs_modified_ts`, `essential_conflict`, and `conflict`
when present.

## Researcher

Source: `gramps/gen/lib/researcher.py`.

Stored as database metadata key `researcher`. It records owner/researcher
identity data and is returned by `DbReadBase.get_researcher()`.
