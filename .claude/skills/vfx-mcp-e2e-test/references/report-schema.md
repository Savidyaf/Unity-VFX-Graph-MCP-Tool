# Report schema

The test agent's output. Produced ONCE at the end of a complete run (or partial run on stop condition).

## Top-level shape

```jsonc
{
  "schema_version": "1.0",
  "package": "com.spiralingstudio.mcp.vfxgraph",
  "package_version": "0.3.1",
  "unity_instance": "VFX-MPC-wip@ca9e7d133faa7c96",
  "unity_version": "6000.4.0f1",
  "started_at": "ISO-8601 timestamp from agent's first call",
  "completed_at": "ISO-8601 timestamp from agent's last call",
  "phases": [ /* one entry per phase 1-6, in order */ ],
  "summary": {
    "total":   0,
    "pass":    0,
    "fail":    0,
    "anomaly": 0,
    "skipped": 0,
    "known_deferred_seen": [
      { "step": "1.5", "tool": "vfx_diag", "action": "list_attributes", "what": "still returns state=not_implemented" }
    ],
    "unexpected_errors": [
      { "step": "3.7", "tool": "vfx_node", "action": "list", "code": "vfx_exception", "message": "..." }
    ],
    "stop_reason": null
  }
}
```

## Phase entry

```jsonc
{
  "phase": 1,
  "name": "Discovery",
  "started_at": "...",
  "completed_at": "...",
  "steps": [ /* one entry per step in this phase */ ]
}
```

## Step entry

```jsonc
{
  "step": "1.5",
  "tool": "vfx_diag",
  "action": "list_attributes",
  "params_summary": "page=0",
  "expected": "attributes[] non-empty (per known-deferred.md, lifted in this run)",
  "observed_keys": ["state", "hint", "attributes"],
  "status": "ANOMALY",
  "raw_response_excerpt": "{\"state\":\"not_implemented\",\"hint\":\"Built-in attribute enumeration deferred...\"}",
  "notes": "Still stubbed; should be lifted per references/known-deferred.md item F10"
}
```

## Status field — strict semantics

| Status | When to use |
|---|---|
| `PASS` | Call succeeded AND every key in `expected` is in `observed_keys` AND no shape divergence from `references/error-envelope-spec.md` |
| `FAIL` | Call returned an `error` envelope when success was expected, OR threw, OR a precondition was unmet, OR an "EXPECT FAIL" row did NOT return an error envelope |
| `ANOMALY` | Call appears successful but: missing expected keys, returned `state="not_implemented"` where implementation was expected per `known-deferred.md`, returned the wrong envelope shape (per the inconsistencies table in `error-envelope-spec.md`), or an "EXPECT ANOMALY" row in the skill explicitly says so |
| `SKIPPED` | A precondition step failed (e.g. earlier step removed a token this row depends on); record `notes` with the reason |

## Token table (do NOT include in final report)

The agent maintains a private scratch dict like:
```jsonc
{
  "$FIXTURE":         "Assets/VfxE2EFixtures/E2EThruster.vfx",
  "$SUBGRAPH":        "Assets/VfxE2EFixtures/E2EThrusterMath.vfxoperator",
  "$SPARE_SUB":       "Assets/VfxE2EFixtures/E2ESpare.vfxoperator",
  "$spawn_ctx":       "ctx_a3f21",
  "$init_ctx":        "ctx_b8c14",
  "$update_ctx":      "ctx_d2e09",
  "$output_ctx":      "ctx_f1a45",
  "$position_op":     "n_3c890",    // cleared after step 3.17
  "$float_param":     "n_7e215",
  "$setattr_block":   "b_9b332",
  "$drag_block":      "b_2a661",
  "$drag_block_copy": "b_4f128",
  "$subgraph_ref":    "n_5d440"
}
```

Token prefixes are **per-category** (`ctx_` for `VFXContext`, `b_` for `VFXBlock`, `n_` for `VFXOperator` / `VFXParameter` / `VFXSubgraphRef`). See `references/action-catalog.md` for the canonical table.

This is internal state. It is NOT part of the report.

## raw_response_excerpt rules

- Truncate to 400 characters.
- Strip newlines so the excerpt is one line.
- If the response is shorter than 400 chars, include in full.
- For pagination responses, prefer the first page's metadata over the items array — the items consume the whole budget.

## Final reporting checklist

Before producing the final message, the agent must:
- ✅ All 6 phases have a `steps[]` array (even if empty for phase that was fully skipped)
- ✅ `summary.total` equals the sum of pass + fail + anomaly + skipped
- ✅ `summary.known_deferred_seen` has one entry per ANOMALY caused by an unlifted known-deferred item
- ✅ `summary.unexpected_errors` has one entry per FAIL with an error code not documented in `error-envelope-spec.md`
- ✅ `summary.stop_reason` is null on a clean full run, or a string explaining early termination
- ✅ The fixture folder cleanup ran (Phase 6 final steps), or the report records why it couldn't
