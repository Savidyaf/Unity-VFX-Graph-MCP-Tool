---
name: vfx-mcp-e2e-test
description: Use when running an end-to-end production-readiness sweep of the
  com.spiralingstudio.mcp.vfxgraph Unity MCP package. Exercises every action
  in all 9 vfx_* tools against a live Unity instance and produces a
  structured pass/fail/anomaly report. Trigger on prompts about E2E
  testing, regression testing, or release-gating the VFX MCP addon.
---

# VFX MCP E2E Production-Readiness Test

You are running a structured E2E sweep of the
`com.spiralingstudio.mcp.vfxgraph` package against a live Unity instance.
Your job is to exercise every action, record each call in the report
schema, and stop. **You do NOT fix bugs. You do NOT modify code. You do
NOT change tests. You report.**

## Skill files

- `references/action-catalog.md` — every action × params × expected response keys
- `references/error-envelope-spec.md` — canonical error / not_implemented envelope shape
- `references/report-schema.md` — the JSON report shape you must produce
- `references/known-deferred.md` — intentional `state="not_implemented"` items + how to flag them

Read all four reference files before starting Phase 1.

## Hard rules

1. **Status semantics:**
   - `PASS` = call succeeded AND every expected response key from the action catalog is present
   - `FAIL` = call returned an `error` envelope when success was expected, OR threw, OR a precondition was unmet
   - `ANOMALY` = call appears successful but is missing expected keys, has unexpected extras, returned a `state="not_implemented"` envelope where implementation was expected, or returned an envelope shape that disagrees with `references/error-envelope-spec.md`
   - `SKIPPED` = a precondition step failed, so this row was not run
2. **Always pass `verbose=true`** in every action call. We need the `health` block surfaced for audit.
3. **Always pass `unity_instance="ca9e7d133faa7c96"`** to every `mcp__UnityMCP__execute_custom_tool` call.
4. **Never call** `mcp__UnityMCP__refresh_unity`, `mcp__UnityMCP__manage_editor` for play/stop, `mcp__UnityMCP__manage_script*`, or any `git` command.
5. **Never short-circuit** on the first FAIL. Run all 6 phases. The triage step needs the full picture.
6. **Token table:** maintain a scratch dict of `$placeholder → "n_xxxx"` across phases. The skill specifies `$placeholders` (e.g. `$spawn_ctx`); you substitute the live token from the prior call's response.
7. **Param naming gotcha:** asset-creation tools (`vfx_asset.create`, `vfx_subgraph.create`) use the param name **`path`**. All graph-mutation tools use **`graph`**. Don't mix them up.
8. **Cleanup is mandatory.** Phase 6 must delete `Assets/VfxE2EFixtures/` and its `.meta` file even if earlier phases failed.

## Pre-flight checks (before Phase 1)

Run these in order. Abort if any fails.

1. **Unity instance:**
   ```
   Read mcpforunity://instances. Confirm exactly one connected instance
   with hash starting "ca9e7d13". Record the full instance id.
   ```
2. **Set active instance:**
   ```
   mcp__UnityMCP__set_active_instance instance_id="VFX-MPC-wip@ca9e7d133faa7c96"
   ```
3. **Editor idle:**
   ```
   Read mcpforunity://editor_state. Verify isCompiling=false AND isPlaying=false.
   If isCompiling=true, wait 5s and re-read once. Abort if still compiling.
   ```
4. **Custom tools loaded:**
   ```
   Read mcpforunity://custom-tools. Verify all 9 of these are listed:
   vfx_asset, vfx_graph, vfx_node, vfx_block, vfx_property, vfx_subgraph,
   vfx_recipe, vfx_batch, vfx_diag.
   ```
5. **Initialize report.** Set up the empty report skeleton from `references/report-schema.md`.
6. **Fixture folder:** create `Assets/VfxE2EFixtures/` if missing via:
   ```
   mcp__UnityMCP__manage_asset action=create_folder path="Assets/VfxE2EFixtures"
   ```

---

## Phase 1 — Discovery (read-only)

Goal: verify catalogs, the diagnostic surface, and the get_info read-side
all work on an empty fixture.

| Step | Tool | Action | Params | Expectation |
|---|---|---|---|---|
| 1.1 | vfx_diag | list_node_types | `page=0` | `node_types[]` non-empty, `total > 100`, has `page`/`has_next` |
| 1.2 | vfx_diag | list_block_types | `page=0` | `block_types[]` non-empty, `total > 50` |
| 1.3 | vfx_diag | list_contexts | `page=0` | `contexts[]` non-empty (Spawn, Init, Update, Output etc.) |
| 1.4 | vfx_diag | list_subgraphs | `page=0` | `subgraphs[]` shape with `fqn`+`kind`, may be empty array |
| 1.5 | vfx_diag | list_attributes | `page=0` | **EXPECT ANOMALY**: per `references/known-deferred.md`, this used to return `state="not_implemented"` and we want it lifted in this run. If it returns a real `attributes[]`, mark PASS. If `state="not_implemented"`, mark **ANOMALY**. |
| 1.6 | vfx_diag | list_settings | `page=0` | Same as 1.5 — **ANOMALY** if still `state="not_implemented"` |
| 1.7 | vfx_diag | get_warnings | (none) | `warnings[]` array, may be empty |
| 1.8 | vfx_diag | read_console | (none) | `lines[]` array |
| 1.9 | vfx_recipe | list | (none) | `recipes[]` array — **ANOMALY** if it has a `note` key saying "deferred to v0.3.1" (we're on v0.3.1; recipes were supposed to land) |
| 1.10 | vfx_diag | (unknown) | `action="bogus"` | **EXPECT FAIL**: error envelope with `code="unknown_action"`, non-empty `hint` |

After each call:
- Verify no `error` key in the response
- Verify every key in the "Expected" column actually appears in `observed_keys`
- Record raw_response_excerpt (first 400 chars)

---

## Phase 2 — Asset lifecycle

Goal: exercise asset create / list / assign / delete on independent
throwaway assets, and CREATE the main fixture asset for later phases.

| Step | Tool | Action | Params | Expectation | Side effect |
|---|---|---|---|---|---|
| 2.1 | vfx_asset | list | (none) | `assets[]` includes the existing `Assets/VFX/TestEffect.vfx` if present | — |
| 2.2 | vfx_asset | create | `path="Assets/VfxE2EFixtures/E2EThrowaway.vfx"` | `created.path`, `created.kind=="vfx"` | asset on disk |
| 2.3 | vfx_asset | list | (none) | `assets[]` now includes `E2EThrowaway.vfx` | — |
| 2.4 | vfx_asset | delete | `path="Assets/VfxE2EFixtures/E2EThrowaway.vfx"` | success shape (no error) | asset removed |
| 2.5 | vfx_asset | create | `path="Assets/VfxE2EFixtures/E2EThruster.vfx"` | `created.path`, `created.kind=="vfx"` | **MAIN FIXTURE** created. Store path as `$FIXTURE`. |
| 2.6 | vfx_asset | create | `path="Assets/VfxE2EFixtures/E2EThrusterMath.vfxoperator"` | `created.path`, `created.kind=="vfxoperator"` | Subgraph fixture. Store as `$SUBGRAPH`. |
| 2.7 | vfx_asset | create | `path="Assets/VfxE2EFixtures/bad.txt"` | **EXPECT FAIL**: `error.code` matches `validation_error` | — |
| 2.8 | vfx_asset | delete | `path="Assets/VfxE2EFixtures/does_not_exist.vfx"` | **EXPECT FAIL**: `error.code` matches `asset_not_found` | — |

`vfx_asset.assign` is exercised in Phase 6 against a temp GameObject.

---

## Phase 3 — Graph build (against `$FIXTURE`)

Goal: build a working particle system on the main fixture using the full
node/block/connect surface, and capture every minted token.

For every step, the call shape is:
```
mcp__UnityMCP__execute_custom_tool
  tool_name="<tool>" parameters={"action": "<action>", "graph": "$FIXTURE", "verbose": true, ...rest}
  unity_instance="ca9e7d133faa7c96"
```

| Step | Tool | Action | Params | Capture | Expectation |
|---|---|---|---|---|---|
| 3.1 | vfx_node | list | `page_size=200` | — | empty `nodes[]`, `total=0`, `graph_path` echoes |
| 3.2 | vfx_node | add | `type="UnityEditor.VFX.VFXBasicSpawner" x=0 y=0` | `$spawn_ctx` ← `added[0].token` | `added[0].token` matches `n_[0-9a-f]{5}`, `added[0].type` echoes |
| 3.3 | vfx_node | add | `type="UnityEditor.VFX.VFXBasicInitialize" x=300 y=0` | `$init_ctx` | same |
| 3.4 | vfx_node | add | `type="UnityEditor.VFX.VFXBasicUpdate" x=600 y=0` | `$update_ctx` | same |
| 3.5 | vfx_node | add | `type="UnityEditor.VFX.VFXPlanarPrimitiveOutput" x=900 y=0` | `$output_ctx` | same (planar primitive output is the URP-friendly default) |
| 3.6 | vfx_node | add | `type="UnityEditor.VFX.Operator.Position" x=-300 y=200` | `$position_op` | operator (not context) — verify `category` later via list |
| 3.7 | vfx_node | list | `page_size=200` | — | `total=5`, every captured token is in `nodes[]`, `category` set per type |
| 3.8 | vfx_property | add | `type="System.Single" x=-600 y=400` | `$float_param` | parameter token |
| 3.9 | vfx_block | add | `parent_token="$init_ctx" type="UnityEditor.VFX.Block.SetAttribute" index=0` | `$setattr_block` | `added[0].token`, `parent_token` echoes |
| 3.10 | vfx_block | add | `parent_token="$update_ctx" type="UnityEditor.VFX.Block.LinearDrag" index=0` | `$drag_block` | same |
| 3.11 | vfx_node | connect | `from_token="$spawn_ctx" from_slot="o" to_token="$init_ctx" to_slot="i"` | — | `connected.from_token`/`to_token` echo (flow link) |
| 3.12 | vfx_node | connect | `from_token="$init_ctx" from_slot="o" to_token="$update_ctx" to_slot="i"` | — | same |
| 3.13 | vfx_node | connect | `from_token="$update_ctx" from_slot="o" to_token="$output_ctx" to_slot="i"` | — | same |
| 3.14 | vfx_node | list | `page_size=200` | — | `total=7` (5 nodes + 2 blocks), blocks have `parent_token` and `block_index >= 0` |
| 3.15 | vfx_node | add | `type="bogus.does.not.exist" x=0 y=0` | — | **EXPECT FAIL**: `error.code` ∈ {`type_not_found`, `unknown_type`, `validation_error`}, non-empty `hint` |
| 3.16 | vfx_node | connect | `from_token="n_99999" from_slot="o" to_token="$init_ctx" to_slot="i"` | — | **EXPECT FAIL**: `error.code` ∈ {`token_not_found`, `node_lost`, `unknown_token`} |
| 3.17 | vfx_node | remove | `token="$position_op"` | clear `$position_op` | `removed[0].token` matches |
| 3.18 | vfx_node | duplicate | `token="$drag_block"` | `$drag_block_copy` ← `duplicated.token` | `duplicated.source==$drag_block`, new token differs |

---

## Phase 4 — Mutations (against `$FIXTURE`)

Goal: exercise every mutator + every coercion path.

| Step | Tool | Action | Params | Expectation |
|---|---|---|---|---|
| 4.1 | vfx_node | move | `token="$spawn_ctx" x=10 y=20` | `moved.token=$spawn_ctx`, `moved.x==10`, `moved.y==20` |
| 4.2 | vfx_node | list | `page_size=200` | `$spawn_ctx` row has `x==10`, `y==20` (verifies move actually persisted, not silent-success) |
| 4.3 | vfx_node | get_setting | `token="$output_ctx" name="blendMode"` | `value` non-null (string or enum int) |
| 4.4 | vfx_node | set_setting | `token="$output_ctx" name="blendMode" value="Additive"` | `set_setting.token`, `set_setting.name=="blendMode"`, `set_setting.value=="Additive"` |
| 4.5 | vfx_node | get_setting | `token="$output_ctx" name="blendMode"` | `value=="Additive"` (round-trip — verifies no silent drop) |
| 4.6 | vfx_property | set_value | `token="$float_param" value=2.5` | `set_value.token`, `set_value.value=="2.5"` |
| 4.7 | vfx_property | set_exposed | `token="$float_param" exposed=true` | `set_exposed.exposed==true` |
| 4.8 | vfx_node | get_property | `token="$float_param" name="value"` | `value=="2.5"` (round-trip) |
| 4.9 | vfx_block | reorder | `token="$drag_block" new_index=1` | `reordered.token`, `reordered.new_index==1` |
| 4.10 | vfx_block | set_activation | `token="$drag_block" active=false` | `set_activation.active==false` |
| 4.11 | vfx_block | set_activation | `token="$drag_block" active=true` | `set_activation.active==true` (round-trip) |
| 4.12 | vfx_block | set_attribute | `token="$setattr_block" attribute_name="A" value="velocity"` | `set_attribute.attribute_name=="A"`, `set_attribute.value=="velocity"` |
| 4.13 | vfx_node | set_property | `token="$position_op_2"...` | SKIP if 3.17 removed `$position_op` (it did). Skip this row, mark SKIPPED with reason. |
| 4.14 | vfx_graph | get_info | `graph="$FIXTURE"` | `graph_path`, `child_count > 0`, `system_count`, `system_names[]`, plus `space`, `bounds_setting_mode`, `update_mode`. **EXPECT ANOMALY** if `space`/`bounds_setting_mode`/`update_mode` are empty strings (per `known-deferred.md`, the F3 partial keys are scheduled to be lifted in this run) |
| 4.15 | vfx_graph | set_space | `space="World"` | **EXPECT ANOMALY** per known-deferred (used to be `state="not_implemented"`; should be implemented now or escalated) |
| 4.16 | vfx_graph | set_capacity | `capacity=8192` | same |
| 4.17 | vfx_graph | set_bounds | `center=[0,0,0] size=[10,10,10]` | same |
| 4.18 | vfx_graph | set_data_settings | `name="boundsSettingMode" value="Manual"` | success or `state="not_implemented"` (depends on whether boundsSettingMode is graph-level — flag ANOMALY only if it's clearly a stub) |
| 4.19 | vfx_node | set_setting | `token="n_99999" name="x" value=1` | **EXPECT FAIL**: `error.code` ∈ {`token_not_found`, `node_lost`} |
| 4.20 | vfx_node | set_property | `token="$float_param" name="totally_fake_slot" value=1` | **EXPECT FAIL**: well-formed error envelope |

---

## Phase 5 — Batch + recipes + subgraph (against `$FIXTURE`)

| Step | Tool | Action | Params | Expectation |
|---|---|---|---|---|
| 5.1 | vfx_subgraph | create | `path="$SUBGRAPH"` | already created in 2.6 — call again with new throwaway path `Assets/VfxE2EFixtures/E2ESpare.vfxoperator` and capture as `$SPARE_SUB`. Expect `created.path`, `created.kind=="vfxoperator"` |
| 5.2 | vfx_subgraph | add_ref | `subgraph="$SUBGRAPH" x=1200 y=0` | `added.token`, `added.subgraph==$SUBGRAPH`. Capture token as `$subgraph_ref`. **NOTE:** v0.3.1 returns `added` as a single object here (not array as in `vfx_node.add`). Flag ANOMALY for this inconsistency. |
| 5.3 | vfx_subgraph | get_exposed | `graph="$FIXTURE" token="$subgraph_ref"` | `exposed[]` array. **EXPECT ANOMALY** if response has a `note` key like "Phase 5: enumerate..." (means stub, not real impl) |
| 5.4 | vfx_subgraph | inline | `graph="$FIXTURE" token="$subgraph_ref"` | **EXPECT FAIL or stub**: per known-deferred, this was deferred. Mark PASS if it returns a real success, ANOMALY if it returns `error.code="not_implemented"` (since user wants this lifted) |
| 5.5 | vfx_subgraph | extract | (similar) | same |
| 5.6 | vfx_batch | commit | `graph="$FIXTURE" ops=[...]` (3 ops below) | success shape; the multi-tool batch should commit all 3 in a single transaction |
| | | | op[0]: `{tool:"vfx_node", action:"add", graph:"$FIXTURE", type:"UnityEditor.VFX.Operator.Add", x:-200, y:600}` | |
| | | | op[1]: `{tool:"vfx_property", action:"add", graph:"$FIXTURE", type:"UnityEngine.Vector3", x:-500, y:600}` | |
| | | | op[2]: `{tool:"vfx_block", action:"add", graph:"$FIXTURE", parent_token:"$update_ctx", type:"UnityEditor.VFX.Block.LinearDrag", index:-1}` | |
| 5.7 | vfx_batch | commit | `graph="$FIXTURE" ops=[ {tool:"vfx_asset", action:"delete", path:"$SPARE_SUB"} ]` | **EXPECT FAIL**: `error.code="not_implemented"` (delete not batchable per design) |
| 5.8 | vfx_batch | commit | `graph="$FIXTURE" ops=[]` | **EXPECT FAIL**: `error.code="missing_required_param"` |
| 5.9 | vfx_recipe | list | (none) | (already in 1.9, skip) |

---

## Phase 6 — Save / compile / health / cleanup

| Step | Tool | Action | Params | Expectation |
|---|---|---|---|---|
| 6.1 | vfx_graph | save | `graph="$FIXTURE"` | `saved==$FIXTURE` |
| 6.2 | vfx_graph | compile | `graph="$FIXTURE"` | `Ok` (or equivalent), `Errors[]` array (may be empty), `DurationMs` numeric |
| 6.3 | vfx_graph | compilation_status | `graph="$FIXTURE"` | **EXPECT ANOMALY** if returns `state="not_implemented"` (per known-deferred, F4 to be lifted) |
| 6.4 | vfx_graph | get_health | `graph="$FIXTURE"` | same — ANOMALY if `state="not_implemented"` |
| 6.5 | vfx_graph | read_console | (none) | `lines[]` array |
| 6.6 | vfx_graph | discard_changes | `graph="$FIXTURE"` | `discarded==$FIXTURE` |
| 6.7 | vfx_asset | create | `path="Assets/VfxE2EFixtures/E2ETempGO.prefab"` | **SKIP** — wrong tool path. Replace with: use `mcp__UnityMCP__manage_gameobject action=create name="E2ETempGO"` |
| 6.8 | mcp__UnityMCP__manage_gameobject | create | `name="E2ETempGO"` | gameObject created |
| 6.9 | vfx_asset | assign | `path="$FIXTURE" gameObject="E2ETempGO"` | `assigned==$FIXTURE`. **NOTE:** assign bypasses ShapeMutation on success — only `assigned` key, no `health` even with `verbose=true`. Flag ANOMALY for the inconsistency. |
| 6.10 | mcp__UnityMCP__manage_gameobject | delete | `name="E2ETempGO"` | gameObject removed |
| 6.11 | vfx_asset | delete | `path="$FIXTURE"` | success |
| 6.12 | vfx_asset | delete | `path="$SUBGRAPH"` | success |
| 6.13 | vfx_asset | delete | `path="$SPARE_SUB"` | success (if it survived 5.7) |
| 6.14 | mcp__UnityMCP__manage_asset | delete | `path="Assets/VfxE2EFixtures"` | folder removed (cleans up .meta too) |
| 6.15 | vfx_asset | list | (none) | `assets[]` no longer contains anything under `Assets/VfxE2EFixtures/` |

---

## Reporting

After Phase 6 completes (or fails out), produce ONE final message containing:

1. **The full report JSON** per `references/report-schema.md`. Wrap in a fenced code block tagged `json`.
2. **A 5-line summary**: total / pass / fail / anomaly / skipped
3. **The list of unexpected error codes** seen across all phases (for triage)
4. **The list of known-deferred items encountered** (with their step numbers)
5. **Any preconditions that failed and forced SKIPPED rows**

Do NOT prose-narrate. The triage step needs structured data.

## Stop conditions

Stop and produce a partial report immediately if:
- Unity instance disconnects (any tool call returns a transport error twice in a row)
- Three consecutive calls return `code="vfx_busy"` even after the documented retry
- A pre-flight check fails (record what failed and why)
- The fixture asset disappears mid-run (something out-of-band deleted it)

In all stop cases, **still attempt Phase 6 cleanup** before reporting.
