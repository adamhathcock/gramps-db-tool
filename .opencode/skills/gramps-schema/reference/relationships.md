# Handle Relationships

Gramps relationships between primary objects are stored as handles. There are
no foreign-key objects in the Python model; references are maintained by object
fields and backend reference-map indexing.

## Primary Object References

| From | Field | To | Notes |
| --- | --- | --- | --- |
| `Person` | `event_ref_list[].ref` | `Event` | Person events, including birth/death indexes |
| `Person` | `family_list[]` | `Family` | Families where person is parent/spouse |
| `Person` | `parent_family_list[]` | `Family` | Families where person is child |
| `Person` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Person` | `citation_list[]` | `Citation` | Direct citations |
| `Person` | `note_list[]` | `Note` | Direct notes |
| `Person` | `tag_list[]` | `Tag` | Tags |
| `Person` | `person_ref_list[].ref` | `Person` | Associations to other people |
| `Person` | `lds_ord_list[].place` | `Place` | LDS ordinance place |
| `Person` | `lds_ord_list[].famc` | `Family` | LDS family context |
| `Family` | `father_handle` | `Person` | Father/parent handle |
| `Family` | `mother_handle` | `Person` | Mother/parent handle |
| `Family` | `child_ref_list[].ref` | `Person` | Children via `ChildRef` |
| `Family` | `event_ref_list[].ref` | `Event` | Family events |
| `Family` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Family` | `citation_list[]` | `Citation` | Direct citations |
| `Family` | `note_list[]` | `Note` | Direct notes |
| `Family` | `tag_list[]` | `Tag` | Tags |
| `Family` | `lds_ord_list[].place` | `Place` | LDS ordinance place |
| `Event` | `place` | `Place` | Event location |
| `Event` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Event` | `citation_list[]` | `Citation` | Direct citations |
| `Event` | `note_list[]` | `Note` | Direct notes |
| `Event` | `tag_list[]` | `Tag` | Tags |
| `Place` | `placeref_list[].ref` | `Place` | Place hierarchy/reference |
| `Place` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Place` | `citation_list[]` | `Citation` | Direct citations |
| `Place` | `note_list[]` | `Note` | Direct notes |
| `Place` | `tag_list[]` | `Tag` | Tags |
| `Source` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Source` | `note_list[]` | `Note` | Direct notes |
| `Source` | `reporef_list[].ref` | `Repository` | Repositories that hold the source |
| `Source` | `tag_list[]` | `Tag` | Tags |
| `Citation` | `source_handle` | `Source` | Cited source |
| `Citation` | `media_list[].ref` | `Media` | Via `MediaRef` |
| `Citation` | `note_list[]` | `Note` | Direct notes |
| `Citation` | `tag_list[]` | `Tag` | Tags |
| `Media` | `citation_list[]` | `Citation` | Direct citations |
| `Media` | `note_list[]` | `Note` | Direct notes |
| `Media` | `tag_list[]` | `Tag` | Tags |
| `Repository` | `note_list[]` | `Note` | Direct notes |
| `Repository` | `tag_list[]` | `Tag` | Tags |
| `Note` | styled links | any primary object | `Note.get_links()` can contain Gramps handle links |
| `Note` | `tag_list[]` | `Tag` | Tags |

Embedded objects can also contain `citation_list` and `note_list` if they
inherit `CitationBase` or `NoteBase`; these should be treated as references to
`Citation` and `Note` respectively.

## Backlink Discovery

`DbReadBase.find_backlink_handles(handle, include_classes=None)` returns an
iterator of `(class_name, handle)` tuples for objects that reference a handle.
The default contract allows a slow sequential scan; backends can provide faster
reference-map implementations.

## Reference Mutation

Primary objects implement methods such as `has_handle_reference()`,
`remove_handle_references()`, and `replace_handle_reference()`. These are used
to keep references consistent when objects are deleted or merged.

External applications should not infer bidirectional consistency from one side
alone. For example, a `Family.child_ref_list` and a `Person.parent_family_list`
both describe the same family relationship but are stored separately.

## Place Tree

`get_place_tree_cursor()` iterates `Place` objects in place hierarchy order.
Place hierarchy is represented by `Place.placeref_list[].ref` rather than a
dedicated parent column.
