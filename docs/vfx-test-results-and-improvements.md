# VFX MCP Test Implementation Notes

## What was implemented

- Added EditMode automation in `Assets/MCPForUnity/Editor/Tests/Editor/VfxToolContractTests.cs`.
- Replaced the previously commented-out placeholder tests with active tests that cover:
  - active tool routing checks for `manage_vfx` and optional `manage_vfx_graph`
  - end-to-end particle smoke flow (create object, add `ParticleSystem`, configure, play, read info)
  - module and burst smoke flow (`particle_enable_module`, `particle_set_size_over_lifetime`, `particle_add_burst`)
  - negative contract tests for invalid action and invalid target
  - graph unknown-action contract compatibility (`unknown_action` or `unsupported_pipeline`)

## Coverage mapping to the plan

- **Validate active tools:** covered by `ActiveToolRouting_*` tests.
- **Run particle smoke tests:** covered by `ParticleSmoke_*` tests.
- **Run negative tests:** covered by `NegativeContracts_*` tests.
- **Derive improvement worklist:** captured below.

## How to run

Run with Unity Test Runner (EditMode), or Unity CLI on a machine that has a Unity Editor executable:

`-runTests -testPlatform EditMode -projectPath <repo-path> -testResults <path-to-results.xml>`

## Live MCP runtime results (Unity-connected)

The following was executed directly through the connected Unity MCP server in Cursor:

- Connectivity:
  - `manage_editor telemetry_status` -> success
  - `manage_scene get_active` -> success
- Tool routing:
  - `manage_vfx action=ping` -> success (legacy schema: `success`, `tool`, `components`)
  - `execute_custom_tool manage_vfx_graph action=ping` -> graph tool reachable; returns unknown graph action for `ping`
- Particle smoke path:
  - `manage_gameobject create name=MCP_Particle_Basic_Runtime` -> success
  - `manage_components add component=ParticleSystem` -> success
  - `manage_vfx particle_set_main/set_emission/set_shape` -> success
  - `manage_vfx particle_play` -> success
  - `manage_vfx particle_get_info` -> confirms main/emission/shape settings and running state
  - `manage_vfx particle_enable_module noise`, `particle_set_size_over_lifetime`, `particle_add_burst` -> success
  - Follow-up `particle_get_info` -> confirms `burstCount: 1`
- Persistence / duplication:
  - `manage_scene save` worked only with both `name` and `path` present
  - `manage_gameobject duplicate` preserved `ParticleSystem` and settings
  - `find_gameobjects by_name` confirmed the created object remained in scene
- Negative checks:
  - Unknown action (`particle_not_real`) -> clear error list of valid actions
  - Invalid target on `particle_set_main` -> `ParticleSystem not found`
  - `manage_vfx_graph action=definitely_not_real` -> clear unknown graph action

## Runtime issues observed

1. `read_console` is currently unusable in this environment.
   - Returned: `ReadConsole handler failed to initialize due to reflection errors.`
   - Impact: cannot inspect Unity warnings/errors via MCP during automated smoke tests.

2. `manage_scene save/load` path concatenation behavior appears incorrect.
   - Save response path became nested: `Assets/Scenes/MCPParticleRuntimeTest.unity/MCPParticleRuntimeTest.unity`.
   - Load then attempted to append scene name again, resulting in triple nesting and failure.
   - Impact: scene round-trip flows are unreliable if caller provides full `.unity` path.

## VFX Graph runtime results (live MCP)

The following graph-oriented checks were executed directly:

- `manage_vfx` graph runtime entrypoints (`vfx_list_assets`, `vfx_list_templates`) returned:
  - `VFX Graph package (com.unity.visualeffectgraph) not installed`
- Asset search via `manage_asset` confirmed VisualEffectGraph assets exist in packages, including:
  - `Packages/com.unity.visualeffectgraph/Editor/Templates/Minimal_System.vfx`
- `manage_vfx_graph` (via `execute_custom_tool`) worked and supported:
  - `list_node_types` (288 types found)
  - `get_graph_info` (success)
  - `add_node` (success)
  - `set_node_property` (success)
  - `list_block_types` (success)
  - `add_block` (success when using `blockType`)
  - `add_property` / `list_properties` (success)
  - `get_node_settings` / `set_node_setting` (success when using `settingName`)
- Missing/failed graph capabilities:
  - `create_asset` on `manage_vfx_graph` -> unknown action
  - `remove_node` on `manage_vfx_graph` -> unknown action
  - `manage_asset create asset_type=VisualEffectAsset` -> unsupported type

## Additional VFX Graph issues observed

1. Contradictory graph capability surface between tools.
   - `manage_vfx` says VFX Graph package is not installed.
   - `manage_vfx_graph` can enumerate and edit graph nodes in the same session.
   - Likely root cause: compile-symbol/conditional mismatch in `manage_vfx`.

2. No robust graph asset creation path.
   - `manage_vfx_graph` does not implement `create_asset` in the active runtime variant.
   - `manage_asset` does not support `VisualEffectAsset` creation.
   - Net effect: graph editing works only on pre-existing `.vfx` assets.

3. Graph action parameter naming is inconsistent.
   - `add_block` expects `blockType` (not `type`).
   - `set_node_setting` expects `settingName` (not `setting`).
   - Causes avoidable trial-and-error for tool users.

4. `list_properties` returned empty `type` for created property.
   - `add_property` reported `type: float`, but `list_properties` returned `type: ""`.
   - Indicates serialization/typing gap in graph property listing.

## Prioritized improvements

1. Fix contradictory VFX Graph package gating in `manage_vfx`.
   - Align compile-symbol checks and runtime detection with actual installed package state.
   - Ensure `vfx_*` actions do not report false negatives when graph APIs are available.

2. Add a supported graph asset creation API path.
   - Implement `create_asset` in active `manage_vfx_graph`, or
   - Extend `manage_asset create` to support `VisualEffectAsset` (with template selection).

3. Implement missing graph edit primitives.
   - Add `remove_node` (and likely node deletion safety checks).

4. Standardize graph action parameter names and aliases.
   - Accept both `type`/`blockType` and `setting`/`settingName` to reduce friction.

5. Fix graph property type reporting in `list_properties`.
   - Ensure created property type round-trips consistently.

6. Fix `manage_scene` save/load path normalization and round-trip behavior.
   - Ensure `save` respects explicit `.unity` file paths without appending extra segments.
   - Ensure `load` accepts exact scene file paths verbatim.

7. Fix `read_console` reflection initialization failure.
   - Add fallback when reflection fails and return actionable diagnostics.
   - Add tests for `read_console get` happy path and initialization error path.

8. Resolve command-surface ambiguity between `Assets` and `Packages` VFX handlers.
   - Risk: duplicate tool names (`manage_vfx`) can register with override behavior depending on discovery order.
   - Suggestion: keep one canonical implementation path and remove/disable the other from compilation.

9. Standardize VFX response contracts.
   - Current risk: mixed legacy and structured schemas complicate client logic.
   - Suggestion: converge `manage_vfx` responses onto a versioned structured contract.

10. Re-enable graph contract validation in CI for this repository variant.
   - Suggestion: keep tests tolerant to optional `manage_vfx_graph` availability, but enforce known error-code behavior when present.

11. Add one scripted baseline scenario for graph creation/editing in docs.
   - Suggestion: include exact payloads used by this live graph smoke flow to make regression checks repeatable.
