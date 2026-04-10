# Known deferred items — disposition for the v0.3.1 E2E run

This file is the **authorization document**. It says:
- which `state="not_implemented"` envelopes the test agent should EXPECT to find unchanged (mark PASS),
- which ones should be lifted in this E2E run (mark ANOMALY if still stubbed),
- and which ones the fix subagents are authorized to implement (with allowed-files lists).

User-locked decision (2026-04-09): **all in-scope deferred items get lifted in this run.**

## Items to LIFT (test agent flags as ANOMALY if still stubbed)

### vfx_diag.list_attributes (Phase 4a F10)
- **Current:** `{"state": "not_implemented", "hint": "Built-in attribute enumeration deferred to v0.3.2 — see Phase 4a F10."}`
- **Decision:** LIFT
- **Approach:** Walk `VFXAttributesManager.GetBuiltInNames()` via the existing `com.unity.visualeffectgraph` `InternalsVisibleTo` grant. If unreachable from the addon assembly, fall back to a hardcoded built-in list keyed off Unity 6000.4 docs and stop — do NOT add a new bridge helper without explicit user approval. Report fallback choice in the fix subagent's report.
- **Files allowed to touch:** `Editor/Tools/VfxDiagTool.cs`, `Editor/Generation/VfxLibraryWalker.cs`, `Editor/Generation/Emitters/CatalogEmitter.cs`, `Editor/Generated/VfxCatalog.g.cs` (regenerate), `Tests/Editor/Tools/VfxDiagListAttributesTests.cs` (NEW, authorized)

### vfx_diag.list_settings (Phase 4a F10)
- **Current:** `{"state": "not_implemented", "hint": "Per-type setting enumeration deferred to v0.3.2 — see Phase 4a F10."}`
- **Decision:** LIFT
- **Approach:** Walk per-type `[VFXSetting]`-attributed fields across every walked `VFXModel` subclass at codegen time. Field-attribute reflection is build-time only — does NOT violate the no-runtime-reflection rule. Emit into `VfxCatalog.g.cs` keyed by type FQN.
- **Files allowed to touch:** same as above + `Editor/Tools/VfxDiagTool.cs`, `Tests/Editor/Tools/VfxDiagListSettingsTests.cs` (NEW, authorized)

### vfx_graph.compilation_status (Phase 4a F4)
- **Current:** `{"state": "not_implemented", "hint": "Use the 'compile' action to trigger a compile and read its result."}`
- **Decision:** LIFT
- **Approach:** Cache the last `VfxCompileResult` from `IVfxCompileGate.Compile` per asset path on the kernel container. `compilation_status` reads the cache. Returns the same shape `compile` does. If never compiled, return `{"state": "no_compile_yet", "hint": "Run vfx_graph.compile first."}` — that's a documented state, not a stub.
- **Files allowed to touch:** `Editor/Kernel/VfxCompileGate.cs`, `Editor/Tools/VfxGraphTool.cs`, `Tests/Editor/Kernel/VfxCompileGateTests.cs`

### vfx_graph.get_health (Phase 4a F4)
- **Current:** `{"state": "not_implemented", "hint": "Health is computed per-transaction. Run a save/compile/mutate to get a real report."}`
- **Decision:** LIFT (partial)
- **Approach:** Run a no-op transaction (BusyGate + Transaction.Begin + Commit with empty intent list) and return its `commit.Health` shaped via `Shape(commit, verbose)`. This forces the three-part health gate to compute against the current on-disk state without any mutation. Document the cost (one extra YAML diff + one extra compile) in the hint.
- **Files allowed to touch:** `Editor/Tools/VfxGraphTool.cs`, `Tests/Editor/Tools/VfxGraphHealthTests.cs` (NEW)

### vfx_graph.get_info F3 partial keys
- **Current:** `space`, `bounds_setting_mode`, `update_mode` are empty strings
- **Decision:** LIFT
- **Approach:** Walk top-level `VFXContext` children and collapse a single dominant `space` value (or empty if mixed). For `bounds_setting_mode`, walk the per-system `VFXDataParticle.boundsSettingMode` and return the first non-default. `update_mode` does NOT exist on VFXGraph; document this and either remove the key or return a constant that documents the absence (`"per_system"`). The test agent should accept either documented fix.
- **Files allowed to touch:** `Editor/Tools/VfxGraphTool.cs`, `Tests/Editor/Tools/VfxGraphGetInfoTests.cs` (NEW)

### vfx_graph.set_space / set_capacity / set_bounds
- **Current:** All three return `{"state": "not_implemented", "hint": "... use vfx_node.set_setting on the relevant context"}`
- **Decision:** LIFT (with guardrail)
- **Approach:** Implement as **convenience wrappers** that find the target context (init, output) automatically and call `NodeOps.SetSetting` under the hood — NOT new contract methods. The wrapper takes the user's intent ("set capacity = 8192") and dispatches to the right `VFXBasicInitialize` token internally. If the graph has multiple init contexts, return an honest error asking the user to pick one. This avoids touching `VfxKernelContracts.cs`.
- **Files allowed to touch:** `Editor/Tools/VfxGraphTool.cs`, `Tests/Editor/Tools/VfxGraphSetSpaceTests.cs`, `Tests/Editor/Tools/VfxGraphSetCapacityTests.cs`, `Tests/Editor/Tools/VfxGraphSetBoundsTests.cs` (all NEW)

### vfx_block.list_attributes (envelope inconsistency)
- **Current:** `{"note": "Phase 5: use vfx_diag.list_attributes"}`
- **Decision:** LIFT — replace with proper `state="not_implemented"` envelope, OR just delegate to `vfx_diag.list_attributes` once F10 above lands
- **Files allowed to touch:** `Editor/Tools/VfxBlockTool.cs`

### vfx_subgraph.get_exposed (envelope inconsistency)
- **Current:** `{"exposed": [], "note": "Phase 5: enumerate VFXSubgraphOperator/Block/Context exposed inputs"}`
- **Decision:** LIFT — implement real exposed-input enumeration (walk `VFXSubgraphOperator.subChildren` for `VFXParameter` instances with `m_Exposed=true`), or fall back to honest envelope.
- **Files allowed to touch:** `Editor/Tools/VfxSubgraphTool.cs`, `Editor/Kernel/VfxNodeOps.cs` (only if a new helper is needed)

### vfx_recipe.list (deferred note stale)
- **Current:** `{"recipes": [], "note": "Recipes deferred to v0.3.1."}` — but we ARE on v0.3.1
- **Decision:** LIFT — implement at minimum a list of recipe NAMES (not full templates) so the action returns useful data. Recipe execution itself stays deferred to v0.3.2 unless cheap.
- **Files allowed to touch:** `Editor/Tools/VfxRecipeTool.cs`

### vfx_subgraph.inline / extract
- **Current:** Throws `NotImplementedException("v0.3.1")`
- **Decision:** LIFT only if cleanly implementable; OK to escalate as "needs design work, deferring to v0.3.2 with explicit rationale"
- **Files allowed to touch:** `Editor/Tools/VfxSubgraphTool.cs`, plus kernel helpers if needed (escalate first)

### vfx_diag.get_warnings
- **Current:** `{"warnings": []}` always (TODO comment in source)
- **Decision:** LIFT — wire to `IVFXErrorReporter.GetDirtyModelErrors` if reachable; otherwise honest stub envelope
- **Files allowed to touch:** `Editor/Tools/VfxDiagTool.cs`

### vfx_node.move correctness (F5 + F15 incomplete)
- **Current:** Uses post-hoc `obj["moved"] = ...` instead of `ShapeMutation`. Records intent op `Kind="set_setting"` instead of `"move"`. Sets `model.position` directly instead of calling `IVfxNodeOps.MoveNode`.
- **Decision:** FIX — F5 and F15 cleanup miss
- **Files allowed to touch:** `Editor/Tools/VfxNodeTool.cs` (Move method), `Editor/Kernel/VfxNodeOps.cs` (verify MoveNode body exists), `Editor/Kernel/VfxYamlVerifier.cs` (confirm `case "move":` arm exists), `Tests/Editor/Tools/VfxNodeMoveTests.cs` (extend)

### vfx_block.reorder / set_activation intent op kinds
- **Current:** Both record `Kind = "set_setting"`
- **Decision:** LIFT — change to `Kind = "reorder"` and `Kind = "set_activation"` respectively. Verifier may need new case arms.
- **Files allowed to touch:** `Editor/Tools/VfxBlockTool.cs`, `Editor/Kernel/VfxYamlVerifier.cs` (only if needed)

### vfx_subgraph.add_ref response shape
- **Current:** `{"added": {"token": ..., "subgraph": ...}}` — single object not array
- **Decision:** LIFT — wrap in JArray to match `vfx_node.add` and `vfx_property.add` precedent
- **Files allowed to touch:** `Editor/Tools/VfxSubgraphTool.cs` (~3 line change)

### vfx_asset.assign response shape
- **Current:** Bypasses `ShapeMutation` on success — returns just `{"assigned": "..."}` with no warnings/health folding
- **Decision:** LIFT — fold through `ShapeMutation` like all other mutating actions
- **Files allowed to touch:** `Editor/Tools/VfxAssetTool.cs`

### README.md (project-wide stale docs)
- **Current:** `Packages/com.spiralingstudio.mcp.vfxgraph/README.md` describes the legacy `manage_vfx_graph` mega-tool architecture and references files (`VfxGraphActionRouter`, `VfxGraphEdit`, etc.) that no longer exist.
- **Decision:** REWRITE in the final pass (P3 cluster). Test agent does NOT touch this — handled by main agent post-clean.

## Items that STAY deferred (not in this run)

- `vfx_asset.delete` not batchable — INTENTIONAL per v0.3.1 design §4.5; the post-delete YAML verifier would fail. Test agent should EXPECT FAIL when calling `vfx_asset.delete` inside a `vfx_batch.commit`.
- Subgraph `inline`/`extract` MAY escalate to v0.3.2 if non-trivial — fix subagent decides at impl time.
