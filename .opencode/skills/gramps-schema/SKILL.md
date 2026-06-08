---
name: gramps-schema
description: Use when working with the Gramps schema, gramps/gen/db/base.py, gramps/gen/lib serialization, database object model, raw records, JSON data, or cross-language integrations that need the current Gramps data shape.
---

# Gramps Schema

Use this skill when another application, language, importer, exporter, sync
tool, database reader, API, or generated type model needs the current Gramps
database/object schema.

The schema authority is the current Gramps source, especially:

- `gramps/gen/lib/*.py`: model classes, `serialize()`, `unserialize()`, and `get_schema()`.
- `gramps/gen/db/base.py`: read/write database interface.
- `gramps/gen/db/dbconst.py`: table keys and class/name maps.
- `gramps/gen/db/generic.py`: table function map, serializer selection, schema version, and raw object loading.
- `gramps/gen/lib/serialize.py`: blob and JSON serializer behavior.

For non-Python integrations, prefer the JSON object field names documented in
the references. Use tuple indexes only when reading raw blob/pickle records or
working with code paths that call `serialize()` directly.

Reference files:

- `reference/overview.md`: tables, identifiers, serializers, and shared rules.
- `reference/primary-objects.md`: primary/table object fields and tuple order.
- `reference/secondary-objects.md`: embedded object fields and tuple order.
- `reference/relationships.md`: handle reference graph.
- `reference/db-interface.md`: database API grouped by object operation.

When a reference and source disagree, trust the source and update the reference.
The references are a current-state schema snapshot intended for use by other
applications and languages.
