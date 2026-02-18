# MCP VFX Implementation Progress

This document tracks implementation progress for the production-readiness plan of `com.pakaya.mcp.vfx`.

## Status Legend

- `pending`: not started
- `in_progress`: currently being implemented
- `completed`: implemented and validated
- `blocked`: waiting on decision or external dependency

## Phase Checklist

| Phase | Scope | Status |
|---|---|---|
| Phase 1 | Canonicalization and parity safety net | completed |
| Phase 2 | Critical workflow blockers | completed |
| Phase 3 | Modular refactor for maintainability | completed |
| Phase 4 | API consistency and ergonomics | completed |
| Phase 5 | Testing, CI, and release hardening | completed |
| Phase 6 | Completion docs and README upgrade | completed |

## Decision Log

- 2026-02-18: Canonical source is `Packages/com.pakaya.mcp.vfx/Editor/Tools/Vfx`.
- 2026-02-18: URP-only support remains enforced.
- 2026-02-18: Use sync tooling and CI drift checks to keep `Assets/MCPForUnity/Editor/Tools/Vfx` compatible while package remains canonical.

## Test Log

- 2026-02-18: Initial setup, no new tests executed yet.
- 2026-02-18: Ran `python3 scripts/sync_vfx_tools.py --write` to establish package-assets mirror parity.

## Open Questions

- None at this phase start.

## Work Log

### Phase 1 Start

- Set up implementation tracker.
- Next: add package-first sync strategy and CI drift checks.

### Phase 1 Complete

- Added package-first sync utility at `scripts/sync_vfx_tools.py`.
- Added CI drift workflow at `.github/workflows/vfx-sync-check.yml`.
- Updated package docs to reflect canonical package ownership and mirror policy.
- Synced compatibility mirror files from package into `Assets/MCPForUnity/Editor/Tools/Vfx`.

### Phase 2 Start

- Implement critical workflow fixes from `docs/MCP-VFX-Tool-Limitations.md`.

### Phase 2 Complete

- Added `SerializableType` conversion handling in `SetNodeSetting`.
- Hardened `LinkGPUEvent` with:
  - invalidation + save before linking,
  - multi-index retry strategy,
  - richer attempted-index diagnostics.
- Added `set_capacity` action end-to-end:
  - action contract registration,
  - router wiring,
  - reflection implementation against context `VFXData`.
- Enhanced `get_graph_info` to expose:
  - parameter display names (`m_ExposedName` fallback),
  - context capacity when available.
- Replaced `inspect_vfx_asset` debug stub with live graph/property inspection responses.

### Phase 3 Complete

- Added modular service boundaries:
  - `VfxGraphNodeService`
  - `VfxGraphConnectionService`
  - `VfxGraphPropertyService`
  - `VfxGraphIntrospectionService`
  - `VfxGraphPersistenceService`
- Updated operation facades to use service modules.
- Routed `SafeInvalidate` through persistence service to centralize invalidation behavior.

### Phase 4 Complete

- Expanded action surface with:
  - `disconnect_nodes`
  - `get_connections`
  - `save_graph`
- Improved slot/property failure payloads with available alternatives.
- Improved reflection method resolution strategy in `VfxGraphReflectionCache`:
  - optional signature-aware lookup
  - safer fallback matching instead of broad ambiguous overload selection.

### Phase 5 Complete

- Added package EditMode test assembly and test files under `Packages/com.pakaya.mcp.vfx/Tests/Editor`:
  - `VfxInputValidationTests`
  - `VfxActionsTests`
  - `VfxGraphReflectionCacheTests`
- Added internals visibility for package tests:
  - `Packages/com.pakaya.mcp.vfx/Editor/VfxAssemblyInternals.cs`
- Updated CI workflow `.github/workflows/unity-editmode-tests.yml` with package-focused VFX test filter and artifacts.

### Phase 6 Complete

- Upgraded package README with:
  - architecture,
  - supported actions,
  - quality/maintainability expectations,
  - troubleshooting guidance.
- Added changelog updates for canonical package ownership and sync/CI drift checks.
- Mirror parity revalidated after all package edits.
