# MCP VFX Graph Tool — Limitations & Changelog

> Observations from building buffer-driven VFX pipelines through MCP tooling.
> Unity 6000.5.0 · VFX Graph 17 · URP

---

## v2.0.0 — Resolved Limitations

The following issues from the original audit have been addressed:

| # | Issue | Resolution |
|---|-------|------------|
| 1.1 | `SerializableType` handling in `set_node_setting` | Hardened `ResolveRuntimeTypeByName` with common aliases (float/Vector3/etc.), `VFXType`-attributed struct scanning via `ResolveVFXType` |
| 1.2 | `link_gpu_event` failures | Added pre-link reimport step to materialize flow slots, improved diagnostics |
| 1.4 | Cannot set particle capacity | `set_capacity` action (existed since v1) |
| 2.1 | `inspect_vfx_asset` is a debug stub | Rewritten with full introspection: graph info, properties, connections, compilation status, attributes, block aliases |
| 2.4 | VFXParameter names not visible | `exposedName` now included in `get_graph_info` node data |
| 3.4 | No `set_bounds` action | Added `set_bounds` with center/size for Initialize context |
| 3.5 | No compilation feedback | Added `compile_graph` and `get_compilation_status` |
| 3.8 | No `list_attributes` action | Added with built-in + custom attributes, types, defaults, aliases |
| 5.1 | No batch API | Added `batch` action with deferred persistence and symbolic references |
| 5.3 | Error messages lack remediation | `add_node`, `add_block`, `set_node_setting` now include suggestions and available alternatives |
| 5.4 | No data connections in graph info | `get_graph_info` now includes `dataConnections` array |

### New capabilities added

| Action | Description |
|--------|-------------|
| `add_attribute_block` | Dedicated SetAttribute block creation with attribute/composition/source/random/channels |
| `set_context_settings` | Apply settings to any context (Update toggles, Spawn loop/delay) |
| `configure_output` | Output type selection, blend mode, orientation, settings |
| `add_custom_attribute` | Add graph-level custom attributes |
| `remove_custom_attribute` | Remove custom attributes |
| `set_block_activation` | Enable/disable blocks |
| `reorder_block` | Change block execution order |
| `setup_buffer_pipeline` | Composite: buffer property + SampleBuffer + type config + VFXType struct generation |
| `create_from_recipe` | Template-based graph creation (ecs_buffer_particles, simple_spawn_particles, gpu_event_chain, particle_strip_trail) |
| `batch` | Multi-operation execution with symbolic references and single save |

### Attribute alias system

`add_block` now auto-resolves 80+ friendly names to internal block types with auto-configured settings. Examples: `SetPosition`, `AddVelocity`, `Turbulence`, `CollisionShape`, `ConstantSpawnRate`, `TriggerEventOnDie`, etc.

---

## Remaining Known Limitations

### Console access in Unity 6 alpha
`read_console` may fail in some Unity 6 alpha builds due to internal API changes. A project-level `read_vfx_console` workaround is available.

### Reflection fragility
All graph operations rely on reflecting into `UnityEditor.VFX.*` internal types. These can change across Unity versions without notice. Key risk areas:

| Type/Method | Used For | Risk |
|---|---|---|
| `VisualEffectResource.GetResourceAtPath` | Loading graphs | High |
| `VFXModel.AddChild` | Adding nodes/blocks | High |
| `VFXContext.LinkTo` | Flow links | Medium |
| `VFXSlot.Link` | Data connections | Medium |

### No subgraph creation
Cannot create or manage VFX Subgraphs programmatically.

### URP-only gate
Non-URP pipelines return `unsupported_pipeline` errors. VFX Graph technically works with HDRP, but this tool only guarantees URP support.

### Custom attribute API version dependency
`add_custom_attribute` / `remove_custom_attribute` depend on VFX Graph 17+ internal APIs. Older versions may not expose the required methods or serialized properties.

---

## Appendix: Original Session Summary (v1.0)

During the initial audit session, building two buffer-driven VFX graphs (BulletTracers + MissileTrails) required ~40 MCP calls. The following workarounds were needed:

1. **SampleBuffer unusable** → Used CustomHLSL block (now fixed via SerializableType handling)
2. **GPU Event linking broken** → Dropped trail sub-pipeline (now fixed via reimport step)
3. **No console access** → Worked blind on compilation status (now mitigated via `compile_graph`/`get_compilation_status`)
4. **No capacity control** → Left at defaults (fixed in v1 via `set_capacity`)
5. **Parameter names invisible** → Tracked IDs manually (now fixed via `exposedName` in graph info)
