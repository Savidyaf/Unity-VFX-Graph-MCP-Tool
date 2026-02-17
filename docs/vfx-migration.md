# Migration: `manage_vfx` to `manage_vfx_graph`

Use `manage_vfx_graph` for graph-editing operations and keep `manage_vfx` for mixed component workflows.

## Recommended migration

- Existing: `manage_vfx` with `action: "vfx_add_node"`
- Preferred: `manage_vfx_graph` with `action: "add_node"`

## Why migrate

- Narrower, explicit schema for graph operations.
- Cleaner error handling and action validation.
- Easier to evolve without affecting particle/line/trail actions.

## Compatibility

`graph_add_node` remains accepted as an alias for `add_node` during migration.
