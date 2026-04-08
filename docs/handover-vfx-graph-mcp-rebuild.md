# Handover — VFX Graph MCP v0.3.1 (post-v0.3.0 ship)

**You are the implementation agent.** v0.3.0 is **shipped and tagged** (`v0.3.0` on branch `vfxgraph-rebuild-v0.3`). The full release gate is GREEN (107/107 tests, 11 of 13 criteria PASS, 1 PARTIAL deferred to v0.3.1, 1 satisfied at tag time). Your job is the **v0.3.1 follow-up** — primarily two pieces of work: (1) make the package usable from a downstream reference project, and (2) burn down the Phase 4a code-review backlog and the Phase 6 partial-coverage carry-overs.

## One-sentence scope
v0.3.0 is production-ready in the dev project; you make it production-ready in **someone else's** project, then close out the 13 review followups documented during v0.3.0.

## Start here
- **Branch:** `vfxgraph-rebuild-v0.3` — already exists, contains 50 commits ahead of `vfxgraph-package-v0.2`. **Do NOT recreate.**
- **Tag:** `v0.3.0` — the v0.3.0 ship point. Use `git diff v0.3.0..HEAD` to see anything you add.
- **Pre-rebuild snapshot tag:** `v0.2-pre-rebuild`
- **Phase 4a code review (the v0.3.1 backlog):** `docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md` — sections 2 (findings table) and 6 (Phase 5 carry-overs) ARE your backlog. F1+F2 are already fixed; F3–F7 and F9–F15 are open.
- **Phase 6 release gate (carry-overs):** `docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md` — section 6 lists v0.3.1 deferrals.
- **Spec:** `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`
- **Plan:** `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md` — the ERRATA at the top is still authoritative for any scope question.
- **Execution skill:** `superpowers:subagent-driven-development` is the recommended workflow for batched work; small mechanical fixes can be done by the main agent directly.

## What is already done — do NOT redo

| Phase | Work | Tag/commit |
|---|---|---|
| 1–3 | Embedded VFX Graph soft-fork (TWO patches), generator + walker + 6 emitters, kernel (identity/sidecar/fingerprint/verifier/compile gate/busy gate/console reader/correlator/transaction/shaper/NodeOps stub) | `5c7c17c..730ec49` |
| 4-SETUP + 4A/4B/4C | `VfxKernelContainer` + 9 MCP tool classes + `VfxNodeOps` fill-in | `36b5e6f..b2ad353` |
| 4-SMOKE | `VfxSmokeTests.EndToEnd_AllNineTools_ExerciseSinglePath` exercising every tool, plus `VfxYamlVerifier` graceful-degradation (later replaced by Phase 5-7 MonoScript GUID resolver) | `dfa0bfe` |
| 4a | Code review doc (APPROVED_WITH_FOLLOWUPS) + F1+F2 HIGH fixes | `a85d7bb`, `40f6332` |
| 5 | Quirks.yaml (33 collisions/6 aliases/1 default) + Hints.yaml (31 templates) + OverridesEmitter rewire + 500-node perf fixture + 7 perf tests + un-ignored busy-gate throw-case + no-reflection static analyzer + **MonoScript GUID resolver** in `VfxYamlVerifier` | `69ea29f..bef4a5b` |
| 6 | Release gate report — GREEN verdict | `129bdb5` |
| 7 | `git rm` of legacy `Editor/Tools/Vfx/` (~9185 lines) + 5 legacy test files + bump `package.json` to 0.3.0 + CHANGELOG entry forward-linking gap doc + 8 review/spec/plan docs | `3f512f5`, `92c9b97` |
| TAG | `v0.3.0` annotated tag | tag `v0.3.0` |

**Test state:** 107 tests in `com.spiralingstudio.mcp.vfxgraph.Editor.Tests`; 107 pass; 0 ignored; 0 fail. Run via `mcp__UnityMCP__run_tests` with `mode: "EditMode"` and `assembly_names: ["com.spiralingstudio.mcp.vfxgraph.Editor.Tests"]`. The pre-existing legacy `VfxGraphResultMapperTests` failure was eliminated by Phase 7's deletion.

**Catalog state:** 242 ops / 134 blocks / 18 contexts / 49 params / 2 subgraphs / 36 slot types — generated, committed, locked. The 6 `.g.cs` files in `Editor/Generated/` ship inside the addon and travel with it.

## v0.3.1 backlog — priority order

### P0 — Reference project installation (ASKED ABOUT 2026-04-08)

**Context:** v0.3.0 is fully working in the dev project at `/Users/pakaya/Documents/GitHub/VFX-MPC-wip` because the embedded VFX Graph package at `Packages/com.unity.visualeffectgraph/` carries the two soft-fork patches. A downstream project that pulls our addon by git URL or local path will hit `CS0122 — inaccessible due to its protection level` on the very first compile because its `com.unity.visualeffectgraph` is the unpatched package-cache copy. The catalog being generated does NOT solve this — the addon's editor assembly compiles against `internal` types from `Unity.VisualEffectGraph.Editor` (`VFXLibrary`, `VFXModel`, `VFXGraph`, `VFXSlot`, etc.) which require the InternalsVisibleTo grant from one of the soft-fork patches.

**The two soft-fork patches** (recap of the rebuild ERRATA section "Embedded VFX Graph package: TWO patch files"):

| Patch file | Location | Why |
|---|---|---|
| `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` | inside the embedded VFX Graph pkg | grants `InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")` and `…Tests` |
| `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs` | inside the embedded VFX Graph pkg | compile-time bridges (`LoadGraphFromAsset`, `CompileAndUpdateAsset`, `RefreshCompilationReport`, `CreateVfxAsset`, `CreateVfxSubgraphBlock`, `CreateVfxSubgraphOperator`) — needed because `VisualEffectResource` is in Unity engine's `UnityEditor.VFXModule` (cannot receive InternalsVisibleTo from a user package) and `VisualEffectAssetEditorUtility` is internal to `Unity.VisualEffectGraph.Editor` |

These patches **cannot ship inside our addon package** — they live in someone else's package. Three reasonable options for v0.3.1:

#### Option A — Documentation-only installer guide (lowest risk)
Write `Packages/com.spiralingstudio.mcp.vfxgraph/Documentation~/installation.md` (Unity convention: `Documentation~` is excluded from compile and shows up in the Package Manager UI). Walk the downstream user through:
1. Embed `com.unity.visualeffectgraph` (move from `Library/PackageCache/<name>@<hash>/` to `Packages/com.unity.visualeffectgraph/`)
2. Copy the two patch files from this repo's `Packages/com.unity.visualeffectgraph/Editor/{AssemblyInfo.cs,VfxMcpKernelHelpers.cs}` into the embedded copy
3. Add the addon to `Packages/manifest.json` (git URL or local path)
4. Verify by running the smoke test

Pros: zero code changes to the addon. Cons: every install is a manual ritual; users will skip step 1 and complain.

#### Option B — Post-install editor check + helper window (medium risk)
On first import (via `[InitializeOnLoad]` in a small `Editor/VfxMcpInstallChecker.cs`), detect whether `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` exists and contains the InternalsVisibleTo grant. If not, log a `Debug.LogError` with a clear message AND open an EditorWindow that explains the patch-or-uninstall choice. The window has two buttons: "Open patch instructions" (links to Documentation~) and "Patch automatically" (option C below).

Pros: surfaces the problem at the right moment. Cons: the check itself runs from the addon's editor assembly, which won't compile until the patches are applied — so the check has to live in a SEPARATE `MCPForUnity.Editor.Tools.VfxBootstrap` assembly that has zero VFX Graph references. Adds an asmdef.

#### Option C — Scripted patcher (highest risk, most user-friendly)
A button-press patcher that:
1. Checks whether `com.unity.visualeffectgraph` is embedded; if not, embeds it via `Client.Embed(packageName)` from `UnityEditor.PackageManager`
2. Copies the two patch files from `Packages/com.spiralingstudio.mcp.vfxgraph/Patches/com.unity.visualeffectgraph/` (a new shipped subdirectory inside the addon) into the embedded VFX Graph package
3. Triggers `AssetDatabase.Refresh()` and exits

This requires adding a `Patches/` subdirectory under the addon that mirrors the soft-fork file layout. Pros: one-click install for downstream users. Cons: lives in the bootstrap asmdef (same constraint as Option B), and Unity package upgrades will overwrite the patches — needs a re-patch after every VFX Graph version bump.

**Recommended scope for v0.3.1:** Option A (documentation) + Option B (editor check, no auto-patch). Defer Option C's auto-patcher to v0.3.2 if the docs prove insufficient.

**Acceptance criteria for P0:**
- A reference project that follows the doc can run `mcp__UnityMCP__run_tests` with the smoke test name and see it pass
- The post-install check correctly identifies an unpatched VFX Graph package and refuses to compile/run with a clear actionable error

### P1 — Phase 4a HIGH-equivalent backlog (correctness-affecting)

These are NOT marked HIGH in the review doc but they're correctness-affecting and should land before any external user touches the API:

**F2 follow-up (already fixed in `40f6332`, but verify):** `VfxNodeOps.SetSetting` no longer silent-falls-back to `Vector3` when a SerializableType caller passes a non-string. Check `Editor/Kernel/VfxNodeOps.cs:281+` matches the F2 fix.

**F5 (MEDIUM): Tool layer overwrites Shaper output.** Every mutating action in `VfxNodeTool/VfxBlockTool/VfxPropertyTool/VfxSubgraphTool` calls `Shape(commit, verbose)` then mutates the returned `JObject` to inject an `"added"` key. This works today only because `commit.Diffs` is empty. **Fix:** add a `ShapeMutation(commit, verbose, mutationPayload)` overload to `IVfxResponseShaper` that folds the per-action data into the response, OR record op metadata into `commit.Diffs` inside `scope.Record` so `Shape` renders it naturally. The contract change requires a new `VfxKernelContracts.cs` lock-bump commit per the project rules.

**F7 (MEDIUM): `VfxNodeOps.SetSetting` non-SerializableType branch uses `Convert.ChangeType`.** Crude — fails for enums, Vector2/3/4, Color, Curve, Gradient. **Fix:** dispatch through `VfxCoercers.g.cs` (already generated by Phase 3A-3) using `setting.field.FieldType` as the discriminator. This re-introduces gaps #6 and #19 if not fixed.

**F9 (MEDIUM): `VfxBatchTool` only routes one action.** 8 of 9 tools' `ApplyInTransaction` bodies throw `NotImplementedException("v0.3.1 batch integration")`. **Fix:** wire each tool's `ApplyInTransaction` to dispatch its own action switch (mirror `HandleCommand` but skip the BusyGate/Transaction wrapping — the batch dispatcher already opened them). The `VfxBatchTool.HandleCommand` outer catch should also map `NotImplementedException` to `code="not_implemented"` so callers can distinguish "doesn't batch yet" from a real bug.

**F15 (LOW but correctness-affecting): `VfxNodeTool.Move` records intent as `set_setting` but mutates `model.position` directly.** YAML verifier silently passes because there's no `move` handler. **Fix:** add `MoveNode(token, Vector2)` to `IVfxNodeOps` (contracts lock bump), wire `VfxNodeTool.move` action through it, add a `case "move":` handler in `VfxYamlVerifier.VerifyFromYamlInternal` so position drift is caught.

### P2 — Phase 6 partial-coverage carry-overs

**Subgraph lifecycle E2E (criterion #6 PARTIAL).** Add `Tests/Editor/Tools/VfxSubgraphTests.cs` with:
- A fixture that creates a `.vfxoperator` subgraph with 2 exposed inputs (Float + Vector3)
- Reference it in a parent `.vfx` graph via `vfx_subgraph.add_ref`
- Set overrides via `vfx_subgraph.set_override`
- Mutate the subgraph's exposed inputs (rename one, drop one)
- Re-run the parent's compile / verify the parent surfaces `subgraph_interface_changed` either as a verifier warning or a compile error
- Assert the override values are surfaced in `vfx_subgraph.get_exposed`

The catalog has 2 subgraph types (`VFXSubgraphOperator`, `VFXSubgraphBlock`); `VFXSubgraphContext` is NOT in `VFXLibrary.GetContexts()` and uses the file-based `.vfx` extension instead — it can stay out of scope for v0.3.1 unless the user asks for it.

**Multi-op batch abort/rollback E2E.** Once F9 is done (8 more `ApplyInTransaction` bodies wired), add a test that runs a 5-op batch where op 3 deliberately throws (e.g., `vfx_node.add` with a bogus type FQN), and asserts:
- The transaction commit returns `Ok=false` with a `vfx_exception` or `validation_error` envelope
- The asset YAML on disk shows NONE of the 5 ops (the batch rolled back, not partially-applied)
- The console correlator surfaces the failing op's exception text

### P3 — Phase 4a MEDIUM/LOW polish (defer if time-pressed)

| ID | File:line | Fix |
|---|---|---|
| F3 | `Editor/Tools/VfxGraphTool.cs:101–118` | `GetInfo` exposes `graph.space`, `graph.systemNames`, capacity, bounds-setting-mode, system count via the public VFXGraph surface. |
| F4 | `Editor/Tools/VfxGraphTool.cs:117–125` | `compilation_status` and `get_health` return `state="not_implemented"` instead of `status=unknown` so clients can distinguish. |
| F6 | `Editor/Tools/VfxAssetTool.cs:74–99` | `Create` either documents the intentional transaction-bypass with an inline comment, or wraps the create call in a `SingleCall` transaction. |
| F10 | `Editor/Tools/VfxDiagTool.cs:102–114` + `Editor/Generation/VfxLibraryWalker.cs` + `Editor/Generation/Emitters/CatalogEmitter.cs` | Walker emits `VfxCatalog.Attributes[]` and `VfxCatalog.Settings[]` arrays; `list_attributes` and `list_settings` page over them instead of returning empty stubs. |
| F11 | `Editor/Kernel/VfxResponseShaper.cs:66–70` | `ShapeRead` honors `verbose`: terse mode strips deep nested fields per spec performance budget. |
| F12 | `Editor/Kernel/VfxNodeOps.cs:407–412` | Add inline comment: `GetSetting` returns AssemblyQualifiedName for SerializableType so it round-trips through `SetSetting` via `Type.GetType(aqn)`. |
| F13 | `Editor/Tools/VfxNodeTool.cs:75–78` (and 7 sibling tool files) | `ApplyInTransaction` stub message: `"batch dispatch — wired during v0.3.1 batch integration"` — distinct from `vfx_subgraph.inline/extract`'s `"v0.3.1 — non-destructive refactoring"`. |
| F14 | `Tests/Editor/Tools/VfxSmokeTests.cs:132–141` | `vfx_property.add` assertion tightens to `AssertNoError` once the parameter catalogue discriminator is confirmed. |

### P4 — `vfx_subgraph.inline` and `vfx_subgraph.extract` (full v0.3.1 feature)

Per erratum A-H2 these were always v0.3.1. Implementing them is non-trivial (each is its own design problem):
- **inline:** copy every node + slot binding from the referenced subgraph into the parent graph, rewrite all linked-slot fileIDs, delete the subgraph reference node. The fileID rewrite is the hard part — see how `VFXViewController.PasteAll` handles it.
- **extract:** the inverse — select a sub-graph of nodes in the parent, create a new `.vfxoperator`/`.vfxblock` asset, move the selected nodes there with their connections preserved, replace them in the parent with a new `vfx_subgraph.add_ref`.

If you ship these, also wire them into the smoke test and add a dedicated `VfxSubgraphInlineExtractTests.cs`.

## Critical things the next agent must know

**a) The TWO soft-fork patches are non-negotiable.** Every Unity package bump of `com.unity.visualeffectgraph` must preserve both files. The pre-commit hook (opt-in) verifies they exist. The startup sanity check `VFXLibrary.GetOperators().Any()` continues to guard the InternalsVisibleTo grant; an additional sanity check `VfxMcpKernelHelpers.LoadGraphFromAsset(testAsset) != null` guards the bridge helper. The bridges are compile-time-only — they do NOT reflect on anything. The reflection rule (no runtime reflection on `UnityEditor.VFX.*` / `UnityEngine.VFX.*` types) is enforced by `VfxNoReflectionTests` (Phase 5-6) and remains intact.

**b) `VfxKernelContracts.cs` is LOCKED.** Any change requires a new contracts commit + main-agent approval before touching consumer files. F5 and F15 both need contract changes — bundle them into ONE contracts-bump commit.

**c) `VfxSubgraphContext` is NOT in `VFXLibrary.GetContexts()`** — it lacks `[VFXInfo]`. Catalog has **2** subgraph types. P2's subgraph lifecycle E2E should target operator + block subgraphs only.

**d) Top-level parent fingerprint rule:** nodes whose direct parent is the `VFXGraph` are "top-level" and use `parentFp = 0`. Enforced in `VfxStructuralFingerprint.Compute`, `VfxIdentity.Mint`, and the recovery filter. Don't pass non-zero `parentFp` for top-level operators.

**e) Asset extensions:** `.vfxblock` (block subgraph) and `.vfxoperator` (operator subgraph). `.vfxop` does NOT exist. `.vfx` is the main graph. Use the bridge helpers in `VfxMcpKernelHelpers.cs` for asset creation, not `ScriptableObject.CreateInstance`.

**f) Token-savings:** terse mode (default `verbose=false`) MUST omit the `health` key. Release-gate criterion. The Shaper enforces this automatically — don't bypass the Shaper.

**g) Batch dispatcher uses reflection on OUR OWN tool classes** — that's allowed and tested. Reflection on `UnityEditor.VFX.*` types is forbidden. The phase 5-6 `VfxNoReflectionTests` analyzer enforces this.

**h) The MonoScript GUID resolver in `VfxYamlVerifier`** has a `forceProductionModeForMisses=true` fallback for unresolved GUIDs (e.g., subgraph .vfxoperator references where `MonoScript.GetClass()` returns null). This is INTENTIONAL per Phase 5-7 — don't remove it. If it false-positives on regular VFX nodes (where GUID resolution should work), that's a bug; investigate before disabling.

**i) Sandbox / commit caveat for parallel-lane subagents.** During Phases 3 and 4, subagents hit a hard sandbox block on `git commit` / `git reset` / `git restore --staged` / `git stash`; `git add` was the only mutation allowed. Subagents had to leave their work staged in the index for the parent agent to commit on their behalf using `git commit --only -- <pathspecs>`. **Same applies for any subagent you dispatch in v0.3.1.** Instruct each lane to prefer `git add` and write a final report listing exact file paths + suggested commit messages.

## Hard constraints — violations mean stop and reconsider

1. **No runtime reflection on `UnityEditor.VFX.*` / `UnityEngine.VFX.*`** in `Editor/Kernel/` or `Editor/Generation/`. `VfxNoReflectionTests` (category 18) enforces this.
2. **All 9 tools expose `internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)` with identical signature** — this is the batch dispatcher contract. Don't change it.
3. **`vfx_subgraph.inline` and `vfx_subgraph.extract` MAY ship in v0.3.1.** If they don't, leave the `NotImplementedException("v0.3.1")` stubs in place.
4. **`vfx_recipe` is a scaffold;** only the `list` action returns `[]`. Recipes are out of scope for v0.3.1 unless explicitly asked.
5. **Mutating actions go through `VfxBusyGate.EnsureIdle()` first**, then a `VfxTransaction` from `VfxKernelContainer.Transaction.Begin(...)`. Read-only actions (`get_*`, `list_*`, `read_console`) skip the busy gate.
6. **Three-part health gate runs at `VfxTransaction.Commit()` only** (YAML diff → compile status → console correlation). Never per-op inside a batch.
7. **Token-savings: terse mode (default `verbose=false`) MUST omit the `health` key.** Release-gate criterion.
8. **All response shaping goes through `VfxKernelContainer.Shaper`** (the single chokepoint).
9. **Kernel contracts are LOCKED.** Any change requires a new contracts commit + main-agent approval before touching consumer files.
10. **Asset creation goes through `VfxMcpKernelHelpers.CreateVfxAsset` / `CreateVfxSubgraphBlock` / `CreateVfxSubgraphOperator`** (the soft-fork bridges added in Phase 4A). Never `ScriptableObject.CreateInstance`.

## Phase map — v0.3.1

| Phase | Purpose | Lane structure | Next-up |
|---|---|---|---|
| v0.3.1-A | Reference project installation tooling (P0) | Single agent (Option A doc + Option B post-install check) | Decide which Option(s) to ship; write the doc; wire the bootstrap asmdef |
| v0.3.1-B | Correctness-affecting backlog (P1: F5, F7, F9, F15) | Single agent or split into 4 small subagents | F9 is the biggest — 8 `ApplyInTransaction` bodies. Others are small. |
| v0.3.1-C | Subgraph lifecycle E2E + batch abort E2E (P2) | Single agent | Needs F9 to land first. |
| v0.3.1-D | Polish backlog (P3: F3, F4, F6, F10, F11, F12, F13, F14) | Single agent | Mechanical; can run in parallel with v0.3.1-A and v0.3.1-B if disjoint files. |
| v0.3.1-E | `vfx_subgraph.inline` + `extract` (P4) | Single agent — design + implement + test | Largest scope; defer to v0.3.2 if other priorities push back. |
| v0.3.1-RC | Re-run release gate; tag v0.3.1 | Single agent | Same release gate criteria; expect criterion #6 to flip from PARTIAL to PASS once P2 lands. |

## Definition of done (v0.3.1)

- All 13 release gate criteria from spec section "Production-ready release gate" green (criterion #6 flips from PARTIAL to PASS).
- Reference-project install path documented and tested end-to-end (a sibling Unity project pulls the addon, applies the patches, runs the smoke test successfully).
- Phase 4a backlog items F3–F7 and F9–F15 either fixed or explicitly closed-as-deferred to v0.3.2 with a comment in the review doc.
- `package.json` bumped to `0.3.1`. CHANGELOG entry. Tag `v0.3.1` on the rebuild branch.

## When in doubt

- **Plan ERRATA is authoritative** if a task body conflicts with it.
- **Phase 4a code review (`docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md`)** is the source of truth for what F1–F15 means and which are HIGH/MEDIUM/LOW.
- **Phase 6 release gate (`docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md`)** is the source of truth for which release-gate criteria are PASS/PARTIAL/DEFERRED.
- **Project conventions in CLAUDE.md** still apply: use `mcp__jcodemunch__*` tools for code exploration, never `grep`/`cat`/`find` on real source.
- **Memory `~/.claude/projects/-Users-pakaya-Documents-GitHub-VFX-MPC-wip/memory/feedback_defer_to_followup_runs.md`** still applies: when fixing a backlog item, narrow current scope to mechanical work only; don't expand into adjacent improvements.
- **API name uncertainty** → use `mcp__jcodemunch__search_symbols` / `get_file_content` against the embedded `Packages/com.unity.visualeffectgraph/`. NEVER guess.
- **Reference document inventory:**
  - `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md` — implementation plan + ERRATA
  - `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md` — spec
  - `docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md` — Phase 4a code review (the v0.3.1 backlog source)
  - `docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md` — Phase 6 release gate
  - `docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/objective-review-vfx-graph-mcp-redesign.md` — external spec reviews
  - `docs/plan-review-vfx-graph-mcp-rebuild.md`, `docs/agent-review-vfx-graph-mcp-rebuild-plan.md` — external plan reviews
  - `docs/vfx_graph_mcp_tool_gaps.md` — gap doc (may or may not still be relevant; cross-check before relying)
  - `Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md` — `[0.3.0]` entry forward-links the docs above
  - `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs` — **locked** interface surface
  - `Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs` — soft-fork bridge (the ONLY path from `VisualEffectAsset` to `VFXGraph`)
