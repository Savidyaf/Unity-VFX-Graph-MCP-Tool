# Spec Review: VFX Graph MCP Redesign (2026-04-07)

**Reviewed:** `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`
**Cross-refs:** `docs/vfx_graph_mcp_tool_gaps.md`, `docs/MCP-VFX-Tool-Limitations.md`
**Verified against:** embedded `com.unity.visualeffectgraph` 17.4.0 source, current addon code
**Purpose:** Findings for the agent to resolve before implementation planning

---

## BLOCKING — Must fix before implementation

### B1. `VFXModel.label` does not exist — identity design breaks

The spec's identity layer (§ Identity model, Flow 4) writes the minted token into `m.label`:

> `m.label = token + "|" + m.GetType().Name`

**Actual Unity API:** `label` is a `[SerializeField] private string m_Label` property declared only on `VFXContext` (`VFXContext.cs:69-78`). It is **not** on `VFXModel`, `VFXOperator`, `VFXBlock`, `VFXParameter`, or any other node type.

- `VFXBlock` (`VFXBlock.cs`) — no label/title field.
- `VFXOperator` inherits `VFXSlotContainerModel<VFXModel, VFXModel>` → `VFXModel` — no label.
- `VFXParameter` has `m_ExposedName` (for blackboard), but no general label.
- `VFXModel` base has only `virtual string name => string.Empty` (read-only, no setter).

**Impact:** The entire mint/resolve/recover pipeline assumes a writable label survives serialization on all node types. Without it, token persistence across save/reimport/restart breaks for operators, blocks, and parameters.

**Fix options (recommend A or hybrid A+B):**

A. Use `ScriptableObject.name` (`VFXObject : ScriptableObject`) — survives serialization, writable on all models. Verify it isn't stripped on save (it persists in `.vfx` YAML as the `m_Name` field on every serialized `MonoBehaviour`/`ScriptableObject`).

B. Use a serialized custom field via `SerializedObject` injection — fragile, fights Unity's serialization.

C. Use `VFXContext.label` for contexts, `VFXParameter.exposedName` for parameters, and fall back to `(type, index-within-parent)` tuple for operators/blocks. The sidecar covers cross-session; this tuple covers in-session only.

**Spec change needed:** Update Flow 4, the identity model section, and any mention of "writes label into model" to reference the actual storage mechanism.

### B2. `InternalsVisibleTo` shim is incomplete

`VfxAssemblyInternals.cs` currently contains only:

```csharp
[assembly: InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor.Tests")]
```

The spec references this file as the `[InternalsVisibleTo]` shim used by "generator + kernel" to access VFX Graph internals. But the attribute lives in the **addon** assembly — it grants the addon's test assembly access to the addon's internals. It does **not** grant the addon access to `UnityEditor.VFX` internals.

For the generator to reflect into `VFXModel`, `VFXContext`, `VFXBlock`, `VFXSlot`, `VFXLibrary` (all `internal`), the `[InternalsVisibleTo]` must live in the **VFX Graph package's** assembly definition (the embedded `com.unity.visualeffectgraph` editor asmdef). Since you have the package embedded, this is viable — but the spec should explicitly state that a new `[InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")]` line is added to the VFX Graph assembly, not just to the addon.

The current runtime code works via reflection (bypassing `internal`). The spec's goal of eliminating runtime reflection makes the `InternalsVisibleTo` direction mandatory, but the impl detail is wrong.

**Spec change needed:** Clarify that `VfxAssemblyInternals` refers to a shim in the embedded VFX Graph assembly, not the addon assembly. Document the exact asmdef file and the `InternalsVisibleTo` line being added.

### B3. `VFXViewController.AddVFXModel` signature vs spec's `VfxNodeOps.AddNode`

The spec's Flow 2 step 4 calls:

> `VfxNodeOps.AddNode(graph, type, ctx) → new VFXModel m`

The actual Unity API is:

```csharp
// VFXViewController.cs:1204
public void AddVFXModel(Vector2 pos, VFXModel model)
```

This takes an already-instantiated `VFXModel` + a position. The model must be created first via `VFXModelDescriptor<T>.CreateInstance()` (from `VFXLibrary`), not via `AddVFXModel`. The controller doesn't instantiate — it parents and positions.

For blocks specifically, the API is `VFXContext.AddChild(block)` / `VFXModel.AddChild`, not the controller method.

**Spec change needed:** Flow 2 step 4 should reflect the two-step pattern: (1) `VFXLibrary.GetXxx() → descriptor.CreateInstance()`, (2) `controller.AddVFXModel(pos, model)` or `context.AddChild(block)`. `VfxNodeOps` must internally wrap both steps.

---

## HIGH — Significant risk if not addressed

### H1. Generator's reflection walker scope: `VFXLibrary` is the real source of truth, not raw assembly types

The spec says `VfxReflectionWalker` walks `UnityEditor.VFX.*` assemblies. But Unity's own node-creation UI uses `VFXLibrary` (a static class at `Editor/Core/VFXLibrary.cs`) which maintains a curated, filtered list via `VFXModelDescriptor<T>`. Many types in the assembly are abstract, deprecated, or internal-only — `VFXLibrary` filters these.

**Risk:** Walking the raw assembly produces a superset that includes non-instantiable types. The generator would emit wrappers for abstract operators, deprecated blocks, and internal test types — all of which fail at `CreateInstance()`.

**Recommendation:** Walk `VFXLibrary.GetOperators()`, `VFXLibrary.GetBlocks()`, `VFXLibrary.GetContexts()` etc. as the primary source of truth. These return `IVFXModelDescriptor` instances with:
- `modelType` (the concrete `Type`)
- `model` (a template instance for slot/setting introspection)
- `name`, `category`, `variant` info

This also gives you the same disambiguation Unity uses in its add-node UI, reducing Quirks.yaml surface area.

### H2. VFXSlot hierarchy is recursive — coercer generation is more complex than one-per-type

`VFXSlot` is `VFXModel<VFXSlot, VFXSlot>` — slots can have child slots (e.g., `VFXSlotFloat3` has children `.x`, `.y`, `.z`). Setting a compound slot value from JSON requires either:
- Setting the parent slot (if the type round-trips as a whole), or
- Walking child slots individually

The spec says "Generator emits a typed `VfxCoercer` per `VFXSlot` type." This is correct at the leaf level, but compound types like `Vector3`, `Color`, `Transform`, `Sphere` (custom VFX struct types) need a compound coercer that:
1. Accepts a JSON object/array
2. Decomposes it into child-slot assignments
3. Handles Unity's auto-promotion (e.g., connecting a `float` to a `Vector3` slot auto-broadcasts)

**Recommendation:** The `CatalogIR` should model the slot tree shape per type, and emitters should generate compound coercers for non-leaf slot types. This directly addresses gaps #5 (Texture2D), #6 (Vector3 from JArray), and #19 (Color).

### H3. `AssetDatabase.SaveAssetIfDirty` and the YAML verifier timing

The spec uses `AssetDatabase.SaveAssetIfDirty(graph)` before YAML verification. In Unity's VFX Graph pipeline, saving triggers recompilation. The verifier reads the `.vfx` file immediately after save. But:

1. VFX Graph compilation is asynchronous — `VisualEffectResource.CompileOrRuntimeError()` and the shader compilation may not be finished when the YAML read happens.
2. The `.vfx` YAML on disk is the serialized model, not the compiled output — so the verifier reads pre-compilation state, which is correct for structural verification.
3. However, `Remove linked slot(s)` warnings (gap #4) happen **during** serialization, not after. Unity logs them in the console but has already modified the YAML by the time it's on disk.

**Implication:** The verifier can detect *missing* connections (intent says connect, YAML says no connection) but cannot detect *why* they were dropped. The spec's claim that the verifier "turns gap #4 into a structural signal" is mostly correct, but the verifier can only say "connection dropped" — it can't say "dropped because slot types were incompatible after save-time resolution."

**Recommendation:** The verifier should also harvest `VfxConsoleReader` lines between pre-save and post-save to correlate console warnings (`Remove N linked slot(s)...`) with specific dropped connections. Mention this correlation in the verifier spec.

### H4. Batch symbolic refs (`$name`) — scope resolution ambiguity

Flow 3 shows `as: "n_lerp"` creating a symbolic ref resolvable in the same batch. The spec says:

> Symbolic refs ($name) resolve against: 1. Tokens minted earlier in the same batch 2. Then VfxIdentity for pre-existing tokens

But the `$` prefix is also used for pre-existing context tokens (e.g., `$ctx_init` in Flow 2). There's no syntactic distinction between "batch-local alias" and "pre-existing token."

**Risk:** If a batch-local alias collides with a pre-existing token, the resolution order silently shadows the pre-existing one. Agents re-using common aliases across batches could hit non-obvious bugs.

**Recommendation:** Either (a) make batch-local aliases use a different prefix (e.g., `@n_lerp`), or (b) reject batch-local aliases that collide with pre-existing tokens (fail-fast). Document the choice.

---

## MEDIUM — Design improvements

### M1. `vfx_graph.discard_changes` — what is the undo mechanism?

The spec says batch abort leaves the "in-memory graph dirty" and the agent can call `vfx_graph.discard_changes` to revert. The actual mechanism in Unity VFX Graph is:
- `VFXGraphUndoStack` (at `VFXViewControllerUndo.cs`) manages undo states
- Discarding changes requires either `Undo.PerformUndo()` (risky — affects global undo stack) or re-loading the graph from the last saved `.vfx` file

The spec doesn't specify which path `discard_changes` uses. Re-loading from disk is safer and more predictable. Clarify.

### M2. `vfx_node` tool combines too many concerns

`vfx_node` handles: `add`, `remove`, `move`, `duplicate`, `connect`, `disconnect`, `get/set_setting`, `get/set_property`. That's 10 actions spanning lifecycle, topology, and configuration.

Connection operations (`connect`, `disconnect`) are conceptually different from node CRUD — they operate on pairs of nodes/slots and have their own error modes (slot type mismatch, implicit conversion, connection dropping). Consider either:
- A separate `vfx_connection` tool, or
- Documenting why they belong in `vfx_node` (agent convenience? reduced tool count?)

### M3. Token hash collision probability should be documented

5 hex chars = 20 bits = ~1M unique. The spec says "plenty for any real graph." But the hash inputs are `(graphPath, typeName, mintOrder)` — if the hash function has weak mixing, common type names could cluster. Document the hash function choice (SHA-256 truncated? CRC32? FNV?) and confirm birthday-bound collision probability is acceptable given the token space.

### M4. Sidecar `Library/VfxMcpIdentity.json` — domain reload timing

The sidecar is written to `Library/`. Unity's domain reload clears managed state. The spec says the sidecar provides cross-session stability. But:
- `Library/` contents survive domain reload (it's git-ignored but not cleared by Unity on reimport of scripts)
- The sidecar must be written *before* domain reload, which means either `AssemblyReloadEvents.beforeAssemblyReload` or flushing on every mint

If tokens are only flushed at transaction commit time, a crash between mint and commit loses the sidecar entry. The recovery path handles this, but the spec should state the flush policy explicitly.

### M5. Testing: missing negative test for generator with `internal` visibility

Test category 1 (codegen determinism) and 2 (self-compile) assume the generator can access all VFX internals. If B2 (InternalsVisibleTo) is incorrectly set up, the generator silently falls back to `public`-only members, producing an incomplete catalog that still compiles. Add a test that asserts the generated catalog count matches `VFXLibrary.GetOperators().Count()` + `GetBlocks().Count()` + etc.

---

## LOW — Polish and edge cases

### L1. `vfx_recipe` scaffold — consider dropping from phase 4

The scaffold registers a tool that returns `[]`. MCP tools with zero useful actions create agent confusion (the agent discovers `vfx_recipe.list`, calls it, gets nothing, and may retry or plan around it). Either:
- Ship the scaffold with a clear tool description ("Recipes not yet available; deferred to next version."), or
- Don't register the tool until there's at least one recipe

### L2. `set_space` action — SpaceableType complexity

`vfx_graph.set_space` interacts with `VFXModel.space` which is per-slot, not per-graph. The `SpaceableType` enum and `VFXSpace` handling varies by context. Ensure the spec's graph-level `set_space` is actually what agents need, vs. per-slot space override.

### L3. Compilation status after batch

The spec's batch flow doesn't include a compilation step. After `VfxTransaction.Commit()` → save → verify, the graph may need recompilation. Should `vfx_batch` auto-compile and return `compilation_status`? Or leave it to the agent? Document the expectation.

### L4. Phase 6 cleanup file list

The spec lists 18 files to delete. Cross-check: `VfxGraphEditActions.cs` and `VfxGraphEdit.cs` are referenced in the delete list via the old Vfx* naming, but the actual filenames in the codebase are:
- `VfxGraphEdit.cs` (exists)
- `VfxGraphEditActions.cs` (verify — could be `VfxGraphActionRouter.cs` based on tree output)

The agent doing the cleanup should `ls` the directory first and reconcile actual filenames vs the spec's list.

---

## Summary: action items by priority

| Priority | ID | One-line fix |
|---|---|---|
| BLOCKING | B1 | Replace `m.label` with a storage mechanism that exists on all VFXModel subtypes |
| BLOCKING | B2 | Put `InternalsVisibleTo` in the VFX Graph asmdef, not the addon asmdef |
| BLOCKING | B3 | Correct Flow 2 to show `VFXLibrary.Get*() → CreateInstance() → AddVFXModel(pos, model)` |
| HIGH | H1 | Use `VFXLibrary.Get*()` as walker source, not raw assembly reflection |
| HIGH | H2 | Model slot tree shape in CatalogIR; generate compound coercers |
| HIGH | H3 | Correlate verifier YAML diffs with console warnings between pre/post save |
| HIGH | H4 | Disambiguate batch-local aliases from pre-existing token refs |
| MEDIUM | M1 | Specify `discard_changes` mechanism (reload-from-disk vs undo) |
| MEDIUM | M2 | Justify or split `connect`/`disconnect` out of `vfx_node` |
| MEDIUM | M3 | Document hash function and collision bound |
| MEDIUM | M4 | State sidecar flush policy (per-mint vs per-commit) |
| MEDIUM | M5 | Add catalog-count-match test |
| LOW | L1 | Don't register empty recipe tool |
| LOW | L2 | Clarify `set_space` scope (graph vs slot) |
| LOW | L3 | Document post-batch compilation expectation |
| LOW | L4 | Verify cleanup file list against actual filenames |
