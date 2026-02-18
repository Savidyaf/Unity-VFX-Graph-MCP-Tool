# Changelog

## [Unreleased]

- Promoted `Packages/com.pakaya.mcp.vfx/Editor/Tools/Vfx` as canonical source for VFX MCP tooling.
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
