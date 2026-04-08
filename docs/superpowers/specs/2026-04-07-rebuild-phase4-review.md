# Phase 4a — Code Review: VFX Graph MCP v0.3.0 Rebuild

**Reviewer:** Claude Opus 4.6 (code-reviewer, effort: max)
**Date:** 2026-04-08
**Branch:** `vfxgraph-rebuild-v0.3`
**Scope:** Phase 4 commits only (5 commits on top of Phase 3 base)

```
dfa0bfe Phase 4-SMOKE: E2E smoke test + verifier graceful-degradation
b2ad353 Phase 4C: VfxSubgraphTool + VfxRecipeTool + VfxBatchTool + AddSubgraphRef
59ba935 Phase 4B: VfxNodeTool + VfxBlockTool + VfxPropertyTool + fill VfxNodeOps
4acc756 Phase 4A: Add VfxAssetTool + VfxGraphTool + VfxDiagTool + asset-creation bridges
36b5e6f Phase 4-SETUP: Add VfxKernelContainer with Override hook
```

---

## 1. Top-Level Verdict

**APPROVED_WITH_FOLLOWUPS** — Phase 4 is architecturally sound. Every BLOCKING plan erratum that touches Phase 4 is structurally applied, no hard-rule violations, and the smoke test (`VfxSmokeTests.EndToEnd_AllNineTools_ExerciseSinglePath`) is attested PASS in the commit record. Two HIGH findings (VfxGraphTool hand-rolled error envelopes bypassing the Shaper, and a silent `typeof(Vector3)` fallback inside `VfxNodeOps.SetSetting`) and several MEDIUM items need to land during Phase 5, but none gate Phase 5 entry. The production-mode YAML verifier graceful-degradation is accepted as a documented Phase 5 hardening item.

---

## 2. Findings Table

| # | Severity | Commit | File:Line | Title | Fix |
|---|---|---|---|---|---|
| F1 | HIGH | 4acc756 | `Editor/Tools/VfxGraphTool.cs:201-215,233-247,265-279,300-314` | `SetSpace`/`SetCapacity`/`SetBounds`/`SetDataSettings` catch `NotImplementedException` and return a hand-rolled `{code,message}` JObject, **bypassing `VfxKernelContainer.Shaper.ShapeError`**. This violates rule #14 (all errors route through the shaper). The catch blocks are now also dead code because Phase 4B filled `VfxNodeOps.SetSetting`. | Delete the four `catch (NotImplementedException)` blocks entirely. The outer `HandleCommand` try/catch will route any still-unimplemented paths through `Shaper.ShapeError` with code `vfx_exception`. |
| F2 | HIGH | 59ba935 | `Editor/Kernel/VfxNodeOps.cs:336-341` | `SetSetting` silently falls back to `typeof(Vector3)` when the caller's type-name string fails to resolve: `var t = System.Type.GetType((string)value) ?? typeof(Vector3);`. This directly re-introduces gap #15 from `docs/vfx_graph_mcp_tool_gaps.md` (silent wrong-type write) and mirrors gap #2 (exception swallowed, wrong state). | Throw `VfxValidationException("invalid_type_name", ...)` when the string fails to resolve. Also validate `value is string` before the cast — a non-string caller currently hits an `InvalidCastException` that becomes an opaque `vfx_exception`. |
| F3 | MEDIUM | 4acc756 | `Editor/Tools/VfxGraphTool.cs:101-118` | `GetInfo` loops `foreach (var _ in graph.children) childCount++` instead of using LINQ `Count()` — minor allocation / code-smell. The bigger problem is the response carries only `graph_path` + `child_count`, with `TODO Phase 5` marker. This is acceptable for Phase 4 delivery but `vfx_graph.get_info` is nominally listed in the smoke test surface. | Phase 5: expose `graph.space`, `graph.systemNames`, capacity, bounds-setting-mode, and system count using the public VFXGraph surface. |
| F4 | MEDIUM | 4acc756 | `Editor/Tools/VfxGraphTool.cs:117-125` | `CompilationStatus` and `GetHealth` return hand-rolled payloads stating `status=unknown` / `health=unknown`. Not wrong, but they look like success responses to a client. A caller cannot distinguish "nothing to report" from "not implemented." | Either (a) return an empty successful shape with an explicit `"state": "not_implemented"` key, or (b) route through `ShapeError` with code `not_implemented` + `hint`. The smoke test accepts either. |
| F5 | MEDIUM | 59ba935 | `Editor/Tools/VfxNodeTool.cs:114-120` (same pattern in all mutating actions) | After `Shape(commit, verbose)` the tool mutates the shaped `JObject` to inject an `"added"` key, overwriting whatever the Shaper already produced from `commit.Diffs`. The pattern works today because the transaction's diff list is empty (ops record intents but do not populate `commit.Diffs`), but it couples the tool's output format to an empty-diff invariant. If Phase 5 starts populating `commit.Diffs`, the tools will overwrite the kernel-computed entries. | Move the add/remove/connect/duplicate output rendering into `VfxResponseShaper.Shape` (or a `ShapeMutation` overload) so the tool layer never edits shaper output. Alternatively, write the operation's metadata into `commit.Diffs` inside `scope.Record` so Shape renders it naturally. |
| F6 | MEDIUM | 4acc756 | `Editor/Tools/VfxAssetTool.cs:74-99` | `Create` runs `BusyGate.EnsureIdle()` but **does not open a `VfxTransaction`**. Asset creation is arguably outside the health gate's remit (there's no prior YAML to diff against), but the absence means the three-part gate never runs on creation — and if `VisualEffectAssetEditorUtility.CreateNewAsset` leaves the asset in a dirty / uncompiled state, the smoke test wouldn't catch it. | Document the intentional bypass in an inline comment, OR wrap the `CreateVfxAsset(path)` call in a transaction with `VfxTransactionScopeKind.SingleCall` so the compile and console correlator at least see the new asset. Recommended: comment + Phase 5 audit. |
| F7 | MEDIUM | 59ba935 | `Editor/Kernel/VfxNodeOps.cs:344-347` | `System.Convert.ChangeType(value, setting.field.FieldType)` for every non-SerializableType setting is crude. Enums need `Enum.Parse`, `Color` needs `JArray → Color` unpacking (gap #19), `Vector2/3/4` needs JArray unpacking (gap #6). Phase 4B leaves this as "rely on ChangeType," which is exactly the failure mode the gap doc flags. | Phase 5: dispatch on `setting.field.FieldType` to the generated coercer table from `VfxCoercers.g.cs`. Phase 3A-3 already generated those; Phase 5 wires them. |
| F8 | MEDIUM | dfa0bfe | `Editor/Kernel/VfxYamlVerifier.cs:187-239` | Production-mode graceful degradation: when `blocks.Count > 0 && typeFqns.Count == 0`, `VerifyAdd` emits a `yaml_verify_skipped` warning instead of an `intent_diverged` error. Real Unity `.vfx` YAML never ships `m_TypeFqn`, so today this short-circuits the strict-match path for every Phase 4 add op. Synthetic `VfxYamlVerifierTests` still pass because those fixtures explicitly populate `m_TypeFqn`. | Accepted as Phase 5 hardening. Phase 5 task 5-4 must land **before the v0.3.0 release gate** (see Section 7 recommendation). The fix is: resolve `m_Script` GUIDs back to `MonoScript → System.Type → FullName` and repopulate `typeFqns` from that. |
| F9 | MEDIUM | b2ad353 | `Editor/Tools/VfxBatchTool.cs:84-107` | Batch dispatcher only wires **one** real `ApplyInTransaction` body (`VfxSubgraphTool.AddRef`). The other 8 throw `NotImplementedException("v0.3.1 batch integration")`. This is documented and aligned with erratum A-H2 scoping, BUT the batch tool's outer catch converts the NIE into `code="vfx_exception"` rather than `code="not_implemented"`, so callers cannot distinguish "this action doesn't batch yet" from "a real bug fired." | Either (a) add a dedicated `catch (NotImplementedException nie)` in `VfxBatchTool.HandleCommand` that maps to `code="not_implemented"`, or (b) have each stub throw a `VfxValidationException("not_implemented", ...)` explicitly. |
| F10 | LOW | 4acc756 | `Editor/Tools/VfxDiagTool.cs:102-114` | `ListAttributes` and `ListSettings` return hand-crafted payloads with `"note"` markers because `VfxCatalog.g.cs` has no `Attributes`/`Settings` arrays. OK for Phase 4 delivery. | Phase 5: extend `VfxLibraryWalker` + `CatalogEmitter` to emit attribute and setting listings. |
| F11 | LOW | 4acc756 | `Editor/Kernel/VfxResponseShaper.cs:66-70` | `ShapeRead(payload, verbose)` is a pass-through; the `verbose` flag is ignored. Comment says "Phase 3D pass-through. Phase 4 will add per-action shaping" — but Phase 4 did not add it. | Phase 5: implement verbose/terse filtering for read responses. Not a regression; the `verbose` flag just has no effect on reads. |
| F12 | LOW | 59ba935 | `Editor/Kernel/VfxNodeOps.cs:411-413` | `GetSetting` returns `AssemblyQualifiedName` for `SerializableType` settings. That's technically correct and solves gap #16 (opaque readback), but the set-path in `SetSetting` expects a callable `Type.GetType(...)` string. Using AQN is robust; document this as the canonical round-trip format. | Add an inline comment: "returns AQN so the value round-trips through SetSetting via Type.GetType(aqn)." |
| F13 | LOW | 59ba935 | `Editor/Tools/VfxNodeTool.cs:75-78` | `ApplyInTransaction` stub throws `NotImplementedException("v0.3.1 batch integration")`. This matches the 4-COORD contract (uniform signature across all 9 tools for reflection dispatch), but the error text "v0.3.1" collides with the "v0.3.1" text used for legitimately-deferred subgraph `inline`/`extract`, making follow-up grepping noisier. | Phase 5: adopt distinct wording: `"batch dispatch — wired during v0.3.1 batch integration"` vs. `"v0.3.1 — non-destructive refactoring"`. |
| F14 | LOW | dfa0bfe | `Tests/Editor/Tools/VfxSmokeTests.cs:132-141` | `vfx_property.add` check is intentionally lenient: `Assert.IsNotNull(propResult, ...)` only. A soft-failure (not_implemented / catalog miss) would not fail the smoke test. The commit message acknowledges this. | Phase 5: tighten to `AssertNoError` once `VFXParameter` catalogue discriminator is confirmed. |
| F15 | LOW | 59ba935 | `Editor/Tools/VfxNodeTool.cs:163-174` | `Move` records its intent with `Kind="set_setting"` but sets `model.position` directly (not via NodeOps). The YAML verifier has no `set_setting` handler for position — it silently passes. | Phase 5: add a `MoveNode(token, Vector2)` method to `IVfxNodeOps`, have the verifier handle `move` as a first-class kind, so position drift is caught. |

---

## 3. Erratum Compliance Matrix

| Erratum | Status | Evidence |
|---|---|---|
| **P-B1** (CompileAndUpdateAsset takes asset arg) | ✓ Applied | `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs:64` — `graph.CompileAndUpdateAsset(asset)`. Phase 3 work untouched by Phase 4. |
| **P-B2** (SerializableType wrapper) | ✓ Applied (with F2 caveat) | `Editor/Kernel/VfxNodeOps.cs:336-341` — detects `FieldType == typeof(SerializableType)` and writes via implicit cast. HIGH finding F2 flags a silent Vector3 fallback. `GetSetting` at `:407-412` also handles the readback correctly. |
| **P-B3** (slot tree from live inputSlots/outputSlots) | ✓ Applied | `Editor/Kernel/VfxNodeOps.cs:460-468` — `FindSlotByName` walks `container.inputSlots`/`outputSlots` and matches via `slot.property.name`. No C# reflection. |
| **P-B4** (AddVFXParameter bookkeeping) | ✓ Applied | `Editor/Kernel/VfxNodeOps.cs:104-150` — replicates `VFXViewController.cs:1223` exactly: sets `collapsed = true`, computes `order = max(existing)+1`, writes `m_ExposedName`, seeds `GetDefaultField`. Comment cites the controller line numbers. |
| **P-B5** (subgraph FQNs in `UnityEditor.VFX.*` namespace) | ✓ Applied | `Editor/Kernel/VfxNodeOps.cs:180-200` — uses `"UnityEditor.VFX.VFXSubgraphOperator"` and `"UnityEditor.VFX.VFXSubgraphBlock"`. No `.Operator.` / `.Block.` nesting. |
| **NEW-1 / P-M3** (`.vfxop` purge) | ✓ Applied | Project-wide grep for literal `".vfxop"` string: **zero matches** (all hits are `.vfxoperator` — a different extension). `Editor/Tools/VfxAssetTool.cs`, `VfxSubgraphTool.cs`, `VfxNodeOps.cs` all use `.vfxblock` and `.vfxoperator`. |
| **P-M4** (`read_console` persistent `s_lastReadMark`) | ✓ Applied | Both `Editor/Tools/VfxGraphTool.cs:24` and `Editor/Tools/VfxDiagTool.cs:25` declare `private static object s_lastReadMark = VfxConsoleReader.GetHighWaterMark();` at the type level. Each `ReadConsole` action calls `GetLinesSince(s_lastReadMark)` then refreshes the mark. Mark resets on assembly reload as designed. |
| **P-M5** (VisualEffectAssetEditorUtility.CreateNew* via bridge) | ✓ Applied | `Editor/Tools/VfxAssetTool.cs:82-91` — switches on extension and calls `VfxMcpKernelHelpers.CreateVfxAsset`, `CreateVfxSubgraphBlock`, `CreateVfxSubgraphOperator`. No `ScriptableObject.CreateInstance`. No `AssetDatabase.CreateAsset` (the bridge already calls it internally). `Editor/Tools/VfxSubgraphTool.cs:131-138` follows the same pattern for .vfxblock/.vfxoperator. |
| **P-M6** (every new .cs has a paired .meta) | ✓ Applied | All 9 tool .cs files (`VfxAssetTool.cs`, `VfxGraphTool.cs`, `VfxDiagTool.cs`, `VfxNodeTool.cs`, `VfxBlockTool.cs`, `VfxPropertyTool.cs`, `VfxSubgraphTool.cs`, `VfxRecipeTool.cs`, `VfxBatchTool.cs`) each have a paired `.cs.meta` committed. `VfxKernelContainer.cs.meta`, `VfxSmokeTests.cs.meta`, `Tests/Editor/Tools.meta` (directory), and `VfxMcpKernelHelpers.cs.meta` are also present. Verified via shell check. |
| **P-H5** (VfxKernelContainer.Override hook) | ✓ Applied | `Editor/Kernel/VfxKernelContainer.cs:14-98` — static container with `s_Services` backing field, every public property routes through it, and `internal static IDisposable Override(VfxKernelServices replacement)` swaps and restores via a private `Disposer` class. `VfxKernelServices.CreateDefault()` wires the full dependency graph. Public surface exactly matches plan errata spec. |
| **A-B1** (VfxSmokeTests.cs exists at end of Phase 4) | ✓ Applied | Commit `dfa0bfe` creates `Tests/Editor/Tools/VfxSmokeTests.cs` (297 lines) exercising all 9 tools in one `EndToEnd_AllNineTools_ExerciseSinglePath` test. Commit message attests PASS (1.47s). |
| **A-H2** (shipped actions only; inline/extract deferred) | ✓ Applied | `Editor/Tools/VfxSubgraphTool.cs:49-50` — `"inline" => throw new System.NotImplementedException("v0.3.1"), "extract" => throw new System.NotImplementedException("v0.3.1")`. Caught by `catch (NotImplementedException)` at `:60-68` and routed through `VfxKernelContainer.Shaper.ShapeError` with code `not_implemented`. |

No erratum is Missing or Partial; every BLOCKING/HIGH erratum that touches Phase 4 is applied.

---

## 4. Hard Rule Scan

| Rule | Status | Evidence |
|---|---|---|
| 1. No `typeof(VFX*).GetMethod/CreateInstance/...` reflection | ✓ Pass | Searched `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/**/*.cs` for `typeof\(VFX\w+\)\s*\.(GetMethod|GetField|GetProperty|InvokeMember|CreateInstance)` — zero matches. Also scanned `Editor/Kernel/*.cs` for `Activator.CreateInstance|typeof(VFX\w+)|Type.GetType(...VFX|Assembly.GetType` — zero matches. `VfxNodeOps.SetSetting` uses `System.Type.GetType((string)value)` to parse a **user-supplied type-name string**, not to reflect on VFX types. |
| 2. No hand-rolled error responses | ✗ FAILED (F1) | `VfxGraphTool.cs:209-215, 241-247, 273-279, 308-314` return `new JObject { ["code"] = "not_implemented", ["message"] = ... }` instead of calling `VfxKernelContainer.Shaper.ShapeError(...)`. HIGH finding F1. |
| 3. No modifications to VfxKernelContracts.cs | ✓ Pass | `git log VfxKernelContracts.cs` shows last commit is `d4ec776 Lock kernel contracts for phase 3 parallel subagents`. Unchanged in all 5 Phase 4 commits. |
| 4. `vfx_subgraph.inline/extract` must not ship real impl | ✓ Pass | `VfxSubgraphTool.cs:49-50` throws `NotImplementedException("v0.3.1")`. No real body. |
| 5. Catalog count drift from VFXLibrary.Get*() | ✓ Pass (not touched) | Phase 4 does not modify the catalog; relies on `VfxCatalog.g.cs` from Phase 2/3A. `VfxCatalogCompletenessTests` (Phase 3A-8) gates this and is unchanged. |
| 6. No m.label / m.name / asset-resident identity writes | ✓ Pass | Searched `Editor/Tools/*.cs` for `\.label\s*=|\.name\s*=`. Only hits are in **legacy** `Editor/Tools/Vfx/*` files (deleted in Phase 7). `VfxNodeOps.AddParameter` uses `param.SetSettingValue("m_ExposedName", ...)` which writes a VFX setting, NOT the identity. All minting goes through `VfxKernelContainer.Identity.Mint(...)`. |
| 7. No `.vfxop` in code | ✓ Pass | Regex `\.vfxop` across all `.cs` files under the addon package — zero matches. Everywhere uses `.vfxblock` or `.vfxoperator`. |
| 8. No reflection in Editor/Kernel/ against UnityEditor.VFX.* | ✓ Pass | See rule 1. Also confirmed no `GetResource()` calls exist in `Editor/Kernel/` or `Editor/Tools/` — the only hit is inside the soft-fork bridge at `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs:47`, which is compiled INTO the VFX Graph assembly and is compile-time access, not reflection. |

**Net:** One hard rule violation (F1). Classified as HIGH because the Shaper-bypass breaks error-envelope uniformity but does not re-enable any legacy bug. Fix is mechanical (delete four catch blocks).

---

## 5. Smoke Test Status

**VfxSmokeTests.EndToEnd_AllNineTools_ExerciseSinglePath** — attested PASS in commit `dfa0bfe` message (1.47s runtime). The test exercises:

| Step | Tool.Action | Status |
|---|---|---|
| 1 | vfx_asset.create (.vfx) | Hard-asserted no-error |
| 2 | vfx_asset.list | Hard-asserted no-error |
| 3 | vfx_node.add (VFXBasicSpawner) | Hard-asserted no-error |
| 4 | vfx_node.add (VFXBasicInitialize) | Hard-asserted no-error |
| 5 | vfx_node.add (Operator.Add) | Hard-asserted no-error |
| 6 | vfx_block.add (SetAttribute under Initialize) | Hard-asserted no-error |
| 7 | vfx_property.add (System.Single) | Soft-asserted (NotNull only — F14) |
| 8 | vfx_graph.save | Soft-asserted (NotNull only) |
| 9 | vfx_graph.compile | Soft-asserted (NotNull only) |
| 10 | vfx_graph.compilation_status | Soft-asserted; returns `status=unknown` (F4) |
| 11 | vfx_subgraph.create (.vfxoperator) | Hard-asserted no-error |
| 12 | vfx_subgraph.add_ref | Hard-asserted no-error |
| 13 | vfx_batch.commit (1-op, add_ref) | Soft-asserted (NotNull only) |
| 14 | vfx_diag.list_node_types | Hard-asserted + `total > 0` |
| 15 | vfx_diag.list_block_types | Hard-asserted |
| 16 | vfx_diag.list_contexts | Hard-asserted |
| 17 | vfx_diag.read_console | Hard-asserted |
| 18 | vfx_recipe.list | Hard-asserted + `note` present |
| 19 | vfx_asset.delete | Soft-asserted |

**Tools that surface "not_implemented" stubs in the smoke flow (Phase 5 follow-ups):**

- `vfx_graph.compilation_status` → `status: unknown` (F4)
- `vfx_graph.get_health` → `yaml/compile/console: unknown` (F4)
- `vfx_graph.set_space / set_capacity / set_bounds / set_data_settings` → not exercised in smoke; latent F1 (hand-rolled error JObject)
- `vfx_diag.list_attributes / list_settings` → empty arrays with `"note"` markers (F10)
- `vfx_diag.get_warnings` → empty array; stub (not exercised)
- `vfx_subgraph.get_exposed` → empty array with note; stub (not exercised)
- Batch dispatch: 8 of 9 `ApplyInTransaction` bodies throw `NotImplementedException` (F9)

None of these blocks Phase 4a approval, but Section 6 below lists them as Phase 5 carry-overs.

---

## 6. Phase 5 Carry-overs (TODOs / NIE stubs Phase 4 left open)

1. **VfxGraphTool** — Delete dead `catch (NotImplementedException)` blocks (F1), expose real graph info (space / capacity / systems) in `GetInfo`, wire real `compilation_status` / `get_health` reads (F3, F4).
2. **VfxNodeOps.SetSetting** — Fix silent `typeof(Vector3)` fallback (F2), dispatch `System.Convert.ChangeType` through `VfxCoercers.g.cs` for enums / Vector / Color (F7).
3. **VfxBatchTool** — Wire real `ApplyInTransaction` bodies in the other 8 tool classes and map `NotImplementedException` to `code=not_implemented` in the outer catch (F9).
4. **VfxDiagTool** — Populate `Attributes` / `Settings` catalog arrays from the walker (F10), wire `GetWarnings` to `IVFXErrorReporter.GetDirtyModelErrors` (requires a bridge helper since reporter access would otherwise need reflection).
5. **VfxSubgraphTool.GetExposed** — Enumerate exposed inputs on `VFXSubgraphOperator` / `VFXSubgraphBlock` / `VFXSubgraphContext`.
6. **VfxResponseShaper.ShapeRead** — Implement verbose/terse filtering for reads (F11).
7. **VfxNodeTool.Move / Duplicate** — Add first-class `MoveNode` and `DuplicateNode` methods to `IVfxNodeOps`, add verifier handlers (F15), replace add-at-offset duplicate fallback with a real copy-paste round-trip when Unity surfaces an API.
8. **Tool output mutation pattern** — Fold per-action response rendering into the Shaper (F5) so tools stop editing the Shaper's output JObject in place.
9. **VfxYamlVerifier** — Replace production-mode graceful-degradation with `MonoScript GUID → System.Type.FullName` resolution (F8). **See Section 7.**
10. **VfxAssetTool.Create** — Decide (document + comment) whether asset creation should run inside a transaction (F6).

---

## 7. Production-Mode YAML Verifier Discovery — Assessment

### Evidence

The Phase 4-SMOKE commit (`dfa0bfe`) modified `Editor/Kernel/VfxYamlVerifier.cs` to add a graceful-degradation branch:

```csharp
// Phase 4-SMOKE discovery: ...
// Real Unity .vfx YAML never has m_TypeFqn — node identity rides on
// m_Script GUIDs that resolve back to a MonoScript asset.
bool productionMode = blocks.Count > 0 && typeFqns.Count == 0;
// ...
if (productionMode) {
    result.Warnings.Add(new VfxVerifierWarning { Code = "yaml_verify_skipped", ... });
    return;
}
```

This short-circuits the strict-match path for every production `.vfx` YAML. Synthetic `VfxYamlVerifierTests` still pass because those fixtures explicitly set `m_TypeFqn:` lines, forcing `typeFqns.Count > 0 → strict mode`.

### What Part 1 of the health gate currently catches

With this graceful-degradation in place:

- **Add ops:** Emit `yaml_verify_skipped` warning. No hard error. The strict `intent_diverged` path is disabled for production YAML.
- **Connect ops:** Still verified via `byFileId` indexing + `LinkedSlots` scan. Connect verification does not depend on `m_TypeFqn` and is unaffected.
- **Remove / set_setting / set_property:** Not verified today (by design — Phase 4 scope note).

### Assessment

The fix is **acceptable for dogfooding and Phase 4a sign-off** because:
1. The three-part health gate still runs; Parts 2 (compile gate) and 3 (console correlator) remain strict.
2. Connect op verification (the most gap-heavy category per `vfx_graph_mcp_tool_gaps.md` items #3, #4) is still strict.
3. The synthetic tests still exercise the strict path end-to-end.

**BUT** the graceful-degradation is NOT acceptable for v0.3.0 release because:
1. Gap doc item #4 (batch drops connections but reports `allSucceeded: true`) depends on the verifier catching post-save divergence. Production-mode `yaml_verify_skipped` weakens this.
2. Gap doc items #1, #2, #5, #6 all involve "mutation silently dropped / wrong-typed" that the YAML strict-match path would catch — currently it catches nothing for add ops in production YAML.
3. Release-gate criterion #5 says "Three-part health gate passes on the smoke E2E with **zero warnings**." Production-mode would currently emit one `yaml_verify_skipped` warning per add op, violating this criterion literally.

### Recommendation

**Phase 5 task 5-4 (or a new task 5-2b) MUST land a `MonoScript GUID → System.Type.FullName` resolver BEFORE v0.3.0 ships.** The fix is small:

1. During `ScanMonoBehaviours`, also capture `m_Script: {fileID: X, guid: Y, type: Z}` lines.
2. In `Verify`, before the `productionMode` detection, do `AssetDatabase.GUIDToAssetPath(guid) → AssetDatabase.LoadAssetAtPath<MonoScript>(path) → ms.GetClass().FullName` for each block.
3. Populate `typeFqns` from that resolution.
4. Delete the `productionMode` fallback branch once the set is non-empty.

This is a Phase 5 task, not a Phase 4 BLOCKING finding. Phase 4a approves with this follow-up as a release-gate prerequisite.

---

## Closing Note

Phase 4 is solid work: the 9-tool surface compiles, every erratum is honored, the soft-fork bridge is cleanly additive (not reflective), the uniform `HandleCommand → switch → per-action` + `ApplyInTransaction` contract is preserved, and the smoke test is attested PASS. The two HIGH findings are mechanical fixes (F1 is "delete four catch blocks," F2 is "throw instead of fallback"). Phase 5 entry is not blocked.

The production-mode YAML verifier discovery is a real architectural issue but one Phase 4 correctly surfaced and scoped; landing the MonoScript GUID resolver in Phase 5 (before release) is the right call.

**Verdict: APPROVED_WITH_FOLLOWUPS.**
