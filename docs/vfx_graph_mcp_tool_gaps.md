# manage_vfx_graph — gaps & bugs

Found while building a parented ECS‑driven thruster VFX (flame + GPU‑event smoke trail). Unity 6.x URP, VFX Graph 17.

## Silent success / wrong state

1. **`add_attribute_block channels: "X"|"Y"|"Z"`** — param accepted, reported success, never applied. Block stays `channels=XYZ` (Vector3 slot) instead of becoming single‑float.
2. **`set_node_setting channels = X/Y/Z`** on `SetAttribute` for variadic attributes — returns success. VFX MCP package actually logs `ApplyBlockSettings: failed to set 'Channels' = 'X': Exception thrown by target of invocation`. The exception needs to propagate to the caller.
3. **`connect_nodes toSlot: "_Scale.x"`** (dot‑path on target) — returns success, no connection made, no warning. Save then silently drops any prior linked slot because the target slot type changed under it.
4. **`connect_nodes` batch success** — individual ops can be silently dropped during save (`Remove linked slot(s) that couldn't be deserialized from <slot>` warnings) while `allSucceeded: true`. Batch should post‑verify.
5. **`set_property_value` with `Texture2D` and asset path string** — returns success. Underlying API logs `Cannot assign an object of type System.String to VFXSerializedObject of type UnityEngine.Texture2D`. Fix: load the asset by path before assigning, or reject.
6. **`set_property_value` with `Vector3`/`Vector4` from JSON array** — fails with `Cannot assign an object of type Newtonsoft.Json.Linq.JArray to VFXSerializedObject of type UnityEngine.Vector3`. Needs JArray→Vector conversion. Also returns inconsistent success/failure state vs. the log.
7. **`set_bounds` before `set_data_settings boundsMode=Manual`** — reports success but VFX ignores the bounds (slot is overridden by Recorded mode). Tool should either auto‑flip boundsMode or warn.
8. **`set_node_property` with a setting name** (e.g. `eventName` on `VFXBasicEvent`) — returns generic failure. Should hint "this is a setting, use set_node_setting".

## Hard failures that should work

9. **`add_node type: "Lerp"`** — fails with `internal_exception`. Only the fully qualified `UnityEditor.VFX.Operator.Lerp` works. Name collision with `UnityEngine.UIElements.Experimental.Lerp` (console logs `missing the class attribute 'ExtensionOfNativeClass'`). Need disambiguation or a VFX‑namespace preference when multiple types resolve.
10. **`list_node_types filter="Lerp"` vs `add_node type="Lerp"`** — list finds it, add doesn't. Name resolution inconsistent between the two endpoints.
11. **`get_node_settings` on a fresh `CustomHLSL`** — throws `The variable m_ShaderFile of CustomHLSL has not been assigned`. Node is in a valid state, settings query should tolerate missing shader file.
12. **`get_connections` action** — returned `count: 0` even when `get_graph_info` shows many data connections on the same asset. Either remove the action or fix.

## Missing capability / workarounds

13. **No Vector3 constructor from 3 scalars.** Had to create a `CustomHLSL` operator `float3(a,b,c)` and call `set_hlsl_code`. An `AppendVector` that grows beyond 2 operands (via `m_Operands`) was opaque — the setting type is `Operand[]` with no documented JSON shape.
14. **No explicit `int → uint` / `int → float` cast operator.** Current workaround is `VFXInlineOperator(uint)` acting as a pass‑through, which relies on `connect_nodes` doing implicit widening (the very thing the skill says is unsupported). It happens to work for int→uint and int→float but the behavior isn't documented.
15. **`VFXInlineOperator` created with no `m_Type`** has zero in/out slots until `m_Type` is set via `set_node_setting`. Add a required `m_Type` param on creation or default to `float`.
16. **`set_node_setting` readback of `SerializableType` fields** returns the opaque string `UnityEditor.VFX.SerializableType` instead of the actual type name. No way to verify the setter took from `get_node_settings` alone; have to re-query `get_graph_info` and look at slot types.
17. **`link_gpu_event`** succeeds via `data_slot_fallback` path — suggests the primary flow-port linking path isn't working. Worked end‑to‑end but worth investigating.
18. **`set_hlsl_code`** works, but the setting lives under `m_HLSLCode` — add an alias `hlslCode` so it's discoverable via `set_node_setting`.
19. **Runtime default for Color / HDR Color property type.** I had to add `Vector3` (not `Color`) to avoid the Vector4/JArray bug and to fit the Vector3 color attribute without a Swizzle node. Consider a first‑class `Color` property type that round‑trips JSON arrays cleanly.
20. **No block for `KillIf condition`.** Had to wire `Condition(Size >= 0)` → `SetAttribute alive`. A `KillWhen(bool)` helper block would shrink this.
21. **No `setup_smoke_trail` / `gpu_event_child` high‑level recipe** beyond `gpu_event_chain`. The current recipe doesn't wire size/alpha‑over‑life or inherit‑from‑source. An opinionated recipe would save ~20 calls.
22. **No way to set `spawnIndex` source attribute as `SampleBuffer.index`** without first adding a `VFXAttributeParameter` node + wiring. A `add_sample_buffer_indexed_by(attribute)` shortcut would help.

## ID instability

23. **Block IDs reassigned between calls** — e.g. a `SetCustomAttribute` block returned id `-97282` from a batch response, but a few calls later `get_graph_info` reported the same block as `-97290`. Documented as possible after domain reload, but happened mid‑session without reloads. Either guarantee stability within a session or return a “valid until” token.
24. **Node position resets** — positions set via `move_node` sometimes revert after a save/compile cycle.

## Misc diagnostics

25. `get_graph_info mode=summary` shows each context as a separate chain entry (`["Spawner(1 blocks)"]`, `["Initialize(2 blocks)"]`…) even when they are flow‑linked into one system. Should collapse into a single chain.
26. `read_vfx_console` surfaces legitimate internal warnings (`Remove N linked slot(s)…`) that indicate silent connection loss — batch operations should ideally harvest these and include them in their response.
27. `get_compilation_status` returns 0 errors even when connections were dropped at save time, because the dropped connections leave the graph in a technically‑valid (but wrong) state. A separate `get_warnings` would help.

## Works well (no fixes needed, just noting)

- `batch` with symbolic refs (`$name`)
- `add_attribute_block source: "Source"` (inherit-from-source)
- `set_data_settings boundsMode: "Manual"` + `set_bounds`
- `configure_output` with nested settings dict
- `set_capacity` via `set_capacity` action
- `add_property` for `int`, `float`, `GraphicsBuffer`, `Texture2D` (though defaults for Texture2D must be set externally — see #5)
- `save_graph` + `compile_graph` + `get_compilation_status` cycle
