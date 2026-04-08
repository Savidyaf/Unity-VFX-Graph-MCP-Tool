# Handover — VFX Graph MCP v0.3.0 Rebuild

**You are the implementation agent.** Phases 1, 2, and 3 (including kernel contracts lock and integration fixes) are done. Your job starts at **Phase 4**: 9 MCP tool classes via 3 parallel subagent lanes, then the E2E smoke test, then code review.

## One-sentence scope
The generator-driven typed catalog and the runtime kernel (identity, YAML verifier, compile gate, busy gate, console reader, correlator, transaction, response shaper) are built and tested; you now wire 9 MCP tools on top of them.

## Start here
- **Plan:** `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md` — **read the ERRATA block at the top FIRST**, then jump to Phase 4 (line 3576). Errata are authoritative when they conflict with task bodies.
- **Branch:** `vfxgraph-rebuild-v0.3` — already exists, 33 commits ahead of `vfxgraph-package-v0.2`. **Do NOT recreate.**
- **Pre-rebuild snapshot tag:** `v0.2-pre-rebuild`
- **Execution skill:** `superpowers:subagent-driven-development` — Phase 4 has **3 parallel lanes (4A/4B/4C)**; dispatch a fresh subagent per lane.
- **Phase 4-SMOKE is a NEW task** between the Phase 4 lanes and Phase 4a code review (per erratum A-B1, moved up from the old Phase 6). Write `VfxSmokeTests.cs` BEFORE dispatching the code reviewer.

## What is already done — do NOT redo

| Phase | Work | Commit range |
|---|---|---|
| 1 | Plan errata + InternalsVisibleTo patch + addon asmdef reference + smoke test | `5c7c17c..716b9e6` (4 commits) |
| 2 | CatalogIR + walker (ops/blocks/contexts/params/subgraphs) + CatalogEmitter + generator menu + baseline catalog + determinism test + `.meta` tracking | `0687790..fe20341` (6 commits) |
| 3 — contracts lock | `Editor/Kernel/VfxKernelContracts.cs` (all kernel interfaces + envelope/exception types — **locked**) | `d4ec776` |
| 3A (8 commits) | Walker extension (settings + slot bindings) + slot tree builder + 5 emitters (Coercers / Wrappers / SubgraphWrappers / Schemas / Overrides) + `Quirks.yaml`/`Hints.yaml` stubs + completeness tests | `d342ef5..7b80158` |
| 3B (4 commits) | `VfxStructuralFingerprint` (FNV-1a 64) + `VfxIdentitySidecar` v2 + `VfxIdentity` + `VfxYamlVerifier` (partial UnityYAML reader + structural diff) | `8d9bf45..e282c95` |
| 3C (4 commits) | `VfxBusyGate` + `VfxCompileGate` + soft-fork bridge `VfxMcpKernelHelpers.cs` + `VfxConsoleReader` (ring buffer, zero reflection) + `VfxConsoleCorrelator` | `380c105..730ec49` |
| 3D (3 commits) | `VfxResponseShaper` (terse/verbose) + `VfxTransaction` (three-part health gate) + `VfxNodeOps` stub | `52c3a95..c579135` |
| 3 — integration | Top-level parent fingerprint rule fix in `VfxStructuralFingerprint`/`VfxIdentity`/recovery filter + drop remaining reflection from `VfxIdentity.LoadGraph` + plan errata update for two-patch soft fork | `9622ab6..d558dd9` |

**Test state:** 99 tests in `com.spiralingstudio.mcp.vfxgraph.Editor.Tests`; 98 pass; 1 ignored (`VfxBusyGateTests` throw-case deferred to phase 5); 1 legacy failure (`VfxGraphResultMapperTests.Wrap_FailureAnonymousObject_ReturnsContractError` — pre-existing, scheduled for deletion in Phase 7, **ignore**).

**Catalog state:** 242 ops / 134 blocks / 18 contexts / 49 params / 2 subgraphs / 36 slot types regenerated; 6 `.g.cs` files, 2189 LOC.

## Critical phase-3 discoveries the next agent must know

**a) TWO soft-fork patch files, not one.** The spec originally said "one line." Phase 3 integration proved a second patch is required because `UnityEditor.VFX.VisualEffectResource` is `internal` to Unity engine's `UnityEditor.VFXModule`, which cannot receive an `InternalsVisibleTo` grant from a user package.

| Path | Purpose |
|---|---|
| `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` | 3 lines — `InternalsVisibleTo` for addon Editor + Tests |
| `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs` | ~70 lines — compile-time bridges: `LoadGraphFromAsset`, `CompileAndUpdateAsset`, `RefreshCompilationReport` |

**Phase 4 code that needs a `VFXGraph` from a guid/path MUST call `VfxMcpKernelHelpers.LoadGraphFromAsset(asset)`.** Never reflect, never try `asset.GetResource().GetOrCreateGraph()` directly — it will not compile.

**b) The kernel is ZERO reflection.** No documented exceptions. `VfxConsoleReader` uses `Application.logMessageReceived` (no `LogEntries` reflection). `VfxIdentity.LoadGraph` uses the bridge above. Phase 5 task 5-6 static analyzer must enforce this as-is.

**c) `VFXSubgraphContext` is NOT in `VFXLibrary.GetContexts()`** — it lacks a `[VFXInfo]` attribute. Catalog has **2** subgraphs (one operator subgraph + one block subgraph), not 3. Phase 4 lane 4C's `vfx_subgraph` tool must use a file-based path for context subgraphs via the `.vfxblock` asset extension. `VFXSubgraphOperator` and `VFXSubgraphBlock` ARE in the library.

**d) Top-level parent fingerprint rule.** Per spec, nodes whose direct parent is the `VFXGraph` are "top-level" and use `parentFp = 0`. The `VFXGraph` is the recursion stop condition in `VfxStructuralFingerprint.Compute` — not a participating parent. Enforced in `VfxStructuralFingerprint.Compute`, `VfxIdentity.Mint`, and the recovery filter. Phase 4 code that mints/resolves tokens for top-level operators must rely on this — do not pass non-zero `parentFp` for them.

**e) Subgraph asset extensions: `.vfxblock` + `.vfxoperator`. `.vfxop` does NOT exist** — every occurrence in older spec/plan text is wrong. Phase 4 `VfxAssetTool.Create` and `VfxSubgraphTool` must use `VisualEffectAssetEditorUtility.CreateNewAsset(path)` for `.vfx` and `VisualEffectAssetEditorUtility.CreateNew<VisualEffectSubgraphBlock>(path)` / `CreateNew<VisualEffectSubgraphOperator>(path)` for the others. Do NOT use `ScriptableObject.CreateInstance` — `VisualEffectAsset` is a native Unity object.

**f) `VisualEffectSubgraphOperator`/`VisualEffectSubgraphBlock` are internal to the Runtime assembly.** The `InternalsVisibleTo` patch only covers the Editor assembly. Phase 4 code that needs to reference these subgraph asset types should use `AssetDatabase.LoadAssetAtPath<UnityEngine.Object>()` and validate with `asset.GetType().Name` string compare (the pattern Lane 3A used for `SubgraphWrappersEmitter`). `VFXSubgraphContext` references `VisualEffectAsset` which IS public.

**g) The Tests asmdef has full VFX Graph access.** It references `Unity.VisualEffectGraph.Editor` + `Runtime` + `RenderPipelines.Core.Editor` + `Runtime` and is in the `InternalsVisibleTo` grant. Phase 4 tool tests may call `VFXLibrary.Get*()` directly.

**h) Sandbox / commit caveat for parallel-lane subagents.** During Phase 3, all 4 lane subagents hit a hard sandbox block on `git commit` / `git reset` / `git restore --staged` / `git stash`; `git add` was the only mutation allowed. Subagents had to leave their work staged in the index for the parent agent to commit on their behalf using `git commit --only -- <pathspecs>`. For Phase 4 lanes:
1. Instruct each lane to **prefer `git add`** and to write a final report listing exact file paths + suggested commit messages.
2. Be ready to commit on lanes' behalf with `git commit --only -- <files>` to keep lane work separated.
3. The `--only` flag commits only listed paths and leaves the rest of the index untouched, so multiple lanes' staged work can coexist until the parent commits each separately.

**i) Pre-existing legacy test failure to ignore.** `SpiralingStudio.Mcp.VfxGraph.Tests.Editor.VfxGraphResultMapperTests.Wrap_FailureAnonymousObject_ReturnsContractError` fails ("not_found" vs "unknown_error"). It tests the old `VfxGraphResultMapper` scheduled for deletion in Phase 7. Ignore it on every test run.

## Hard constraints — violations mean stop and reconsider

1. **No runtime reflection on `UnityEditor.VFX.*` / `UnityEngine.VFX.*`.** Kernel reaches zero reflection; no exceptions. Reflection on OUR OWN generated tool classes (for batch dispatch) is allowed and tested.
2. **All 9 tools expose `internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)` with identical signature** — Phase 4 Lane 4C's batch dispatcher depends on reflection against our own tool class names (see task 4-COORD + 4-SETUP first).
3. **`vfx_subgraph.inline` and `vfx_subgraph.extract` are deferred to v0.3.1** per erratum A-H2; throw `NotImplementedException("v0.3.1")`. Do not include in schemas.
4. **`vfx_recipe` is a scaffold;** only the `list` action returns `[]` with a v0.3.1 note.
5. **Mutating actions go through `VfxBusyGate.EnsureIdle()` first**, then a `VfxTransaction` from `VfxKernelContainer.Transaction.Begin(...)`. Read-only actions (`get_*`, `list_*`, `read_console`) skip the busy gate.
6. **Three-part health gate runs at `VfxTransaction.Commit()` only** (YAML diff → compile status → console correlation). Never per-op inside a batch.
7. **Token-savings: terse mode (default `verbose=false`) MUST omit the `health` key.** Release-gate criterion.
8. **All response shaping goes through `VfxKernelContainer.Shaper`** (the single chokepoint).
9. **Kernel contracts are LOCKED.** Any change requires a new contracts commit + main-agent approval before touching lane work.
10. **Asset creation per erratum P-M5:** `VfxAssetTool.Create` uses `VisualEffectAssetEditorUtility.CreateNewAsset(path)` for `.vfx` and `VisualEffectAssetEditorUtility.CreateNew<VisualEffectSubgraphBlock>(path)` / `CreateNew<VisualEffectSubgraphOperator>(path)` for the subgraph types.

## Phase map — remaining phases only

| Phase | Purpose | Lane structure | Next-up tasks |
|---|---|---|---|
| 4 | 9 MCP tools | **3 parallel lanes** (4A / 4B / 4C) — read task **4-COORD** + **4-SETUP** FIRST | 4-SETUP (VfxKernelContainer wiring) → 4A / 4B / 4C in parallel |
| 4-SMOKE | E2E smoke test (erratum A-B1, moved up from old Phase 6) | Single agent, after lanes integrate | Build minimal thruster VFX exercising every tool |
| 4a | Max-effort code review | Single `superpowers:code-reviewer` subagent | Load spec + 4 review docs + gaps doc + CLAUDE.md context |
| 5 | Quirks + Hints + perf + busy + no-reflection analyzer | Single agent | `Quirks.yaml` + `Hints.yaml` entries + 500-node perf fixture + static analyzer (enforces zero kernel reflection) |
| 6 | Release gate (13 criteria) | Single agent | Re-run smoke + verify all 13 criteria + fix anything failing |
| 7 | Cleanup | Single agent | `git rm` old `Editor/Tools/Vfx/` + delete 5 legacy test files + bump `package.json` to 0.3.0 + CHANGELOG with 4-review-doc forward-link |

## Phase 4 lane decomposition

- **Lane 4A** — `VfxAssetTool` (create/list/delete/assign), `VfxGraphTool` (get_info/save/compile/compilation_status/read_console/set_space/set_capacity/set_bounds/set_data_settings/discard_changes/get_health), `VfxDiagTool` (list_node_types/list_block_types/list_contexts/list_attributes/list_settings/list_subgraphs/read_console/get_warnings)
- **Lane 4B** — `VfxNodeTool` (add/remove/move/duplicate/connect/disconnect/get_setting/set_setting/get_property/set_property), `VfxBlockTool` (add/remove/reorder/set_activation/attribute blocks/custom attributes), `VfxPropertyTool` (add/remove/set_value/set_exposed). **Owns most of `VfxNodeOps` fill-in.**
- **Lane 4C** — `VfxSubgraphTool` (create/add_ref/inline\*/extract\*/get_exposed/set_override — \*deferred), `VfxRecipeTool` (scaffold), `VfxBatchTool` (batched ops with single end-of-batch health gate). **Coordinates via task 4-COORD.**

Each lane adds mutation methods to `VfxNodeOps.cs` (currently a stub with `NotImplementedException` bodies + a working `DiscardChanges`). Lane 4B holds most of the implementation; 4A and 4C add their tool-specific methods via the coordinated extension pattern.

## Reference documents

| Doc | When to consult |
|---|---|
| `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md` | **Read ERRATA first.** Authoritative when task bodies disagree with errata. |
| `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md` | Source of truth for architecture, identity model, error codes, health gate, decisions log. |
| `docs/vfx_graph_mcp_tool_gaps.md` | Known bugs in the old tool; if you're about to re-introduce one, stop. |
| `docs/plan-review-vfx-graph-mcp-rebuild.md`, `docs/agent-review-vfx-graph-mcp-rebuild-plan.md` | Plan-level reviews consumed by errata. |
| `docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/objective-review-vfx-graph-mcp-redesign.md` | Spec-level reviews. |
| `CLAUDE.md` | Project conventions. **Always use `mcp__jcodemunch__*` tools for code exploration**, not built-in Grep/Read. |
| `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs` | **Locked** interface surface for the kernel; every Phase 4 tool calls into these. |
| `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs` | Soft-fork bridge — the ONLY path to `VFXGraph` from a `VisualEffectAsset`. |

## Definition of done

All 13 release-gate criteria green (spec section "Production-ready release gate"). All 18 test categories pass. Phase 7 cleanup committed. `v0.3.0` tag on the rebuild branch.

## When in doubt

- **Plan ERRATA is authoritative** if a task body conflicts with it.
- **API name uncertainty** → grep the embedded `Packages/com.unity.visualeffectgraph/` source via `mcp__jcodemunch__search_symbols` / `search_text`. NEVER guess API names.
- **Phase 4 tool needs a `VFXGraph` from a path** → `VfxMcpKernelHelpers.LoadGraphFromAsset(asset)`. Do not reflect.
- **Scope creep tempting** → defer to v0.3.1 per `~/.claude/projects/-Users-pakaya-Documents-GitHub-VFX-MPC-wip/memory/feedback_defer_to_followup_runs.md`.
