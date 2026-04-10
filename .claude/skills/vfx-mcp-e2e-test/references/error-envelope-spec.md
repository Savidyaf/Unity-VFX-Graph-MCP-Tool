# Error envelope spec

The canonical response shapes for the `com.spiralingstudio.mcp.vfxgraph` package, derived from `Editor/Kernel/VfxResponseShaper.cs`. The test agent uses this to discriminate PASS from ANOMALY.

## Discriminator rule

A response is **success** iff it does NOT contain a top-level `error` key.
A response is **error** iff it contains a top-level `error` key.

There is **no `ok: true/false` discriminator** and **no `tool_version` field**.

## Success — mutating action (via ShapeMutation)

```jsonc
{
  // 1+ per-action mutation payload keys (e.g. "added", "removed", "moved", "set_setting", ...)
  "added":   [{"token": "n_a3f21", "type": "..."}],
  // optional: present iff commit had warnings
  "warnings": [...],
  // optional: present iff verbose=true AND commit.Health is non-null
  "health": {
    "YamlDiff": "...",
    "Compile":  "clean | warnings | errors",
    "Console":  "ok | warnings",
    "CompileMs": 1234,
    "YamlBytes": 5678
  }
}
```

## Success — read-only action (via ShapeRead)

```jsonc
{
  // 1+ payload keys per action; ShapeRead is currently a pass-through
  "graph_path": "...",
  "child_count": 0,
  "system_count": 0,
  "system_names": [],
  ...
}
```

## Success — pagination (vfx_diag list_*)

```jsonc
{
  "node_types": ["UnityEditor.VFX.Operator.Add", ...],  // key varies per action
  "page": 0,
  "total": 287,
  "has_next": true
}
```

## Error envelope

```jsonc
{
  "error": {
    "code":    "string, never null, never empty",   // e.g. "validation_error", "node_lost"
    "message": "string, may be empty",
    "hint":    "string, may be empty if no override registered",
    "details": { ... },          // optional: present iff details dictionary non-empty
    "retry_after_hint_ms": 1234  // optional: present iff RetryAfterHintMs.HasValue
  }
}
```

## "Honest not_implemented" envelope (read-only stubs)

Used by `vfx_graph.compilation_status`, `get_health`, `set_space`, `set_capacity`, `set_bounds`, and `vfx_diag.list_attributes`/`list_settings`. Wrapped in `ShapeRead`:

```jsonc
{
  "state": "not_implemented",
  "hint":  "actionable string explaining what to do instead",
  // optional payload arrays kept empty for shape compatibility:
  "attributes": [],   // only on vfx_diag.list_attributes
  "settings":   []    // only on vfx_diag.list_settings
}
```

## "Not implemented" via thrown NotImplementedException (vfx_subgraph.inline/extract)

Caught by the tool's outer try and surfaced as a regular error envelope:

```jsonc
{
  "error": {
    "code":    "not_implemented",
    "message": "v0.3.1",
    "hint":    "This action is scheduled for a future release."
  }
}
```

## Inconsistencies the test agent must flag as ANOMALY

These shapes diverge from the canonical specs above and are real bugs:

| Action | Wrong shape returned | Should be |
|---|---|---|
| `vfx_block.list_attributes` | `{"note": "Phase 5: use vfx_diag.list_attributes"}` | `{"state": "not_implemented", "hint": "..."}` |
| `vfx_subgraph.get_exposed` | `{"exposed": [], "note": "Phase 5: enumerate..."}` | `{"state": "not_implemented", "hint": "..."}` if stubbed; OR a real `exposed[]` per `references/known-deferred.md` |
| `vfx_recipe.list` | `{"recipes": [], "note": "Recipes deferred to v0.3.1."}` | A real recipes list (we are on v0.3.1, the deferral note is stale) OR `state="not_implemented"` envelope |
| `vfx_diag.get_warnings` | `{"warnings": []}` always | Real per-graph warnings or `state="not_implemented"` envelope |
| `vfx_subgraph.add_ref` | `"added"` is a single object, not an array | Should be array per `vfx_node.add` and `vfx_property.add` precedent |
| `vfx_asset.assign` (success) | Bypasses `ShapeMutation` — only `{"assigned": "..."}`, no `health` even with `verbose=true` | Should fold through `ShapeMutation` like all other mutating actions so the health gate output reaches the caller |
| `vfx_asset.create` | Bypasses transaction entirely — no health gate at all | Asset creation may legitimately skip the graph mutation gate, but the contract should be explicit about it (and the response should consistently lack `health` regardless of `verbose`) |
| `vfx_node.move` | Uses post-hoc `obj["moved"] = ...` instead of `ShapeMutation` (F5 cleanup miss) | Should fold through `ShapeMutation` like all 17 other mutators |
| `vfx_node.move` (intent op) | Records `Kind = "set_setting"` instead of `Kind = "move"` (F15 incomplete) | Should record `Kind = "move"` per CHANGELOG; fix is in `VfxNodeTool.Move()` and the verifier may need a `case "move":` arm |
| `vfx_node.move` (kernel call) | Bypasses `IVfxNodeOps.MoveNode` and sets `model.position` directly | Should call `VfxKernelContainer.NodeOps.MoveNode(path, token, position)` per the F15 contract method |
| `vfx_block.reorder` (intent op) | Records `Kind = "set_setting"` | Should be `Kind = "reorder"` or similar — minor inconsistency |
| `vfx_block.set_activation` (intent op) | Records `Kind = "set_setting"` | Should be `Kind = "set_activation"` — minor inconsistency |

The test agent does NOT need to verify the intent-op kinds directly (those are kernel-internal). It WILL surface the user-visible shape divergences (rows 1–8) as ANOMALY.

## Required behavior of every error envelope

- `error.code` is always non-null and non-empty
- `error.message` is non-null (may be empty string)
- `error.hint` is non-null (may be empty string if no override is registered)
- All three keys must be present even when empty

If any of those are missing → ANOMALY.
