# Changelog

## [0.3.1] - 2026-04-08

v0.3.1 read-side surface + Phase 4a correctness backlog

### Added

- **`vfx_node.list`** — paged enumeration of every node in a graph with
  stable tokens (read-side surface; closes the dominant downstream-agent
  gap that motivated v0.3.1). Walks `VFXGraph.children` + recurses one
  level into `VFXContext.children` for blocks. Mints/recovers tokens via
  `VfxIdentity.Mint`. Returns `{graph_path, total, offset, page_size,
  nodes[]}` with each node carrying `token`, `type`, `x`, `y`,
  `parent_token`, `category`, `block_index`.
- **`vfx_graph.get_info`** now exposes `space`, `system_count`,
  `system_names[]`, `bounds_setting_mode`, `update_mode` (Phase 4a F3 —
  partial). `system_count` and `system_names` are real, walked via
  `VFXSystemNames.GetSystemName(ctx)` across top-level contexts. The
  other 3 keys are honest empty-string stubs because they live on
  `VFXContext` / `VFXDataParticle`, not on `VFXGraph` in Unity 6000.4.
- **`IVfxResponseShaper.ShapeMutation`** — single chokepoint for shaping
  mutating-action responses (Phase 4a F5). Replaces the post-hoc JObject
  mutation pattern across all 18 mutating actions in `VfxNodeTool` /
  `VfxBlockTool` / `VfxPropertyTool` / `VfxSubgraphTool`.
- **`IVfxNodeOps.MoveNode`** — first-class node-position mutation
  (Phase 4a F15). Replaces the previous `set_setting`-based "position"
  hack. `VfxNodeTool.move` records intent op `Kind="move"` and the YAML
  verifier accepts the new kind via an explicit `case "move":` arm.
- **`VfxCoercerDispatch`** — typed coercion helper that dispatches
  through `VfxCoercers.g.cs` for `float`/`int`/`uint`/`bool`/`string`/
  `Vector2`/`Vector3`/`Vector4`/`Color` plus inline enum
  name/integer parse (Phase 4a F7). Closes the silent-drop gaps that
  `Convert.ChangeType` had for non-trivial setting types.
- **Multi-tool batch dispatch** for the 6 batchable tools — `vfx_node`,
  `vfx_block`, `vfx_property`, `vfx_subgraph`, `vfx_asset.assign`,
  `vfx_graph.{save, set_data_settings}` (Phase 4a F9). Each mutating
  action now follows the outer/`*Inner` pattern: outer opens BusyGate +
  Transaction.Begin + ShapeMutation; inner takes a live
  `VfxTransactionScope` and does only the NodeOps call + `scope.Record`.
  `VfxBatchTool` dispatches via reflection on `ApplyInTransaction` and
  unwraps `TargetInvocationException`-wrapped `NotImplementedException`
  into `code="not_implemented"` envelopes.
- **New tests:** `VfxNodeListTests` (2 tests — list + GetInfo expansion),
  `VfxResponseShaperTests` (3 new ShapeMutation cases),
  `VfxNodeOpsCoercerTests` (1 round-trip test), `VfxBatchTests` (1
  multi-tool batch test), `VfxNodeMoveTests` (1 move + list round-trip).
  Total: 107 (v0.3.0 baseline) → 115 EditMode tests pass, 0 fail.
- **`VfxKernelContracts.cs`** contracts bump (Lane 0): adds
  `IVfxNodeOps.ListNodes`, `IVfxNodeOps.MoveNode`,
  `IVfxResponseShaper.ShapeMutation`, plus the `VfxNodeListEntry` record
  class. Single locked-file commit.

### Changed

- **`vfx_graph.compilation_status`** and **`vfx_graph.get_health`** now
  return `state="not_implemented"` envelopes with hints (Phase 4a F4),
  not fake `"unknown"` data. Clients can distinguish "not implemented"
  from a stale compile result.
- **`vfx_graph.set_capacity`**, **`set_bounds`**, and **`set_space`**
  return honest `state="not_implemented"` envelopes with hints pointing
  at `vfx_node.set_setting` on the relevant context (capacity lives on
  `VFXBasicInitialize`, bounds are per-system on `VFXDataParticle`,
  space lives on `VFXContext`). Replaces the old `"Lane 4B pending"`
  stub. Runtime probe at `1f4cdcc` confirmed `space` doesn't exist on
  `VFXGraph`.
- **`vfx_graph.set_data_settings`** uses conditional dispatch through
  the new `@graph` synthetic-token branch in `VfxNodeOps.SetSetting`
  (Phase 4a F-A5). If the named setting is a `VFXGraph`-level field it
  routes through the @graph branch; otherwise the kernel throws
  `setting_not_found` and the tool surfaces a `not_implemented`
  envelope with a hint.
- **`VfxNodeOps.SetSetting`** non-`SerializableType` branch (and the
  new `@graph` branch) dispatches via `VfxCoercerDispatch` instead of
  `Convert.ChangeType` (Phase 4a F7).
- **`VfxBatchTool.HandleCommand`** outer catch chain gains a
  `TargetInvocationException` arm that unwraps `NotImplementedException`
  into `code="not_implemented"` so callers can distinguish "this action
  doesn't batch yet" from a real bug.

### Deferred

- **`vfx_diag.list_attributes`** and **`list_settings`** ship as honest
  `state="not_implemented"` envelopes (Phase 4a F10, Path A4-stub per
  spec §4.1). The walker/emitter changes for the full path require
  either a soft-fork bridge addition (`VFXAttributesManager.GetBuiltInNames`)
  or per-type `[VFXSetting]` reflection — both deferred to v0.3.2.
- **`vfx_asset.delete`** is NOT batchable (initially wired in `8d79b15`,
  removed in `55adf14`). After `AssetDatabase.DeleteAsset`, the batch's
  end-of-batch verifier would run against a missing asset. Asset
  deletion belongs in its own single-call path.
- **F3 partial keys** (`space`, `bounds_setting_mode`, `update_mode` on
  `vfx_graph.get_info`) — empty-string stubs in v0.3.1; per-context
  dispatch deferred to v0.3.2.

### References

- **Design**: [`docs/superpowers/specs/2026-04-08-vfx-mcp-v0.3.1-read-side-and-correctness-design.md`](../../../docs/superpowers/specs/2026-04-08-vfx-mcp-v0.3.1-read-side-and-correctness-design.md)
- **Plan**: [`docs/superpowers/plans/2026-04-08-vfx-mcp-v0.3.1-read-side-and-correctness-plan.md`](../../../docs/superpowers/plans/2026-04-08-vfx-mcp-v0.3.1-read-side-and-correctness-plan.md)
- **Phase 4a review** (with v0.3.1 disposition table): [`docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md`](../../../docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md) §2.1

## [0.3.0] - 2026-04-08

### Added — complete rebuild

Production-ready VFX Graph MCP tool suite replacing `manage_vfx` +
`manage_vfx_graph` + `inspect_vfx_asset` with **9 grouped tools**:

- `vfx_asset` — create, list, delete, assign
- `vfx_graph` — get_info, save, compile, compilation_status, read_console,
  set_space/capacity/bounds/data_settings, discard_changes, get_health
- `vfx_node` — add, remove, move, duplicate, connect, disconnect,
  get_setting, set_setting, get_property, set_property
- `vfx_block` — add, remove, reorder, set_activation, set_attribute,
  list_attributes
- `vfx_property` — add, remove, set_value, set_exposed
- `vfx_subgraph` — create, add_ref, get_exposed, set_override
  (`inline` / `extract` deferred to v0.3.1 per erratum A-H2)
- `vfx_recipe` — list (scaffold; recipes deferred to v0.3.1)
- `vfx_batch` — commit (single end-of-batch three-part health gate)
- `vfx_diag` — list_node_types/block_types/contexts/subgraphs/attributes/
  settings, read_console, get_warnings

**Generator-driven typed catalog** walking `VFXLibrary.Get*()` descriptors.
**Zero runtime reflection** on `UnityEditor.VFX.*` / `UnityEngine.VFX.*` types
(reflection on the addon's own generated tool classes for batch dispatch is
documented and tested by `VfxNoReflectionTests`).

**Sidecar + structural-fingerprint identity** (no asset mutation). Token
format: `n_xxxxx` (5-hex FNV-1a 64), persisted in
`Library/VfxMcpIdentity.json`.

**Three-part health gate** at every `VfxTransaction.Commit()`:
1. YAML structural diff via partial UnityYAML reader + MonoScript GUID
   resolver
2. Compile gate via `VFXGraph.CompileAndUpdateAsset`
3. Console correlation via `Application.logMessageReceived` ring buffer

**Token-savings response shaping**: terse mode (default `verbose=false`)
omits the `health` key per release-gate criterion #5.

**Override layer**: `Quirks.yaml` + `Hints.yaml` baked into
`VfxOverrides.g.cs` for collision resolution, alias resolution, and per-
error-code hint templates.

**Performance budgets** for the 500-node synthetic graph
(`VfxPerformanceTests` + `FiveHundredNodeGraph` fixture).

### Changed — soft-fork patches

Soft-forked `com.unity.visualeffectgraph` 17.4.0 with TWO patch files:

- `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` —
  `[InternalsVisibleTo(...)]` for the addon Editor + Tests assemblies
- `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs` —
  compile-time bridge for `VisualEffectResource` (which lives in Unity
  engine's `UnityEditor.VFXModule` and cannot receive an
  `InternalsVisibleTo` grant from a user package)

### Removed

- Legacy `manage_vfx` mega-tool and the bundled ParticleSystem /
  LineRenderer / TrailRenderer code (covered by MCPForUnity's
  `manage_components`)
- `manage_vfx_graph`, `inspect_vfx_asset`
- 5 legacy test files (`VfxActionsTests`, `VfxGraphReflectionCacheTests`,
  `VfxGraphResultMapperTests`, `VfxInputValidationTests`,
  `VfxToolContractTests`) — all targeted the deleted architecture

### References

- **Design spec**: [`docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`](../../../docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md)
- **Implementation plan**: [`docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md`](../../../docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md)
- **Spec reviews**: [`docs/spec-review-vfx-graph-mcp-redesign.md`](../../../docs/spec-review-vfx-graph-mcp-redesign.md), [`docs/objective-review-vfx-graph-mcp-redesign.md`](../../../docs/objective-review-vfx-graph-mcp-redesign.md)
- **Plan reviews**: [`docs/plan-review-vfx-graph-mcp-rebuild.md`](../../../docs/plan-review-vfx-graph-mcp-rebuild.md), [`docs/agent-review-vfx-graph-mcp-rebuild-plan.md`](../../../docs/agent-review-vfx-graph-mcp-rebuild-plan.md)
- **Phase 4a code review**: [`docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md`](../../../docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md)
- **Phase 6 release gate**: [`docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md`](../../../docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md)
- **Gap doc (forward to v0.3.1)**: [`docs/vfx_graph_mcp_tool_gaps.md`](../../../docs/vfx_graph_mcp_tool_gaps.md)

## [Unreleased]

### E2E hardening pass — 2026-04-09

A full end-to-end production-readiness sweep against a live Unity 6000.4.0f1
instance, driven by a new project-local skill (`.claude/skills/vfx-mcp-e2e-test/`).
Two test-agent runs surfaced 14 distinct bug clusters across the v0.3.1 surface;
all 14 are fixed in this pass. EditMode test count grew from **115 (v0.3.1
baseline) → 154 passing**, zero regressions.

**Design / plan / skill**:
- Spec: [`docs/superpowers/specs/2026-04-09-vfx-mcp-v0.3.1-e2e-production-ready-design.md`](../../../docs/superpowers/specs/2026-04-09-vfx-mcp-v0.3.1-e2e-production-ready-design.md)
- Skill (project-local, regression-test asset): [`.claude/skills/vfx-mcp-e2e-test/SKILL.md`](../../../.claude/skills/vfx-mcp-e2e-test/SKILL.md)

### Fixed (E2E hardening pass)

- **C1 — `vfx_block.reorder` is now atomic on failure.** The previous
  implementation called `parent.RemoveChild` then `parent.AddChild(newIndex)`
  non-atomically; if the new index was out of bounds, the block was deleted
  from the graph entirely with no recovery path. The fix captures the original
  index via `parent.GetIndex(block)` BEFORE removing, then wraps the
  `RemoveChild`+`AddChild` pair in a try/catch that re-attaches at the original
  index on failure and re-throws. New test:
  `VfxBlockReorderAtomicityTests.Reorder_OutOfBoundsIndex_IsAtomic_BlockRemainsInGraph`.
- **W2-A — `vfx_node.connect` now supports context-to-context flow links.**
  Previously every `connect` call took the data-slot path, so flow links
  between Spawn → Init → Update → Output returned `slot_not_found` for any
  slot name. The fix adds a guarded branch in `VfxNodeOps.Connect`/`Disconnect`
  that detects when both endpoints are `VFXContext` and routes through the
  public `VFXContext.LinkTo`/`UnlinkTo` API. A `ParseFlowSlotIndex` helper
  accepts the common labels (`null`, `""`, `"flow"`, `"o"`, `"i"`, `"out"`,
  `"in"`) as flow index 0 and parses non-negative integer strings for
  multi-flow contexts. New test:
  `VfxNodeOpsContextFlowTests.Connect_SpawnToInitToUpdate_LinksContextsAndDisconnectClears`.
- **W2-B — `vfx_block.set_attribute` now actually persists the attribute.**
  The previous implementation passed `attribute_name` to `NodeOps.SetProperty`
  as a slot name, which always failed because no such slot exists. The fix
  records two scope ops per call: a `SetSetting("attribute", attributeName)`
  first (which lets the block re-sync its slots), then
  `SetProperty(localSlotName, value)` against the canonical local slot name
  pattern `"_" + Capitalize(attributeName)` (matches
  `VFXBlockSetAttribute.GenerateLocalAttributeName`). Also fixes
  `vfx_block.list_attributes` to return a canonical
  `state="not_implemented"` envelope instead of an ad-hoc `{"note": ...}`
  shape. New test:
  `VfxBlockSetAttributeTests.SetAttribute_LifetimeOnSetAttributeBlock_PersistsSettingAndReturnsSuccess`.
- **W2-C — `vfx_node.duplicate` now works for blocks.** The previous
  dispatch fell through to `AddOperator` for `VFXBlock` instances, which
  failed because blocks need a parent context. The fix adds a
  `VFXBlock` branch that recovers the parent's token via `Identity.Mint`,
  computes `parent.GetIndex(block) + 1` to place the duplicate adjacent to
  the source, and routes through `IVfxNodeOps.AddBlock`. New test:
  `VfxNodeDuplicateBlockTests.Duplicate_OnBlockToken_CreatesSiblingUnderSameContext`.
- **W2-D — Three envelope-shape inconsistencies fixed.** (1)
  `VfxAssetTool.Assign` success path now goes through `Shaper.ShapeMutation`
  instead of bypassing it, so verbose responses include `health` and
  `warnings` like every other mutating action. (2)
  `VfxSubgraphTool.AddRefInner` now wraps its `added` entry in a `JArray`
  to match every other `Add*Inner` (was returning a single `JObject` —
  type drift). (3) `VfxSubgraphTool.GetExposed` now returns a canonical
  `state="not_implemented"` envelope instead of `{"exposed": [], "note": ...}`.
  New tests: `VfxAssetAssignShapeTests`, `VfxSubgraphAddRefShapeTests`.
- **W3-A — `vfx_property.set_value`/`vfx_node.get_property` now round-trip on
  `VFXParameter`.** The previous code looked for a slot named `"value"`, but
  `VFXParameter`'s output slot is named `"o"` (`VFXParameter.Init` calls
  `VFXSlot.Create(new VFXProperty(_type, "o"))`). The fix adds a `VFXParameter`
  special-case branch in `VfxNodeOps.SetProperty`/`GetProperty` that uses the
  public `parameter.value` property (which maps to `outputSlots[0].value` for
  input parameters), with `VfxCoercerDispatch.Coerce(value, parameter.type)`
  on the set path. New test:
  `VfxPropertySetValueTests.SetValue_SingleParameter_RoundTripsThroughGetProperty`.
- **W3-B — `vfx_asset.delete` no longer returns spurious `compile_error`.**
  The previous implementation opened a `VfxTransaction`, deleted the asset,
  then ran the three-part health gate at `Commit()` — but the gate's compile
  step tried to re-load the just-deleted asset and threw. Asset deletion is
  not a graph mutation, so the fix bypasses the transaction entirely
  (matching the existing `Create` pattern), keeps `BusyGate.EnsureIdle()`,
  calls `AssetDatabase.DeleteAsset` directly, and returns
  `{"deleted": path}` without `Shape` wrapping. The unused `DeleteInner` and
  the `delete` arm in `ApplyInTransaction` were removed (delete remains
  intentionally non-batchable per design §4.5). New test:
  `VfxAssetDeleteTransactionTests.Delete_BypassesTransaction_ReturnsPlainDeletedPayloadNoHealth`.
- **W3-C — F10 catalog `vfx_diag.list_attributes` and `list_settings` lifted.**
  Both used to return `state="not_implemented"`. `VfxLibraryWalker` now
  emits a `Attributes[]` array (47 built-in attribute names) and a
  `Settings` dictionary mapping each `VFXModel` subtype to its
  `[VFXSetting]`-attributed field names. The walker uses build-time field
  reflection on the catalog types — does NOT violate the no-runtime-reflection
  rule, which targets runtime kernel dispatch only. `VfxDiagTool.ListSettings`
  uses a new `PageSettings` helper that paginates the dict as
  `{type_fqn, settings[]}` entries. `Editor/Generated/VfxCatalog.g.cs`
  regenerated. New tests: `VfxDiagListAttributesTests`,
  `VfxDiagListSettingsTests`.
- **W3-D — `vfx_recipe.list` returns a real recipe catalog.** Previously
  returned `{"recipes": [], "note": "Recipes deferred to v0.3.1."}` — stale
  even though we ARE on v0.3.1. Now returns 4 recipe entries with `name` +
  `description`: `ecs_buffer_particles`, `simple_spawn_particles`,
  `gpu_event_chain`, `particle_strip_trail`. Recipe EXECUTION (the
  `create`/`instantiate` action) stays explicitly deferred to v0.3.2 via
  the existing `unknown_action` fallback. New test:
  `VfxRecipeListTests.List_ReturnsCanonicalCatalog_WithoutStaleNote`.
  (Also: `VfxSmokeTests.cs` updated to drop a stale assertion on the now-removed
  `note` key.)
- **W4-A — `vfx_node.add` now returns `type_not_found` for unknown FQNs.**
  Previously, invalid type FQNs (e.g. `bogus.does.not.exist`,
  `UnityEditor.VFX.Operator.Position` which doesn't exist) bubbled up as
  `error.code="vfx_exception"` from the catch-all arm — callers couldn't
  distinguish "type doesn't exist" from "kernel crashed". The fix adds a
  `ThrowTypeNotFound(typeFqn, category)` helper in `VfxNodeOps` plus narrow
  catalog prechecks and `ArgumentException` catches around each
  `VfxNodeWrappers.Create*` call in `AddOperator`/`AddContext`/`AddBlock`/
  `AddParameter`. `AddSubgraphRef` gets a symmetric `FileNotFoundException`
  translation for missing subgraph assets. Non-type-resolution exceptions
  continue to propagate as `vfx_exception`. New tests:
  `VfxNodeOpsTypeNotFoundTests.Add_BogusFqn_ReturnsTypeNotFoundError`,
  `Add_InvalidOperatorFqn_ReturnsTypeNotFoundError`.
- **W4-B — Vector / Color coercer hardening.** The generated
  `CoerceToVector2/3/4` and `CoerceToColor` already handle `JObject {x,y,z}`
  and `JArray [x,y,z]` shapes. This pass adds Color hex-string support
  (`#RRGGBB` and `#RRGGBBAA`) via a pre-dispatch branch in
  `VfxCoercerDispatch.Coerce`, plus 12 regression tests covering every
  shape across Vector2/3/4 and Color. The generated coercer file stays
  untouched. New test class: `VfxCoercerDispatchVectorTests`.
- **W4-C — Four `vfx_graph.*` P2 lifts (was Phase 4a F-A5/F4/F3).**
  - `vfx_graph.set_space`, `set_capacity`, `set_bounds` are now real
    convenience wrappers that auto-find the target context/data and mutate
    it inside a single-call transaction. `set_capacity`/`set_bounds`
    target `VFXBasicInitialize.GetData() as VFXDataParticle` (capacity
    field is on `VFXDataParticle` decorated with `[VFXSetting, Delayed,
    SerializeField, FormerlySerializedAs("m_Capacity")]`; canonical bounds
    field name is `boundsMode`, formerly `boundsSettingMode`). When zero
    or multiple candidates exist, each wrapper returns a
    `state="needs_explicit_target"` envelope pointing the caller at
    `vfx_node.set_setting`. `set_bounds` only sets `boundsMode` (defaulting
    to `Manual`) — center/size are slot properties on the init context's
    `bounds` input slot and need a follow-up `vfx_node.set_property` call.
  - `vfx_graph.compilation_status` now returns the cached
    `VfxCompileResult` from the last `Compile()` call (per-asset cache via
    `s_lastCompileByPath`). When no compile has run yet, returns the
    documented `state="no_compile_yet"` state.
  - `vfx_graph.get_health` runs an empty no-op `VfxTransactionScope.SingleCall`
    and returns its `commit.Health` shaped via `Shape(commit, verbose: true)`.
    The three-part health gate fires against current on-disk state with no
    mutations.
  - `vfx_graph.get_info` F3 partial keys are now real: `space` walks top-level
    `VFXContext.space` values and returns the dominant one (empty when mixed),
    `bounds_setting_mode` walks `VFXDataParticle.boundsMode`, and
    `update_mode` returns the documented constant `"per_system"` (VFX Graph
    has no graph-level update mode in Unity 6000.4).
  - 9 new tests in `VfxGraphP2LiftsTests`.
- **W5-A — `VfxNodeOps.SetProperty` now coerces slot writes through
  `VfxCoercerDispatch.Coerce(value, slot.property.type)`.** Previously the
  raw `value` (e.g. JObject) was forwarded to `slot.value`, which threw
  `Cannot assign an object of type Newtonsoft.Json.Linq.JObject to
  VFXSerializedObject of type UnityEngine.Vector3`. The fix is a 12-line
  edit in the non-VFXParameter slot-write branch (W3-A's VFXParameter
  special case is preserved unchanged). Closes the legacy gap-doc bug #6
  for the Vector3-from-JSON-array case. New tests:
  `VfxNodeOpsSetPropertyCoercionTests.SetProperty_Vector3FromJObject_PersistsAsVector3`,
  `SetProperty_Vector3FromJArray_PersistsAsVector3`.
- **W5-B — `vfx_batch.commit` now folds per-op results into a combined
  response payload.** The previous implementation called `Shape(commit, verbose)`
  and discarded each `*Inner` method's returned JObject, so the response
  was missing the documented `added[]` key (per `references/action-catalog.md`)
  even though batched ops persisted correctly in the graph. Surfaced as
  the only ANOMALY in the final E2E run. The fix captures each op's
  return value, folds per-key arrays into a combined `JObject` via a new
  `FoldOpResultIntoPayload` helper, and dispatches through `ShapeMutation`.
  A 3-op add batch now returns `{added: [{token, type}, {token, type}, {token, type}], warnings: [...], health: {...}}`.
  Single-object op results (e.g. `connect`'s `connected: {...}`) are
  wrapped in a JArray so the response is always homogeneous. New test:
  `VfxBatchTests.Batch_ThreeAddOps_ReturnsCombinedAddedArrayInResponse`.

### Added (E2E hardening pass)

- **`VfxResponseShaper.ShapeError` now emits a top-level `success: false`
  discriminator** alongside the existing `error` key. This is a defensive
  workaround for an upstream `coplaydev/unity-mcp` Python normalizer bug
  (`models/unity_response.py:9-49`) that strips `error` from the inner
  payload when computing `data`, leaving every error envelope on the wire
  as `{success: true, data: null}`. The line-19 fast-path
  (`if "success" in response: return response`) lets us escape the strip
  by including a `success` key in the inner JObject. See the **Known
  upstream blockers** section below for the full root-cause analysis and
  the patches we shipped to the user's local `uvx` cache.

### Known upstream blockers (require user action)

The package surface is now production-ready end-to-end, but **two upstream
bugs in `coplaydev/unity-mcp`'s Python host process the wire envelopes
incorrectly** and silently corrupt error responses on the way out. Both
have been patched in the user's local `uvx` cache so the test agent can
see clean envelopes, but the patches will not survive a `uv` cache reset
and need upstream PRs.

1. **`unity_response.py:33-37`** — `normalize_unity_response` strips
   `{message, error, status, code}` from the inner payload to derive
   `data`. An error envelope whose only top-level key is `error` becomes
   an empty dict, then `data: None`. **Patch shipped to local cache**:
   added an early branch that detects `isinstance(payload.get("error"), dict)`
   and surfaces the structured envelope into the normalized response's
   `error` field directly.
2. **`models.py:9`** — `MCPResponse.error: str | None` rejects dict
   payloads via pydantic `ValidationError`. **Patch shipped to local
   cache**: relaxed to `error: Any | None` so structured envelopes pass
   validation.

The package's `VfxResponseShaper` workaround (top-level `success: false`)
keeps error envelopes visible **even if the upstream patches get wiped**
— but full clean wire-envelopes require both upstream patches to be
active, which requires the user to **restart the MCP host process** so
Python re-imports the patched modules. After the restart, all `vfx_*` tools
will return clean structured error envelopes on the wire as designed.

### Documentation

- **Token format — per-category prefix scheme documented as canonical.**
  The v0.3.0 CHANGELOG entry claimed `Token format: n_xxxxx (5-hex FNV-1a
  64)`, but the actual implementation in
  `Editor/Kernel/VfxIdentity.cs` (`PrefixFor`, lines 143–151) has always
  minted **per-category prefixes**:

  | Category | Prefix | Notes |
  |---|---|---|
  | `VFXContext` (Spawn/Init/Update/Output) | `ctx_` | |
  | `VFXBlock` (any block type) | `b_` | checked first; `VFXBlock` derives from `VFXSlotContainerModel`, not `VFXContext`/`VFXOperator` |
  | `VFXOperator` (Add, Position, …) | `n_` | |
  | `VFXParameter` (exposed props) | `n_` | |
  | fallback / `VFXSubgraphRef` | `n_` | |

  The 5-hex body (FNV-1a 64, lower 20 bits) is unchanged; only the prefix
  varies. There is a single mint call site
  (`VfxStructuralFingerprint.ToShortToken(prefix, fp)` in
  `VfxIdentity.Mint`), so this is the canonical scheme — not a second
  code path. Existing unit tests in
  `Tests/Editor/Kernel/VfxStructuralFingerprintTests.cs` already cover
  `n_` / `b_` / `ctx_`. The v0.3.0 `n_xxxxx` sentence was documentation
  drift, not a bug; no code changes in v0.3.1.
- Updated `.claude/skills/vfx-mcp-e2e-test/references/action-catalog.md`
  and `report-schema.md` to reflect the per-category scheme.

- Promoted `Packages/<package-name>/Editor/Tools/Vfx` as canonical source for VFX MCP tooling.
- Added `scripts/sync_vfx_tools.py` to keep `Assets/MCPForUnity/Editor/Tools/Vfx` as a compatibility mirror.
- Added CI workflow `.github/workflows/vfx-sync-check.yml` to detect package-assets drift.
- Centralized all assembly/type resolution through `VfxGraphReflectionCache` with safe `ReflectionTypeLoadException` handling.
- Cached `InvalidationCause` enum and `Invalidate` method lookups in persistence service.
- Fixed `GuessErrorCode` heuristic that misclassified "asset" messages as `asset_not_found`.
- Added `error_code` to all error returns in `VfxGraphEdit` for deterministic error classification.
- Added top-level exception handler in `ManageVfxGraph.HandleCommand`.
- Collapsed redundant `*Operations` and `*Service` wrapper layers; router now calls `VfxGraphEdit` directly.
- Extracted `TryLoadGraph`, `TryLoadNodeById`, `PersistGraph` helpers to reduce duplication.
- Replaced inline `SetDirty`/`SaveAssets` with centralized `PersistGraph`.
- Added `Debug.LogWarning` to correctness-affecting catch blocks.
- Extracted `FindVfxSettingField` and `ConvertSettingValue` from `SetNodeSetting`.
- Added Python setup step to CI sync-check workflow.
- Added orphan file detection and cleanup to sync script.
- Deduplicated CI test workflow (single filtered VFX test run).
- Converted `GraphActions` to `HashSet` for O(1) lookups; added `IsKnownAction`.
- Fixed null-caching in `GetEditorVfxType` (no longer caches failed lookups).
- Added unit tests for `VfxToolContract`, `VfxGraphResultMapper`, and expanded `VfxInputValidation`/`VfxActions` tests.
- Updated README architecture section to reflect simplified structure.

## [0.1.0] - 2026-02-17

- Added URP-first productionization baseline for VFX MCP tools.
- Added structured response contract with `tool_version` and `error_code`.
- Added `manage_vfx_graph` action routing module boundaries.
- Added validation helpers for required fields and asset paths.
- Added URP compatibility guard with explicit non-URP error messaging.
- Added docs and CI/test scaffolding for release hardening.
