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
