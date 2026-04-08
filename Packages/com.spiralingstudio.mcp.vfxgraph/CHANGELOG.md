# Changelog

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
