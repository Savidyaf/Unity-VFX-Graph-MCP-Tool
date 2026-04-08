# Agent Review: VFX Graph MCP Rebuild Plan

**Reviewed:** `docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md`

## Verdict

`Not execution-ready yet.`  
Good direction, but fix `B1` and `B2` before using this as the source plan for implementation. `H1` and `H2` should also be resolved first if the goal is low-ambiguity agent execution.

## Findings

- `B1` Phase 6 order is impossible.
  Refs: `Phase 6 / Task 6-1`, `Phase 6 / Task 6-2`
  Problem: `6-1` requires all 18 test categories green, including `Smoke E2E`, before `6-2` creates `VfxSmokeTests.cs`.
  Impact: release gate cannot pass in the listed order.
  Fix: move `6-2` before `6-1`, or create smoke tests earlier in phase 4/5.

- `B2` `VfxConsoleReader` has two incompatible migration stories.
  Refs: `Phase 3 / Task 3C-2b`, `Phase 7 / Task 7-1`
  Problem: `3C-2b` creates a new `Editor/Kernel/VfxConsoleReader.cs`; `7-1` later says to `git mv` the legacy file to that same path.
  Impact: file collision, ambiguous history, and inconsistent cleanup steps.
  Fix: choose one path only:
  `A)` create fresh in phase 3, delete legacy in phase 7, or
  `B)` move legacy in phase 3 and adapt in place.

- `H1` The plan is not normalized to the current agent/tool runtime.
  Refs: `Task 4 / Step 1-4`, `Task 5`, `Task 6-1`
  Examples:
  `mcp__UnityMCP__run_tests` vs current `run_tests`
  `mcp__UnityMCP__read_console` vs current `read_console`
  `mcp__UnityMCP__execute_menu_item` with `menu_item` vs current `execute_menu_item` with `menu_path`
  `Use the Read tool` vs current `ReadFile`
  `find ... "*.asmdef"` despite current agent shell/file rules
  Impact: later agents will waste turns translating plan instructions before doing work.
  Fix: rewrite all tool invocations to current tool names/params and replace shell discovery steps with repo/file tools.

- `H2` Release claims conflict with deferred public surface.
  Refs: `Goal`, `Phase 4a / review criteria #10`, `Phase 4 / Task 4C-1`, `Phase 4 / Task 4C-2`
  Problem: plan claims `production-ready` + `first-class subgraph support`, but `vfx_subgraph.inline` and `vfx_subgraph.extract` are explicitly deferred with `NotImplementedException("v0.3.1")`, while `vfx_recipe` is scaffold-only.
  Impact: product claim, review gate, and shipped API surface disagree.
  Fix: either remove deferred actions from the `0.3.0` tool surface or narrow the release claim/review gate to match shipped behavior.

- `H3` The `runtime never reflects` contract is internally inconsistent.
  Refs: `Architecture`, `Task 3C-2b`, `Task 4B-3`, `Task 4-COORD`, `Task 7-2 / CHANGELOG`
  Problem:
  top-level architecture says runtime never reflects;
  `3C-2b` reflects into `UnityEditor.LogEntries`;
  `4B-3` sets parameter values via reflection on the output slot;
  `4-COORD` says batch dispatch uses reflection on tool class names;
  changelog says `zero runtime reflection`.
  Impact: misleading contract and easier future review drift.
  Fix: restate the rule precisely, e.g. `no runtime reflection on UnityEditor.VFX internals except documented non-VFX exceptions`, and align plan + changelog to that wording.

- `M1` The plan is not self-contained enough for low-token agent execution.
  Refs: `Reviews consumed`, `Phase 4a`, `Task 5-1`, `Task 5-3`, `Task 6-1`
  Problems:
  referenced review docs are absent:
  `docs/spec-review-vfx-graph-mcp-redesign.md`
  `docs/objective-review-vfx-graph-mcp-redesign.md`
  `Task 5-1` references non-existent `task 4A-X`
  perf and release validation depend on external spec-only sections (`Performance Budgets`, `Production-ready release gate`)
  Impact: extra context loading, dead links, and agent ambiguity.
  Fix: inline the needed criteria/budgets into this plan, fix broken task ids, and either restore or remove the missing review-doc references.

## Minimal Plan Patch Set

1. Reorder phase 6 so smoke tests exist before the release gate runs.
2. Resolve the `VfxConsoleReader` migration strategy into one unambiguous path.
3. Normalize all tool instructions to the current agent/MCP interface.
4. Remove or fully implement deferred public actions for `0.3.0`.
5. Rewrite the reflection rule so it matches the actual allowed exceptions.
6. Inline the external criteria this plan depends on and fix stale references.

## Short Positive Note

The plan is materially stronger than the earlier spec:
`sidecar + fingerprint identity`, `subgraph support`, `health gate`, `busy gate`, and `full release validation` are all good upgrades.
The remaining problems are mostly execution-shape and contract-alignment issues, not a bad core direction.
