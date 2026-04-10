---
name: using-vfx-graph-mcp
description: Use when an agent is creating, inspecting, or mutating Unity VFX Graph assets through the `vfx_*` MCP tools, especially when it must manage live graph tokens, distinguish `graph` vs `path`, batch compatible writes, or recover from `vfx_busy`, `node_lost`, `type_not_found`, `needs_explicit_target`, and `state="not_implemented"` responses.
---

# Using VFX Graph MCP

Operate VFX Graph through `vfx_*` tools without guessing.

**Core rule:** discover first, mutate by returned tokens, then verify before
chaining more edits.

## When to Use

- Creating or deleting `.vfx`, `.vfxblock`, or `.vfxoperator` assets
- Inspecting or editing graph contexts, blocks, properties, or subgraph refs
- Using `vfx_asset`, `vfx_graph`, `vfx_node`, `vfx_block`, `vfx_property`,
  `vfx_subgraph`, `vfx_batch`, or `vfx_diag`
- Recovering from `vfx_busy`, `node_lost`, `type_not_found`,
  `needs_explicit_target`, or `state="not_implemented"`

Do not use this for full release-gate sweeps. Use `vfx-mcp-e2e-test` for that.

## Workflow

1. **Preflight once**
   - Use `mcpforunity://instances` to identify the correct Unity instance and
     set it active if more than one editor is connected.
   - Check `mcpforunity://editor_state` and wait for idle before mutations.
   - Check `mcpforunity://custom-tools` and confirm the `vfx_*` tools are
     loaded.

2. **Discover, do not guess**
   - Use `vfx_asset.list` to find assets.
   - Use `vfx_diag.list_*` to discover valid type names, attributes, settings,
     and subgraphs, and `vfx_recipe.list` when you only need recipe names.
   - Start with the first page of large catalogs and request more pages only if
     needed.
   - Use `vfx_node.list` to recover live graph tokens and hierarchy.
   - Never invent type FQNs, slot names, or tokens from memory.

3. **Mutate by token**
   - Asset lifecycle uses `path`.
   - Graph reads and mutations use `graph`.
   - Capture every returned token immediately and treat it as opaque.
   - If the graph was reimported, discarded, or edited elsewhere, refresh with
     `vfx_node.list`.

4. **Batch only compatible writes**
   - Use `vfx_batch.commit` for grouped write operations on one graph.
   - Each entry in `ops[]` still needs the same fields as the direct call,
     including `graph` on graph-scoped actions.
   - Keep reads, compile/health inspection, and non-batchable asset operations
     outside the batch.

5. **Verify before continuing**
   - Default to terse responses to save tokens.
   - After a change set, use `vfx_graph.save`, `vfx_graph.compile`, then
     `vfx_graph.compilation_status`, `vfx_graph.get_health`, or `read_console`
     based on what you need to confirm.
   - Use `verbose=true` only when you need inline `health` or `warnings`.

## Quick Reference

| Need | Use |
|---|---|
| Discover valid node/block/context types | `vfx_diag.list_node_types`, `vfx_diag.list_block_types`, `vfx_diag.list_contexts` |
| Discover settings, attributes, or subgraphs | `vfx_diag.list_settings`, `vfx_diag.list_attributes`, `vfx_diag.list_subgraphs` |
| Discover recipe names only | `vfx_recipe.list` |
| Recover current graph structure and tokens | `vfx_node.list` |
| Create or delete assets | `vfx_asset.*` with `path` |
| Edit one graph | `vfx_*` calls with `graph` |
| Group multiple writes | `vfx_batch.commit` |
| Diagnose a failure | `vfx_graph.compile`, `vfx_graph.get_health`, `read_console` |

See `reference.md` for the batch matrix, recovery rules, and parameter gotchas.

## Example

**Minimal safe pattern for a new effect**

1. `vfx_asset.create` with `path="Assets/Example.vfx"`
2. `vfx_node.list` to confirm whether the new asset starts empty or already has
   template content
3. `vfx_diag.list_contexts` and `vfx_diag.list_node_types` to confirm exact
   catalog names for Spawn, Initialize, Update, and the output context in this
   Unity version
4. `vfx_node.add` for each context using `graph="Assets/Example.vfx"`
5. `vfx_node.connect` using the returned context tokens; for default context
   flow start with `from_slot="o"` and `to_slot="i"`
6. `vfx_node.list` to confirm the graph shape
7. Any additional block or property edits by returned token
8. `vfx_graph.save` -> `vfx_graph.compile` -> `vfx_graph.get_health`

If a token stops resolving, re-run `vfx_node.list` and retry with fresh tokens
instead of guessing.

## Common Mistakes

- Guessing a type FQN instead of listing the catalog first
- Mixing `path` and `graph`
- Omitting `graph` inside `vfx_batch.commit.ops[]` for actions that normally
  require it
- Reusing stale tokens after `discard_changes`, reimport, or out-of-band edits
- Retrying `state="not_implemented"` instead of reading the `hint`
- Putting read-only or non-batchable actions into `vfx_batch.commit`
- Passing `verbose=true` on every call when terse responses are enough
