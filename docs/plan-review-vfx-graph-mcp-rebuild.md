# Plan Review: VFX Graph MCP v0.3.0 Rebuild

**Reviewed:** `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md`
**Cross-refs:** spec, spec-review, gaps doc, embedded VFX Graph 17.4.0 source
**Purpose:** Agent-consumable findings — fix before/during execution

---

## BLOCKING — Compilation or logic failures if not fixed

### P-B1. `VFXGraph.CompileAndUpdateAsset` takes an asset parameter

Task 3C-2 `VfxCompileGate` calls:
```csharp
graph.CompileAndUpdateAsset();  // line ~2435 of plan
```

Actual signature in `VFXGraph.cs:1464`:
```csharp
internal UnityObject[] CompileAndUpdateAsset(VisualEffectAsset asset)
```

It requires the `VisualEffectAsset` argument. The plan loads `asset` on line 2427 but doesn't pass it.

**Fix:** `graph.CompileAndUpdateAsset(asset);`

### P-B2. `VFXSetting.value` type vs plan's `setting.value?.GetType().FullName`

Task 3A-1 (walker settings capture) uses:
```csharp
TypeFQN = setting.value?.GetType().FullName ?? "System.Object"
```

`VFXSetting.value` calls `field.GetValue(instance)` on a template instance. For `SerializableType` fields (like `VFXInlineOperator.m_Type`), this returns a `SerializableType` wrapper object, not a `System.Type`. The walker would record `TypeFQN = "UnityEditor.VFX.SerializableType"` for all such fields instead of the underlying type.

**Fix:** Add a `SerializableType` unwrap step — check if `setting.field.FieldType.Name == "SerializableType"` and extract the inner type string. This is the exact same issue as gap #16.

### P-B3. `VfxSlotTreeBuilder` uses `GetFields(Public | Instance)` — Unity struct fields are not all public

`Vector3` fields (`x`, `y`, `z`) are public, but `Color` fields (`r`, `g`, `b`, `a`) are also public, so Color works. However, `VFXSlot` child names come from `VFXProperty` decomposition, not from the struct's raw field layout. For example, `Transform` decomposes into `position`, `angles`, `scale` in VFX — not into `Matrix4x4`'s raw fields.

The plan's `VfxSlotTreeBuilder` walks `type.GetFields(Instance | Public)` on the C# struct. VFX Graph's actual slot tree is built by `VFXSlot.CreateSub()` using `VFXLibrary.GetSlot(property.type)` which returns a custom decomposition.

**Fix:** `VfxSlotTreeBuilder` should walk `VFXSlot` child hierarchies from the template instance's actual input/output slots (already available via `container.inputSlots`), not from System.Reflection on the value type. The slot tree shape must come from VFX's own decomposition.

### P-B4. `VfxNodeOps.AddOperator` adds to `graph` directly — wrong parent

Task 4B-1 does:
```csharp
graph.AddChild(op);
```

`VFXGraph.children` are `VFXModel` children, but the correct way to add a top-level operator to a VFX Graph is via the `VFXViewController`:
```csharp
controller.AddVFXModel(pos, model);
```

`AddChild` directly on the graph works for some model types, but bypasses controller bookkeeping (undo registration, notification, slot initialization via `OnEnable`). The controller's `AddVFXModel` is the safe path used by Unity's own UI.

**Fix:** Obtain the `VFXViewController` from `VFXViewController.GetController(resource)` and use `controller.AddVFXModel(pos, op)`. Document the helper pattern in `VfxNodeOps`.

### P-B5. Subgraph type FQNs in walker split may be wrong

Task 8 splits subgraphs by exact FQN match:
```csharp
if (op.TypeFQN == "UnityEditor.VFX.Operator.VFXSubgraphOperator")
```

The actual class at `VFXSubgraphOperator.cs:68`:
```csharp
namespace UnityEditor.VFX
{
    class VFXSubgraphOperator : VFXOperator
```

The FQN is `UnityEditor.VFX.VFXSubgraphOperator`, not `UnityEditor.VFX.Operator.VFXSubgraphOperator`. Similarly, `VFXSubgraphBlock` is in namespace `UnityEditor.VFX`, not `UnityEditor.VFX.Block`.

The plan acknowledges this ("the exact subgraph FQNs may differ slightly") but the code as written will silently fail to split — all subgraphs stay in the main lists.

**Fix:** Use `modelType.IsSubclassOf(typeof(VFXSubgraphOperator))` or check `VFXSubgraph.IsSubgraphModel(template)` instead of string comparison.

---

## HIGH — Correctness risk during execution

### P-H1. `VfxConsoleReader.GetEntryText` is a stub placeholder

Task 3C-2b leaves `GetEntryText` returning `$"[console line {index}]"`. The `VfxConsoleCorrelator` (task 3C-3) pattern-matches against real console text (`"Remove N linked slot(s)..."`). The correlator will never match anything until the stub is filled.

The plan says "real impl wired in phase 3C" but no task explicitly fills the stub. The implementer must wire the actual `LogEntries.GetEntryInternal` reflection call.

**Recommendation:** Add an explicit step in task 3C-2b to implement `GetEntryText` using `LogEntries.GetEntryInternal(int row, LogEntry outputEntry)` — the Unity 6.x signature uses a `LogEntry` struct output parameter. The existing old `VfxConsoleReader.cs` in `Tools/Vfx/` likely has a working implementation to reference.

### P-H2. `VFXSetting.name` returns field names (e.g., `m_Type`), not friendly names

The walker records setting names from `setting.name`, which returns `field.Name` per `VFXSettingAttribute.cs:59-63`. These are C# field names like `m_Type`, `m_HLSLCode`, `m_Operands`. The plan's `Quirks.yaml` (task 5-1) adds aliases (`hlslCode: m_HLSLCode`), but the catalog will list `m_Type` as the canonical name. The agent-facing schema should present either stripped names or the alias.

**Recommendation:** The `CatalogEmitter` should strip the `m_` prefix for the short-name dictionary key, keeping the full `m_Type` as the internal key. The `OverridesEmitter` can layer aliases. This directly addresses gap #18.

### P-H3. `VfxIdentity.Resolve` recovery path is incomplete

The recovery path in task 3B-3 adds ALL candidates to `matches` without any filtering:
```csharp
foreach (var candidate in WalkAllModels(graph))
{
    matches.Add(candidate); // loose criteria — adds everything!
}
```

This always yields `matches.Count > 1`, so recovery always throws `node_lost`. The comment says "loose criteria defined in the spec; simplified here" but the implementer must actually filter by `(type, parentFP)`.

**Fix:** The recovery loop must check `candidate.GetType().FullName == expectedTypeFqn` at minimum. The expected type FQN needs to be stored in the sidecar alongside the fingerprint.

### P-H4. Phase 7 `VfxConsoleReader.cs` move conflicts with phase 3 new file

Phase 3 task 3C-2b creates a NEW `Kernel/VfxConsoleReader.cs`. Phase 7 task 7-1 runs `git mv Tools/Vfx/VfxConsoleReader.cs Kernel/VfxConsoleReader.cs`, which would overwrite the phase-3 file.

The plan notes this conflict ("This new file is authored fresh... Phase 7 deletes the old") but the cleanup step still uses `git mv`. This will fail because the destination already exists.

**Fix:** Phase 7 should `git rm Editor/Tools/Vfx/VfxConsoleReader.cs` (delete old), not `git mv` it. The new file already exists in `Kernel/`.

### P-H5. `VfxKernelContainer` uses static singleton — no test isolation

`VfxKernelContainer` initializes all kernel services as `static` properties:
```csharp
public static IVfxIdentity Identity { get; } = new VfxIdentity(new VfxIdentitySidecar());
```

Tests that run in parallel or sequence share the same sidecar file (`Library/VfxMcpIdentity.json`). Identity tests create tokens that leak between test runs.

**Fix:** Make `VfxKernelContainer` accept an override path for the sidecar in test mode, or use `[SetUp]/[TearDown]` in identity tests to use a unique temp path. The sidecar constructor already accepts a path parameter — use it.

---

## MEDIUM — Implementer should address during execution

### P-M1. `CatalogEmitter.ShortNameToFqn` loses entries on name collision

The dictionary uses `ShortName` as key. If two types share a short name (e.g., `Lerp` from both operators and UIElements), the second `Add` call throws `ArgumentException` at codegen time.

**Fix:** The emitter should detect collisions and either skip the non-VFX one (via `Quirks.yaml`) or emit both with FQN keys. Add a collision-detection pass before the dictionary emit loop.

### P-M2. `CoercersEmitter` assumes all compound children are `float`

The comment at line 1566 acknowledges this. For `Color` (r,g,b,a) this works, but `Transform` children are `Vector3` + `Vector3` + `Vector3`. `Sphere` has center (`Vector3`) + radius (`float`). The emitter unconditionally calls `CoerceToFloat(arr[i])` for every child.

**Fix:** Use the child's `ChildTypeFQN` to dispatch to the appropriate coercer method. E.g., `CoerceToVector3(arr[i])` for a `Vector3` child.

### P-M3. `VisualEffectSubgraphBlock` uses extension `.vfxblock`, not `.vfxop`

Task 4C-1 maps `.vfxop` to `VFXSubgraphContext`, but the actual extensions from VFX Graph source:
- `.vfx` → `VisualEffectAsset`
- `.vfxblock` → `VisualEffectSubgraphBlock`
- `.vfxoperator` → `VisualEffectSubgraphOperator`

There is no `.vfxop` extension in the VFX Graph 17.4.0 source. The plan's `VfxAssetTool.Create` at line 3078 also uses `.vfxop`.

**Fix:** Replace `.vfxop` with `.vfxblock` throughout the plan.

### P-M4. `VfxDiagTool.ReadConsole` snapshots and reads in the same call

Task 4A-3 `ReadConsole`:
```csharp
var lines = VfxConsoleReader.GetLinesSince(VfxConsoleReader.GetHighWaterMark());
```

This snapshots the high-water mark and immediately reads — always returning 0 lines (the mark IS the current position). The spec intends the mark to persist across calls so subsequent reads return only NEW lines.

**Fix:** Store the high-water mark as state (static field or in `VfxKernelContainer`) and only update it after a successful read. First call returns all lines; subsequent calls return new ones.

### P-M5. `VfxAssetTool.Create` for `.vfx` — wrong factory

Line 3077:
```csharp
ScriptableObject.CreateInstance(typeof(UnityEngine.VFX.VisualEffectAsset))
```

`VisualEffectAsset` is a native Unity object — `ScriptableObject.CreateInstance` won't work (it's not a `ScriptableObject`). The correct way to create a `.vfx` asset is:
```csharp
var resource = VisualEffectResource.CreateNewResource(path);
```
Or create via `AssetDatabase.CopyAsset` from a template. Check the VFX Graph's own `VFXTemplateWindow.cs` for the canonical creation path.

### P-M6. No `.meta` files for new directories

Unity requires `.meta` files for every directory and file. The plan creates `Editor/Generation/`, `Editor/Generated/`, `Editor/Kernel/`, `Editor/Tools/` directories. Unity auto-generates `.meta` files when `AssetDatabase.Refresh()` runs, but git-committed files without `.meta` companions will cause Unity import warnings.

**Recommendation:** After each phase, run `AssetDatabase.Refresh()` before `git add`, or add `.meta` files to the commit. The plan's `VfxCatalogGenerator.Regenerate()` already calls `AssetDatabase.Refresh()` for generated files, but new hand-authored files need the same treatment.

---

## LOW — Polish

### P-L1. `VfxSubgraphTool` `.vfxop` vs `.vfxblock` extension mismatch (task 4C-1)

`AddSubgraphRef` maps `.vfxop` to `VFXSubgraphContext`, but `VFXSubgraphContext` is for subgraph contexts embedded in a parent graph — its asset extension is determined by the parent, not by `.vfxop`. The mapping should be `.vfxblock` → `VFXSubgraphBlock`, `.vfxoperator` → `VFXSubgraphOperator`. A `.vfxblock` added to a graph creates a `VFXSubgraphBlock`, not a `VFXSubgraphContext`.

### P-L2. `VfxCatalogCompletenessTests` subtracts 1 for subgraph split — fragile

Task 3A-8:
```csharp
VFXLibrary.GetOperators().Count() - 1  // minus VFXSubgraphOperator
```

If VFXLibrary returns multiple subgraph-typed operators, or zero, the test breaks. Use the walker's actual subgraph list count instead of a hardcoded `-1`.

### P-L3. `VfxYamlVerifier` token-in-YAML assumption conflicts with sidecar identity

The initial verifier (task 3B-4) checks if `ExpectedToken` appears in the YAML. Since identity is sidecar-only (tokens are never written to the asset), tokens will NEVER appear in YAML. The plan acknowledges this but the initial test will pass for the wrong reason (it falls through to the FQN check). Clarify in the test comment.

### P-L4. Missing `VfxErrorEnvelope.cs` in Kernel file list

The plan's file structure lists `VfxErrorEnvelope.cs` in Kernel, but the error types (`VfxErrorEnvelope`, `VfxException`, etc.) are defined inside `VfxKernelContracts.cs`. Either remove `VfxErrorEnvelope.cs` from the file structure or split the types out.

---

## Summary

| Priority | ID | Fix |
|---|---|---|
| BLOCKING | P-B1 | Pass `asset` arg to `CompileAndUpdateAsset(asset)` |
| BLOCKING | P-B2 | Unwrap `SerializableType` in walker settings capture |
| BLOCKING | P-B3 | Build slot trees from VFXSlot hierarchy, not System.Reflection |
| BLOCKING | P-B4 | Use `VFXViewController.AddVFXModel` not `graph.AddChild` |
| BLOCKING | P-B5 | Fix subgraph FQNs: `UnityEditor.VFX.VFXSubgraphOperator` etc. |
| HIGH | P-H1 | Implement `GetEntryText` — stub returns dummy text |
| HIGH | P-H2 | Strip `m_` prefix from setting names in catalog |
| HIGH | P-H3 | Add type-FQN filter to identity recovery path |
| HIGH | P-H4 | Phase 7: `git rm` old file, don't `git mv` over new |
| HIGH | P-H5 | Inject sidecar path for test isolation |
| MEDIUM | P-M1 | Detect name collisions in `ShortNameToFqn` emit |
| MEDIUM | P-M2 | Dispatch compound coercer children by type, not all-float |
| MEDIUM | P-M3 | Replace `.vfxop` with `.vfxblock` extension |
| MEDIUM | P-M4 | Persist console high-water mark across calls |
| MEDIUM | P-M5 | Use `VisualEffectResource.CreateNewResource` for `.vfx` |
| MEDIUM | P-M6 | Ensure `.meta` files for new directories |
| LOW | P-L1 | Fix subgraph extension → model type mapping |
| LOW | P-L2 | Don't hardcode `-1` in completeness test |
| LOW | P-L3 | Clarify token-in-YAML impossibility in verifier test |
| LOW | P-L4 | Reconcile `VfxErrorEnvelope.cs` in file list |
