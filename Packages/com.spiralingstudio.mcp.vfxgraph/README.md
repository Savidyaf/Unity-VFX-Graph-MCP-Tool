# Spiraling Studio MCP VFX Graph Tools

`com.spiralingstudio.mcp.vfxgraph` is a URP-first VFX Graph extension package for MCP for Unity.

## Purpose

This package enables AI agents to build and edit Unity VFX Graph assets through MCP actions, including graph topology, properties, blocks, GPU-event links, GraphicsBuffer pipelines, ECS integration, and runtime assignment workflows.

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
- `VfxGraphEdit` (partial) is the core implementation for all graph operations.
- `VfxGraphEditActions` extends `VfxGraphEdit` with attribute blocks, batch execution, context settings, output configuration, compilation, custom attributes, bounds, block management, buffer pipelines, and recipe-based creation.
- `VfxAttributeAliases` maps friendly block names (e.g. SetPosition, Turbulence) to internal VFX types and auto-configures settings.
- `VfxGraphPersistenceService` centralizes asset persistence, invalidation, and deferred batch saving.
- `VfxGraphReflectionCache` caches all type resolution and method lookups, including VFXType-attributed struct scanning.
- `VfxToolContract` and `VfxGraphResultMapper` normalize response shapes.
- `VfxInputValidation` handles input parameter validation.
- `VfxGraphAssets` manages VFX asset lifecycle (create, assign, list).

## Supported actions (`manage_vfx_graph`)

### Introspection

- `get_graph_info` — full graph topology with data connections, block settings, parameter names
- `get_connections` — slot-level data connections
- `list_node_types` — available VFX node types
- `list_block_types` — available VFX block types
- `list_properties` — blackboard properties
- `list_attributes` — built-in + custom attributes with types, defaults, aliases
- `get_node_settings` — settings for a specific node
- `get_compilation_status` — shader compilation errors
- `save_graph`

### Node and slot operations

- `add_node` — with enriched error suggestions for unknown types
- `remove_node`
- `move_node`
- `duplicate_node`
- `connect_nodes`
- `disconnect_nodes`
- `set_node_property`
- `set_node_setting` — with available settings in error responses

### Context and block operations

- `link_contexts`
- `link_gpu_event` — with pre-link reimport for flow slot materialization
- `add_block` — auto-resolves aliases (SetPosition, Turbulence, etc.)
- `add_attribute_block` — dedicated action for SetAttribute with attribute/composition/source/random/channels
- `remove_block`
- `reorder_block` — change block execution order within a context
- `set_block_activation` — enable/disable blocks
- `set_context_settings` — Update context toggles, Spawn loop/delay, any context setting
- `configure_output` — output type, blend mode, orientation, settings
- `set_bounds` — Initialize context AABox bounds
- `set_space`
- `set_capacity`

### Blackboard and code operations

- `add_property`
- `remove_property`
- `set_property_value`
- `set_hlsl_code`

### Custom attributes

- `add_custom_attribute` — add graph-level custom attribute with name, type, description
- `remove_custom_attribute`

### Buffer and ECS integration

- `create_buffer_helper` — low-level GraphicsBuffer helper
- `setup_buffer_pipeline` — composite: creates buffer property + SampleBuffer + configures type + generates VFXType struct code
- `compile_graph` — trigger shader compilation

### Batch and recipes

- `batch` — execute multiple operations in a single call with deferred save and symbolic references ($spawn, $init, etc.)
- `create_from_recipe` — create common graph patterns from templates:
  - `ecs_buffer_particles` — Spawn→Init→Update→Output with GraphicsBuffer property and SampleBuffer
  - `simple_spawn_particles` — basic particle system with lifetime, velocity, color
  - `gpu_event_chain` — parent→child particle system via GPU events
  - `particle_strip_trail` — particle strip trail rendering

### Asset lifecycle operations

- `create_asset`
- `list_assets`
- `list_templates`
- `assign_asset`

### Console

- `read_vfx_console`

## Quality and maintainability standards

- Deterministic contract responses with explicit `error_code` values.
- Action routing isolates MCP schema from implementation details.
- Reflection methods use caching to reduce lookup overhead.
- Partial class architecture separates core graph operations from extended actions.
- Attribute alias system is data-driven and easily extensible.
- Batch API defers persistence for performance with symbolic reference resolution.
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
- Custom attribute API availability depends on VFX Graph version (17+).
- For best stability, use validated Unity/VFX package combinations in project CI.

## Troubleshooting

- If actions fail with `unsupported_pipeline`, switch project render pipeline to URP.
- If tool results look stale, run `save_graph` and retry introspection actions.
- If mirror drift CI fails, run `python3 scripts/sync_vfx_tools.py --write` and commit synced files.
- Use `inspect_vfx_asset` for a full diagnostic dump of any VFX asset.
- Use `list_attributes` to discover available attributes and their SetBlock aliases.
