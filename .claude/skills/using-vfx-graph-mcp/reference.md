# VFX MCP Reference

## Contents

- Safe default workflow
- Parameter rules
- Batch matrix
- Recovery patterns
- Token rules
- Common catalog defaults
- Known constraints

## Safe Default Workflow

1. Read `mcpforunity://instances` and pin the correct Unity instance if more
   than one editor is connected.
2. Read `mcpforunity://editor_state` and wait for `isCompiling=false` before
   graph mutations.
3. Read `mcpforunity://custom-tools` and confirm the `vfx_*` tools are loaded.
4. If you need a new asset, call `vfx_asset.create` with `path`.
5. After asset creation, run `vfx_node.list` if you need to confirm whether the
   new graph starts empty or already contains template content.
6. Before adding nodes or blocks, use `vfx_diag.list_contexts`,
   `vfx_diag.list_node_types`, `vfx_diag.list_block_types`, or
   `vfx_diag.list_settings` to confirm the exact catalog names.
7. Start large catalog reads at the first page and request more only if you
   still need additional entries.
8. For default context-to-context flow, `o`, `i`, `flow`, an empty string, and
   `0` all address the first flow port. Start with `from_slot="o"` and
   `to_slot="i"` or use `0`/`0`. Use non-negative integer strings only when you
   intentionally need another flow index.
9. After mutations, use `vfx_node.list` to confirm the live graph shape.
10. End a change set with `vfx_graph.save`, `vfx_graph.compile`, then
    `vfx_graph.compilation_status`, `vfx_graph.get_health`, or `read_console`.

Do not guess catalog names from memory. Even the "obvious" output context can
drift across Unity versions.

## Parameter Rules

| Tool/action family | Key parameter rule |
|---|---|
| `vfx_asset.create`, `vfx_asset.delete`, `vfx_asset.assign` | Use `path` |
| `vfx_subgraph.create` | Use `path` |
| Graph reads and graph mutations | Use `graph` |
| `vfx_batch.commit` | Use top-level `graph` plus non-empty `ops[]`; each op uses the same action fields as the direct tool call and still repeats `graph` when the direct action requires it |
| `verbose` | Default `false`; set `true` only when inline `health` or `warnings` matter |
| Returned tokens | Store and reuse exactly; never synthesize them |

For exposed parameters, prefer `vfx_property.set_value` and
`vfx_property.set_exposed` instead of guessing internal slot names.

## Batch Matrix

| Put inside `vfx_batch.commit` | Keep outside `vfx_batch.commit` |
|---|---|
| `vfx_node.add/remove/move/duplicate/connect/disconnect/set_setting/set_property` | `vfx_node.list`, `vfx_node.get_setting`, `vfx_node.get_property` |
| `vfx_block.add/remove/reorder/set_activation/set_attribute` | All `vfx_diag.*` reads |
| `vfx_property.add/remove/set_value/set_exposed` | `vfx_recipe.list` |
| `vfx_subgraph.add_ref/set_override` | `vfx_asset.create/list/delete` |
| `vfx_graph.save/set_data_settings` | `vfx_subgraph.create` |
| `vfx_asset.assign` only | `vfx_graph.get_info`, `compile`, `compilation_status`, `get_health`, `read_console`, `discard_changes`, `set_space`, `set_capacity`, `set_bounds` |

If an operation is read-only, asset-lifecycle oriented, or explicitly deferred,
keep it outside the batch even if it looks similar to a batchable action.

Keep one batch focused on one graph. Do not mix asset paths or stale tokens
across unrelated edit sequences.

## Recovery Patterns

| Signal | Meaning | Do next | Avoid |
|---|---|---|---|
| `asset_pipeline_busy` (or legacy host-level `vfx_busy`) | Unity is compiling, importing, or otherwise busy | Wait `retry_after_hint_ms`, re-check editor idle state, then retry with bounded backoff | `refresh_unity`, parallel write storms, blind rapid retries |
| `node_lost` | The token no longer resolves to a live model | Re-run `vfx_node.list`, recover fresh tokens, retry with the new token | Reusing stale tokens after reimport, `discard_changes`, or external edits |
| `type_not_found`, `unknown_node_type`, `unknown_block_type` | The requested type is not in the current catalog | Use `vfx_diag.list_*` to discover the real FQN | Guessing a near-match type name |
| `asset_not_found` | The path is wrong or the asset no longer exists | Use `vfx_asset.list` or fix the path | Sending more graph calls against a missing asset |
| `state="not_implemented"` | The action is a documented stub or deferred path | Read the `hint` and switch to the documented alternative | Retrying as if it were transient |
| `needs_explicit_target` | A convenience wrapper found zero or multiple candidate targets | Switch to explicit `vfx_node.set_setting` or `set_property` on a specific token | Retrying the wrapper unchanged |
| `compile_error` | The graph compiled with real VFX errors | Inspect `errors[]` and `read_console`, then fix or discard the offending changes | Chaining more edits without understanding the compile failure |

`not_implemented` can surface either as a top-level `state` or as
`error.code="not_implemented"`. In both cases, change approach instead of
hammering retry.

## Token Rules

- Tokens are opaque identifiers returned by the tools.
- Common prefixes are `ctx_` for contexts, `b_` for blocks, and `n_` for
  operators, parameters, and subgraph refs.
- Treat the prefix as diagnostic only. Never construct a token by hand.
- After `discard_changes`, asset deletion, sidecar loss, reimport, or
  out-of-band graph edits, refresh through `vfx_node.list`.

## Common Catalog Defaults

Prefer catalog discovery over memory. In this repo's docs and tests, common
context types include:

- `UnityEditor.VFX.VFXBasicSpawner`
- `UnityEditor.VFX.VFXBasicInitialize`
- `UnityEditor.VFX.VFXBasicUpdate`
- `UnityEditor.VFX.VFXPlanarPrimitiveOutput`

This package is URP-first, so treat those names as examples, not guarantees.
Always confirm availability through `vfx_diag.list_contexts` or
`vfx_diag.list_node_types` in the current Unity/VFX Graph version.

## Known Constraints

- `vfx_block.list_attributes` is not the discovery path; use
  `vfx_diag.list_attributes`.
- `vfx_subgraph.inline` and `vfx_subgraph.extract` may legitimately surface
  `not_implemented`.
- `vfx_recipe.list` is a catalog; recipe execution is deferred.
- `vfx_asset.delete` is intentionally not batchable.
