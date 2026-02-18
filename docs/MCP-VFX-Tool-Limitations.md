# MCP VFX Graph Tool — Limitations & Improvement Roadmap

> Observations from building a buffer-driven multi-gun battlefield VFX pipeline entirely through MCP tooling.  
> Unity 6000.5.0a6 · VFX Graph package · URP · macOS

---

## Table of Contents

1. [Critical Limitations](#1-critical-limitations)
2. [Structural / Design Limitations](#2-structural--design-limitations)
3. [Missing Operations](#3-missing-operations)
4. [Reflection Fragility](#4-reflection-fragility)
5. [Ergonomic Issues](#5-ergonomic-issues)
6. [Non-VFX MCP Tool Issues](#6-non-vfx-mcp-tool-issues)
7. [Improvement Roadmap](#7-improvement-roadmap)

---

## 1. Critical Limitations

### 1.1 `set_node_setting` cannot handle `SerializableType` fields

**Impact:** Cannot configure `SampleBuffer.m_Type`, making the SampleBuffer operator unusable through MCP.

**What happens:** The `SampleBuffer` operator's `m_Type` setting is of type `UnityEditor.VFX.SerializableType`. When `set_node_setting` receives a string value, it tries to pass it directly to `SetSettingValue(string, object)` which expects a `SerializableType` instance, not a raw string.

**Location:** `VfxGraphEdit.cs:1247-1420` — the `SetNodeSetting` method handles enums, primitives, and strings but has no handler for `SerializableType`.

**Workaround used:** Replaced `SampleBuffer` with a `CustomHLSL` block that defines the struct inline and samples the buffer via HLSL.

**Fix:** Add a `SerializableType` case to `SetNodeSetting` that resolves the type name to a `System.Type` via `AppDomain.GetAssemblies()`, then constructs a `SerializableType` instance via reflection.

```csharp
// Pseudocode for the fix
if (settingType.Name == "SerializableType")
{
    Type resolved = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes())
        .FirstOrDefault(t => t.Name == valueStr || t.FullName == valueStr);
    if (resolved != null)
        convertedValue = Activator.CreateInstance(settingType, resolved);
}
```

---

### 1.2 `link_gpu_event` fails even with TriggerEvent block present

**Impact:** Cannot create GPU Event trail pipelines through MCP.

**What happens:** After adding a `TriggerEvent` block to an Update context, calling `link_gpu_event` still fails with _"GPU Events require a TriggerEvent block"_. The tool's `LinkTo`/`LinkFrom` invocations throw, and the generic error message obscures the actual cause.

**Location:** `VfxGraphEdit.cs:2177-2296`

**Root cause candidates:**
- The TriggerEvent block may need a graph recompile/invalidation before it creates the GPU event flow output slot
- The `sourceFlowIndex` auto-detection (`outputFlowCount > 1 ? 1 : 0`) may not account for GPU event outputs correctly — VFX Graph separates flow outputs from GPU event outputs internally
- `LinkTo`/`LinkFrom` may require the target context to be specifically of `VFXBasicGPUEvent` type, not `VFXBasicInitialize`

**Fix suggestions:**
1. After adding a TriggerEvent block, call `SafeInvalidate` and `AssetDatabase.SaveAssets()` before attempting the link
2. Enumerate GPU event flow outputs separately from regular flow outputs (they may live in a different property)
3. Return the actual `TargetInvocationException` inner messages instead of the generic remediation text
4. Consider adding an explicit `refresh_graph` or `recompile_graph` action

---

### 1.3 `read_console` is completely broken

**Impact:** No way to verify compilation errors, VFX graph shader errors, or runtime exceptions through MCP.

**Error:** _"ReadConsole handler failed to initialize due to reflection errors. Cannot access console logs."_

**Root cause:** Unity 6000.5.0a6 (alpha) likely changed internal console reflection APIs. The console reader initializes once and if reflection fails, it stays broken for the entire session.

**Fix:** Implement fallback log reading (e.g., `Application.logMessageReceived` callback, or reading `Editor.log` file directly).

---

### 1.4 Cannot set particle capacity on Initialize context

**Impact:** Default capacity (typically 64-256) is far too low for thousands of buffer-driven projectiles.

**What happens:** `set_node_property` with `property: "capacity"` fails because capacity is stored on the `VFXDataParticle` data model associated with the system, not directly on the `VFXBasicInitialize` context node. There are no settings exposed on `VFXBasicInitialize` either.

**Fix:** Add a dedicated `set_capacity` action or enhance `set_node_setting` to walk from the context to its associated `VFXData` object and set capacity there.

---

## 2. Structural / Design Limitations

### 2.1 `inspect_vfx_asset` is a debug stub, not an inspector

The tool is currently hardcoded to search for `Link` methods on `VFXSlot` using reflection. It ignores its `path` parameter and returns the same debug output regardless of which asset is inspected.

**Location:** `InspectVFXAsset.cs:11-35`

**Should be:** A proper asset introspection tool that returns:
- Graph summary (context count, node count, property count)
- System pipeline topology (Spawn → Init → Update → Output chains)
- Exposed properties with types and default values
- Compilation status and shader errors
- Capacity settings per system

---

### 2.2 URP-only gate blocks HDRP and Built-in usage

`ManageVfxGraph.cs:25-36` validates URP support and returns an error for any other pipeline. VFX Graph works across pipelines (with different output node types). This gate is unnecessarily restrictive.

**Fix:** Make the pipeline check a warning rather than a blocking error, or make it configurable.

---

### 2.3 No graph-level undo/redo support

All operations call `EditorUtility.SetDirty()` and `AssetDatabase.SaveAssets()` immediately. There is no undo grouping, so a multi-step graph build (add 6 nodes, 4 blocks, 5 connections) creates 15+ individual undo entries and 15+ disk writes.

**Fix:**
- Batch operations within a single `Undo.IncrementCurrentGroup()` / named undo group
- Defer `AssetDatabase.SaveAssets()` to the end of a batch, or expose a `save_graph` action

---

### 2.4 VFXParameter names not visible in `get_graph_info`

Parameters show `"name": ""` in graph info output because the exposed name is stored in `m_ExposedName` (a SerializedProperty), not in the ScriptableObject's `name` field. This makes it impossible to distinguish which parameter is which by name.

**Location:** `VfxGraphEdit.cs:674` — uses `model.name` which is empty for parameters.

**Fix:** For VFXParameter nodes, read `m_ExposedName` via `SerializedObject` and include it in the output.

---

## 3. Missing Operations

### 3.1 No `set_capacity` action
See §1.4. Capacity lives on `VFXData`, not on the context. Need a dedicated action.

### 3.2 No `disconnect_nodes` action
Can connect nodes but cannot disconnect existing data links. Only `remove_node` and `remove_block` exist.

### 3.3 No `get_connections` action
No way to query which data slots are currently connected. `get_graph_info` shows slots but not their link targets. Essential for debugging broken graphs.

### 3.4 No `set_bounds` action
Initialize context bounds (`bounds`, `boundsPadding`) are critical for culling. Currently readable as input slots but there's no ergonomic way to set AABox values through `set_node_property`.

### 3.5 No `compile_graph` / `get_compilation_status` action
No way to trigger VFX Graph recompilation or check for shader compilation errors. Combined with the broken `read_console`, this means there's zero compilation feedback.

### 3.6 No `set_output_mesh` / `set_output_texture` action
Output contexts need mesh/texture/shader graph references. The tool has no way to assign these asset references to output context properties.

### 3.7 No `create_subgraph` action
Cannot create or manage VFX Subgraphs programmatically.

### 3.8 No `list_attributes` action
No way to enumerate available VFX attributes (position, velocity, lifetime, age, particleId, etc.) for use with `SetAttribute` blocks and `VFXAttributeParameter` nodes.

---

## 4. Reflection Fragility

### 4.1 All graph operations are reflection-based

The entire tool relies on reflecting into `UnityEditor.VFX.*` internal types. These are:
- Not part of any public API contract
- Subject to change between Unity versions without notice
- Particularly unstable in alpha/preview builds (like 6000.5.0a6)

**Key reflection targets that could break:**
| Type/Method | Used For | Risk |
|---|---|---|
| `VisualEffectResource.GetResourceAtPath` | Loading graphs | High — internal static |
| `VisualEffectResourceExtensions.GetOrCreateGraph` | Getting graph ScriptableObject | High — extension method |
| `VFXModel.AddChild(model, index, bool)` | Adding nodes/blocks | High — signature may change |
| `VFXContext.LinkTo(context, fromIdx, toIdx)` | Flow links | Medium |
| `VFXSlot.Link(slot, bool)` | Data connections | Medium |
| `InvalidationCause` enum | Graph invalidation | Medium — enum values may change |

### 4.2 Assembly scanning on every operation

`VfxGraphReflectionCache` caches types and methods, but `SafeInvalidate`, `SetNodeSetting`, and `AddNode` each perform their own `AppDomain.GetAssemblies().SelectMany(GetTypes())` scans. This is expensive and redundant.

**Fix:** Centralize all type resolution through `VfxGraphReflectionCache` and cache `InvalidationCause`, `VFXBlock`, etc. on first use.

### 4.3 `GetMethodCached` resolves ambiguous matches by parameter count

`VfxGraphReflectionCache.cs:58-63` picks the overload with the most parameters on `AmbiguousMatchException`. This is a fragile heuristic — if Unity adds a new overload with more parameters but different semantics, the tool silently calls the wrong method.

---

## 5. Ergonomic Issues

### 5.1 No batch/transaction API

Building a graph requires 15-30 sequential MCP calls. Each call independently loads the graph, modifies it, saves, and returns. A batch API like:

```json
{
  "action": "batch",
  "path": "Assets/VFX/MyEffect.vfx",
  "operations": [
    { "op": "add_node", "type": "VFXBasicSpawner", "position": [0,0] },
    { "op": "add_node", "type": "VFXBasicInitialize", "position": [0,300] },
    { "op": "link_contexts", "from": "$0", "to": "$1" }
  ]
}
```

would reduce round-trips by 10x and enable atomic graph construction.

### 5.2 Instance IDs are negative and session-specific

All node references use Unity instance IDs (e.g., `-62834`). These are:
- Negative (confusing)
- Session-specific (change on domain reload)
- Not predictable (can't pre-plan a graph build)

**Improvement:** Support symbolic references (e.g., `"$spawn"`, `"$init"`) within a batch, or stable GUID-based identifiers.

### 5.3 Error messages lack remediation guidance

Most errors return generic messages like _"Input property 'X' not found on node"_ without listing available properties. The `get_graph_info` output helps, but the error response itself should include available slot names.

**Good example (already present):** `link_contexts` error includes `sourceOutputFlowSlots`, `targetInputFlowSlots`, and remediation text.

**Bad example:** `set_node_property` just says "not found" with no alternatives listed.

### 5.4 `get_graph_info` doesn't show data connections

The graph info shows flow links between contexts but not data connections between operator outputs and block inputs. This makes it impossible to verify wiring without opening the graph in the Unity editor.

### 5.5 `list_node_types` / `list_block_types` filter is substring-only

The filter matches by `type.Name.Contains(filter)`. There's no way to filter by category (operator vs context vs block), compatible context type, or value type.

---

## 6. Non-VFX MCP Tool Issues

### 6.1 `editor_state` perpetually stale

The editor state resource consistently reports `ready_for_tools: false` with `blocking_reasons: ["stale_status"]` even when tools work fine. The staleness age keeps growing. This likely confuses automated agents that check readiness before acting.

### 6.2 `manage_components.set_property` limited for object references

Setting VisualEffect asset references via `set_property` requires passing `{"guid": "..."}` — this works but isn't documented. Setting references to components on other GameObjects (like `ProjectileVFXBridge.tracerVFX = <VisualEffect on another GO>`) requires knowing the exact serialization format.

### 6.3 `manage_gameobject.modify` cannot set Transform rotation via `manage_components`

Setting rotation through `manage_components.set_property` on a Transform failed (`"Value cannot be null"`), but worked fine through `manage_gameobject.modify`. Inconsistent behavior.

---

## 7. Improvement Roadmap

### Priority 1 — Unblock core workflows

| Item | Impact | Effort |
|---|---|---|
| Fix `SerializableType` handling in `set_node_setting` | Unblocks SampleBuffer configuration | Low |
| Fix `link_gpu_event` (invalidate before link, enumerate GPU event slots) | Unblocks trail/sub-effect pipelines | Medium |
| Add `set_capacity` action | Unblocks buffer-driven VFX | Low |
| Fix `read_console` for Unity 6 alpha | Restores compilation feedback | Medium |
| Show parameter `m_ExposedName` in `get_graph_info` | Makes properties identifiable | Low |

### Priority 2 — Reduce round-trips

| Item | Impact | Effort |
|---|---|---|
| Batch/transaction API for graph operations | 10x fewer MCP calls for graph builds | High |
| Deferred save (save only on explicit `save_graph` call) | Faster multi-step operations | Medium |
| Undo grouping for multi-step edits | Clean undo history | Low |

### Priority 3 — Improve discoverability

| Item | Impact | Effort |
|---|---|---|
| Rewrite `inspect_vfx_asset` as proper graph inspector | Replaces the debug stub | Medium |
| Add `get_connections` to show data link topology | Essential for debugging | Medium |
| Include available slots in error messages | Reduces trial-and-error | Low |
| Add `list_attributes` action | Helps with SetAttribute configuration | Low |
| Richer `list_node_types` filtering (by category, compatible context) | Better discoverability | Low |
| Show data connections in `get_graph_info` | Complete graph picture | Medium |

### Priority 4 — Robustness

| Item | Impact | Effort |
|---|---|---|
| Centralize all reflection through `VfxGraphReflectionCache` | Performance + maintainability | Medium |
| Add Unity version compatibility matrix | Prevents silent breakage | Low |
| Remove or soften URP-only gate | Broader pipeline support | Low |
| Add `compile_graph` + `get_compilation_status` actions | Programmatic build verification | Medium |
| Add `disconnect_nodes` action | Complete CRUD for connections | Low |

---

## Appendix: Session Summary

During this session, building two buffer-driven VFX graphs (BulletTracers + MissileTrails) required approximately **40 MCP tool calls**. The following workarounds were needed:

1. **SampleBuffer unusable** → Used CustomHLSL block with inline struct definition
2. **GPU Event linking broken** → Dropped trail sub-pipeline, kept single-system missile graph
3. **No console access** → Worked blind on compilation status
4. **No capacity control** → Left at defaults (needs manual editor adjustment)
5. **Parameter names invisible** → Had to track property IDs manually across calls
