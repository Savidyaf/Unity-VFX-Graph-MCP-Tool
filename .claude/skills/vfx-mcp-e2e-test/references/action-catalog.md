# Action catalog

Source-of-truth derived from the actual code in `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/`. Used by the test agent to verify response shapes and by the evaluator to audit the report.

All success/error shapes follow `references/error-envelope-spec.md`. This catalog documents the **action-specific** keys.

## vfx_asset (4 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `create` | yes (no transaction) | `path` | `created.path`, `created.kind` (vfx/vfxblock/vfxoperator) |
| `list`   | no  | none | `assets[]` of `{guid, path, kind}` |
| `delete` | yes | `path` | (via Shape) — diffs/warnings/health, NO action-specific key (the inner `deleted` is dropped on the way out) |
| `assign` | yes (broken) | `path`, `gameObject` | `assigned` (string) — **bypasses ShapeMutation** so no health even with verbose=true |

## vfx_graph (11 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `get_info` | no | `graph` | `graph_path`, `child_count`, `system_count`, `system_names[]`, `space`, `bounds_setting_mode`, `update_mode` (last 3 are F3 stubs in baseline) |
| `save` | yes | `graph` | `saved` (string) — note: bypasses ShapeMutation similar to assign |
| `compile` | no (but BusyGate) | `graph` | `Ok`, `DurationMs`, `Errors[]` (PascalCase from VfxCompileResult) |
| `compilation_status` | no | `graph` | F4 baseline stub: `state`, `hint` |
| `read_console` | no | none | `lines[]` |
| `get_health` | no | `graph` | F4 baseline stub: `state`, `hint` |
| `set_space` | no (stub) | `graph`, `space` | F-A5 baseline stub: `state`, `hint` |
| `set_capacity` | no (stub) | `graph`, `capacity` | F-A5 baseline stub: `state`, `hint` |
| `set_bounds` | no (stub) | `graph`, `center`, `size` | F-A5 baseline stub: `state`, `hint` |
| `set_data_settings` | yes (conditional) | `graph`, `name`, `value` | `set_data_settings.{name, value}` on success; or stub `state`/`hint` if not graph-level |
| `discard_changes` | yes (no transaction) | `graph` | `discarded` (string) |

## vfx_node (11 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `list` | no | `graph` (+ optional `page_size`, `offset`) | `graph_path`, `total`, `offset`, `page_size`, `nodes[]` of `{token, type, x, y, parent_token, category, block_index}` |
| `add` | yes | `graph`, `type` (+ optional `x`, `y`) | `added[0].{token, type}` |
| `remove` | yes | `graph`, `token` | `removed[0].{token}` |
| `move` | yes (broken) | `graph`, `token`, `x`, `y` | `moved.{token, x, y}` — uses post-hoc obj mutation (F5 miss) |
| `duplicate` | yes | `graph`, `token` (+ optional `offset_x`, `offset_y`) | `duplicated.{source, token, type}` |
| `connect` | yes | `graph`, `from_token`, `from_slot`, `to_token`, `to_slot` | `connected.{from_token, from_slot, to_token, to_slot}` |
| `disconnect` | yes | same as connect | `disconnected.{from_token, from_slot, to_token, to_slot}` |
| `set_setting` | yes | `graph`, `token`, `name`, `value` | `set_setting.{token, name, value}` (value as string) |
| `set_property` | yes | same | `set_property.{token, name, value}` (value as string) |
| `get_setting` | no | `graph`, `token`, `name` | `token`, `name`, `value` (JToken or null) |
| `get_property` | no | same | `token`, `name`, `value` (JToken or null) |

## vfx_block (6 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `add` | yes | `graph`, `parent_token`, `type` (+ optional `index`) | `added[0].{token, type, parent_token}` |
| `remove` | yes | `graph`, `token` | `removed[0].{token}` |
| `reorder` | yes | `graph`, `token`, `new_index` | `reordered.{token, new_index}` |
| `set_activation` | yes | `graph`, `token`, `active` | `set_activation.{token, active}` |
| `set_attribute` | yes | `graph`, `token`, `attribute_name`, `value` | `set_attribute.{token, attribute_name, value}` |
| `list_attributes` | no (broken envelope) | none | currently `{"note": "..."}` — should be proper stub envelope or delegate to vfx_diag |

## vfx_property (4 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `add` | yes | `graph`, `type` (+ optional `x`, `y`) | `added[0].{token, type}` |
| `remove` | yes | `graph`, `token` | `removed[0].{token}` |
| `set_value` | yes | `graph`, `token`, `value` | `set_value.{token, value}` |
| `set_exposed` | yes | `graph`, `token`, `exposed` | `set_exposed.{token, exposed}` |

## vfx_subgraph (6 actions)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `create` | yes (no transaction) | `path` (.vfxblock or .vfxoperator) | `created.{path, kind}` |
| `add_ref` | yes (broken shape) | `graph`, `subgraph` (+ optional `x`, `y`) | `added.{token, subgraph}` — **single object, not array** (inconsistency) |
| `inline` | yes (deferred) | — | throws → `error.code="not_implemented"` |
| `extract` | yes (deferred) | — | same |
| `get_exposed` | no (stub envelope) | `graph`, `token` | currently `{"exposed": [], "note": "..."}` |
| `set_override` | yes | `graph`, `token`, `name`, `value` | `set_override.{token, name, value}` |

## vfx_recipe (1 action)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `list` | no (stale stub) | none | currently `{"recipes": [], "note": "Recipes deferred to v0.3.1."}` — should be lifted in v0.3.1 |

## vfx_batch (1 action)

| Action | Mutating | Required params | Success keys |
|---|---|---|---|
| `commit` | yes | `graph`, `ops[]` (each with `tool`, `action`, plus the per-op params) | shaped via `Shape(commit, verbose)` — `added[]`, `warnings[]`, `health` if verbose |

Allowed tool names in op: `vfx_asset`, `vfx_graph`, `vfx_node`, `vfx_block`, `vfx_property`, `vfx_subgraph`, `vfx_recipe`, `vfx_diag`. Only ops whose tool's `ApplyInTransaction` has a real implementation will succeed; the rest unwrap to `error.code="not_implemented"`.

`vfx_asset.delete` inside a batch is INTENTIONALLY blocked (post-delete YAML verification can't run on missing assets).

## vfx_diag (8 actions, all read-only)

| Action | Required params | Success keys |
|---|---|---|
| `list_node_types` | optional `page` (int) | `node_types[]`, `page`, `total`, `has_next` |
| `list_block_types` | optional `page` | `block_types[]`, `page`, `total`, `has_next` |
| `list_contexts` | optional `page` | `contexts[]`, `page`, `total`, `has_next` |
| `list_attributes` | optional `page` | F10 baseline stub: `state`, `hint`, `attributes[]` (empty) |
| `list_settings` | optional `page` | F10 baseline stub: `state`, `hint`, `settings[]` (empty) |
| `list_subgraphs` | optional `page` | `subgraphs[]` of `{fqn, kind}`, `page`, `total`, `has_next` |
| `read_console` | none | `lines[]` |
| `get_warnings` | none | `warnings[]` (always empty in baseline) |

## Notes on params

- **`graph` vs `path`:** `vfx_asset` and `vfx_subgraph.create` use `path`. Everything else uses `graph`.
- **`token` format:** `<prefix>_xxxxx` where `xxxxx` is 5 hex chars (FNV-1a 64 hash, lower 20 bits). The prefix is **per-category**, minted by `VfxIdentity.PrefixFor(VFXModel)` in `Editor/Kernel/VfxIdentity.cs`:

  | Category | Prefix | Example |
  |---|---|---|
  | `VFXContext` (Spawn/Init/Update/Output) | `ctx_` | `ctx_a3f21` |
  | `VFXBlock` (any block type) | `b_` | `b_9b332` |
  | `VFXOperator` (Add, Position, Multiply, …) | `n_` | `n_3c890` |
  | `VFXParameter` (exposed property) | `n_` | `n_7e215` |
  | `VFXSubgraphRef` / fallback | `n_` | `n_5d440` |

  Earlier drafts of this catalog and the v0.3.0 CHANGELOG claimed `n_xxxxx` across the board; that was documentation drift — the kernel has always used the per-category scheme (see `VfxIdentity.PrefixFor`). The synthetic token `@graph` is used internally for graph-level setting dispatch via `vfx_graph.set_data_settings`.
- **`verbose`:** boolean, default `false`. The test agent always passes `true`.
- **`unity_instance`:** required by `mcp__UnityMCP__execute_custom_tool` when multiple instances are connected. The skill specifies the exact value.
