# Spiraling Studio MCP VFX Graph Tools

`com.spiralingstudio.mcp.vfxgraph` is a URP-first VFX Graph extension package for MCP for Unity.

## Purpose

This package enables AI agents to build and edit Unity VFX Graph assets through MCP actions, including graph topology, properties, blocks, GPU-event links, and runtime assignment workflows.

## Source of truth and compatibility

- Canonical implementation is in `Packages/<package-name>/Editor/Tools/Vfx`.
- `Assets/MCPForUnity/Editor/Tools/Vfx` is a compatibility mirror to support MCP runtime loading patterns.
- Mirror sync command:
  - `python3 scripts/sync_vfx_tools.py --write`
- Mirror drift is enforced in CI:
  - `.github/workflows/vfx-sync-check.yml`

## Architecture

- `ManageVfxGraph` is the primary MCP entrypoint (`manage_vfx_graph`).
- `VfxGraphActionRouter` maps action names directly to `VfxGraphEdit` methods.
- `VfxGraphEdit` is the core implementation for all graph operations.
- `VfxGraphPersistenceService` centralizes asset persistence and invalidation.
- `VfxGraphReflectionCache` caches all type resolution and method lookups.
- `VfxToolContract` and `VfxGraphResultMapper` normalize response shapes.
- `VfxInputValidation` handles input parameter validation.
- `VfxGraphAssets` manages VFX asset lifecycle (create, assign, list).

## Supported actions (`manage_vfx_graph`)

### Introspection

- `get_graph_info`
- `get_connections`
- `list_node_types`
- `list_block_types`
- `list_properties`
- `get_node_settings`
- `save_graph`

### Node and slot operations

- `add_node`
- `remove_node`
- `move_node`
- `duplicate_node`
- `connect_nodes`
- `disconnect_nodes`
- `set_node_property`
- `set_node_setting`

### Context and block operations

- `link_contexts`
- `link_gpu_event`
- `add_block`
- `remove_block`
- `set_space`
- `set_capacity`

### Blackboard and code operations

- `add_property`
- `remove_property`
- `set_property_value`
- `set_hlsl_code`
- `create_buffer_helper`

### Asset lifecycle operations

- `create_asset`
- `list_assets`
- `list_templates`
- `assign_asset`

## Quality and maintainability standards

- Deterministic contract responses with explicit `error_code` values.
- Action routing isolates MCP schema from implementation details.
- Reflection methods use caching to reduce lookup overhead.
- New features require tests in `Packages/<package-name>/Tests/Editor`.
- Package-focused tests are enforced by `.github/workflows/unity-editmode-tests.yml`.

## Pipeline support

- Guaranteed support: URP.
- Non-URP pipelines return structured `unsupported_pipeline` errors.

## Installation and setup

1. Open Unity Package Manager.
2. Add package from disk (embedded package) or Git URL to this package path.
3. Ensure `com.unity.visualeffectgraph` and `com.unity.render-pipelines.universal` are present.
4. Start MCP for Unity and reconnect your client so custom tools refresh.

## Known constraints

- Unity VFX internals are reflection-driven and can change across Unity versions.
- GPU event flow slot indexing may vary by graph structure; diagnostics include attempted indices.
- For best stability, use validated Unity/VFX package combinations in project CI.

## Troubleshooting

- If actions fail with `unsupported_pipeline`, switch project render pipeline to URP.
- If tool results look stale, run `save_graph` and retry introspection actions.
- If mirror drift CI fails, run `python3 scripts/sync_vfx_tools.py --write` and commit synced files.
