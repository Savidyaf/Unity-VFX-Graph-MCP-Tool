# Handover — VFX Graph MCP v0.3.0 Rebuild

**You are the implementation agent.** This is a production-ready rebuild of the VFX Graph MCP tools. Read this doc first, then start executing the plan.

## One-sentence scope
Replace `manage_vfx` + `manage_vfx_graph` + `inspect_vfx_asset` with a 9-tool suite backed by a generator-driven typed catalog, sidecar identity, and a three-part health gate.

## Start here
- **Plan:** `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md` — execute task-by-task
- **Execution skill:** `superpowers:subagent-driven-development` (dispatch a fresh subagent per task; two-stage review between tasks)
- **Branch:** `vfxgraph-rebuild-v0.3` (create off `vfxgraph-package-v0.2` HEAD as task 1)
- **Pre-rebuild snapshot tag:** `v0.2-pre-rebuild` (also created in task 1)

## Reference documents (read on demand, not upfront)
| Doc | When to consult |
|---|---|
| `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md` | Source of truth for architecture, identity model, error codes, health gate. Any ambiguity in a plan task → check here. |
| `docs/vfx_graph_mcp_tool_gaps.md` | Known bugs in the old tool. The new architecture should make most gaps structurally impossible; if you're writing code that re-introduces a gap, stop. |
| `docs/spec-review-vfx-graph-mcp-redesign.md` | External review 1 — technical findings that shaped the spec. |
| `docs/objective-review-vfx-graph-mcp-redesign.md` | External review 2 — scope / production-readiness findings. |
| `CLAUDE.md` | Project conventions. **Always use jcodemunch tools for code exploration**, not built-in Grep/Read. |

## Hard constraints — violations mean stop and reconsider
1. **No runtime reflection on `UnityEditor.VFX.*` types.** The kernel calls into generated code only. `VfxConsoleReader` is the one documented exception (reflects on `UnityEditor.LogEntries`, not VFX).
2. **No asset mutation for identity.** `VFXModel.name`/`label`/serialized fields are off-limits. Identity lives entirely in `Library/VfxMcpIdentity.json` + structural fingerprints. Do not write tokens into assets.
3. **InternalsVisibleTo patch is mandatory.** Phase 1 adds `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` (2 lines). If `VFXLibrary.GetOperators().Any()` is ever false at startup, the patch is missing — fail loudly, don't work around it.
4. **Three-part health gate runs at `VfxTransaction.Commit()` only.** Never per-op inside a batch. YAML diff + compile status + console correlation, in that order.
5. **Kernel contracts are LOCKED during phases 3 and 4.** `Editor/Kernel/VfxKernelContracts.cs` is committed before parallel lanes start. Any change requires main-agent approval and a new contracts commit.
6. **TDD throughout.** Every task has test → fail → implement → pass → commit. Do not skip the red-then-green cycle.
7. **Defer fixes to v0.3.1.** This is the rebuild run. Recipes (`vfx_recipe`), gap-doc regression tests, and residual fixes belong to the next run per the memory note. Don't scope-creep.

## Phase map (9 phases, ~80 tasks)
| Phase | Purpose | Lane structure |
|---|---|---|
| 1 | Branch + tag + commit embedded packages + InternalsVisibleTo patch + asmdef reference + smoke test | Single agent |
| 2 | Generator scaffold: CatalogIR, VfxLibraryWalker, CatalogEmitter, VfxCatalogGenerator menu item, first baseline emission, determinism test | Single agent |
| 3 | Full generator coverage + runtime kernel | **4 parallel lanes (3A/3B/3C/3D)** after contracts are locked |
| 4 | 9 MCP tools (vfx_asset, vfx_graph, vfx_node, vfx_block, vfx_property, vfx_subgraph, vfx_recipe, vfx_batch, vfx_diag) | **3 parallel lanes (4A/4B/4C)** |
| 4a | Max-effort code review via `superpowers:code-reviewer` subagent with full spec + reviews context | Single subagent |
| 5 | Populate `Quirks.yaml` + `Hints.yaml` from phase-4 dogfooding; perf/busy/no-reflection tests | Single agent |
| 6 | Production-ready release-gate validation (13 criteria, 18 test categories) | Single agent |
| 7 | Delete legacy files (30+), move `VfxConsoleReader.cs`, bump to v0.3.0, CHANGELOG | Single agent |

## Parallel lane rules (phases 3 and 4)
- Each lane owns a disjoint file set. No lane modifies another lane's files.
- Shared files (`VfxCatalogGenerator.cs`, `VfxNodeOps.cs`) have one owning lane. Lane 3A owns the generator; lane B/C coordinate via task 4-COORD before touching `VfxNodeOps.cs`.
- At phase end, main agent runs the full test suite and fixes any cross-lane compile errors.
- All 9 tool classes must expose `internal static object ApplyInTransaction(JObject, VfxTransactionScope)` with identical signature — batch dispatcher depends on it.

## Gotchas worth knowing before you start
- **`VFXContext.label` exists; no other VFXModel has it.** Don't assume a universal label field.
- **`VFXViewController.AddVFXModel(Vector2 pos, VFXModel model)` takes a pre-instantiated model.** Two-step pattern: `descriptor.CreateInstance()` → `controller.AddVFXModel(pos, m)`. For blocks: `context.AddChild(block, index)`.
- **`VFXGraph.CompileAndUpdateAsset()` is synchronous** (not `CompileOrRuntimeError` — that method does not exist). Read errors via `graph.errorManager.compileReporter.GetDirtyModelErrors(model)` after the call returns.
- **`VFXLibrary.GetOperators/GetBlocks/GetContexts/GetParameters` are the walker's source of truth**, NOT raw assembly reflection. Raw reflection yields abstract/deprecated types that fail at `CreateInstance()`.
- **Subgraph assets:** `.vfxop` = context/block subgraph, `.vfxoperator` = operator subgraph. Three concrete types: `VFXSubgraphContext`, `VFXSubgraphBlock`, `VFXSubgraphOperator`.
- **Sidecar lives at `Library/VfxMcpIdentity.json`** — git-ignored by Unity convention. `Library/` deletion is recoverable via the identity recovery path; don't treat it as catastrophic.
- **The old `Editor/Tools/Vfx/VfxConsoleReader.cs` stays in place through phases 3–6.** Phase 3C creates a NEW file at `Editor/Kernel/VfxConsoleReader.cs`. Phase 7 deletes the old one.

## When in doubt
- **Spec ambiguity** → check the decisions log at the bottom of the spec (20 entries, one per fork).
- **API name uncertainty** → grep the embedded `Packages/com.unity.visualeffectgraph/` source via `mcp__jcodemunch__search_symbols` / `search_text`. Never guess API names.
- **Plan task unclear** → the spec section it implements is your authoritative reference. Link back via the spec's section headings.
- **Scope creep tempting** → the memory note at `~/.claude/projects/-Users-pakaya-Documents-GitHub-VFX-MPC-wip/memory/feedback_defer_to_followup_runs.md` applies. Defer to v0.3.1.

## Definition of done
All 13 release-gate criteria green (see spec section "Production-ready release gate"). All 18 test categories pass. Phase 7 cleanup committed. `v0.3.0` tag on the rebuild branch.
