# Spiraling Studio MCP VFX Graph Tools

`com.spiralingstudio.mcp.vfxgraph` is a URP-first VFX Graph extension package for [MCP for Unity](https://github.com/CoplayDev/unity-mcp). It exposes Unity's Visual Effect Graph as a set of structured MCP tools so AI agents can build and edit VFX assets programmatically — graph topology, blocks, properties, subgraph references, batch operations, and asset lifecycle.

## Purpose

The package gives an AI agent (or any MCP client) direct, contract-bounded access to:

- Create, list, assign, and delete `.vfx` / `.vfxblock` / `.vfxoperator` assets
- Add / remove / move / duplicate / connect / disconnect nodes inside a graph
- Manipulate `VFXContext` settings, `VFXBlock` settings, and `VFXParameter` exposed properties
- Apply multi-tool batches in a single end-of-batch health-gate transaction
- Read graph topology with stable, persistent identity tokens
- Diagnose the graph via catalog inspection, console reads, and per-graph warnings
- All without writing C#, all through structured `vfx_*` MCP tool calls

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ MCP client (Claude Code, custom agent, etc.)                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │ JSON over MCP transport
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│ MCP for Unity host (coplaydev/unity-mcp)                        │
│ - Discovers [McpForUnityTool]-tagged classes                    │
│ - Routes execute_custom_tool → tool's HandleCommand(JObject)    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│ This package — 9 tool surfaces in Editor/Tools/                 │
│ ┌──────────────┐ ┌─────────────┐ ┌──────────────┐               │
│ │ VfxAssetTool │ │VfxGraphTool │ │ VfxNodeTool  │  ...           │
│ └──────┬───────┘ └──────┬──────┘ └──────┬───────┘               │
│        │ each tool's HandleCommand dispatches to private        │
│        │ helpers; mutating actions open a transaction scope     │
│        ▼                                                        │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Editor/Kernel — the locked contract surface                 │ │
│ │   IVfxNodeOps · IVfxIdentity · IVfxYamlVerifier ·           │ │
│ │   IVfxCompileGate · IVfxConsoleCorrelator · IVfxBusyGate ·  │ │
│ │   IVfxTransaction · IVfxResponseShaper                      │ │
│ │   (defined in VfxKernelContracts.cs — never modified ad-hoc)│ │
│ └─────────────────────────────────────────────────────────────┘ │
│        │                                                        │
│        ▼                                                        │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Editor/Generation — build-time catalog generator            │ │
│ │   Walks VFXLibrary.GetOperators/GetContexts/GetBlocks once  │ │
│ │   per Unity version, emits VfxCatalog.g.cs +                │ │
│ │   VfxCoercers.g.cs + VfxNodeWrappers.g.cs into              │ │
│ │   Editor/Generated/                                         │ │
│ └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────┬───────────────────────────────────────┘
                          │ uses public + InternalsVisibleTo APIs
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│ com.unity.visualeffectgraph (soft-fork)                         │
│ Two patches:                                                    │
│   - Editor/AssemblyInfo.cs — InternalsVisibleTo grant for the   │
│     addon Editor + Tests assemblies                             │
│   - Editor/VfxMcpKernelHelpers.cs — bridge for                  │
│     VisualEffectResource (lives in UnityEditor.VFXModule and    │
│     can't receive an InternalsVisibleTo grant from a user pkg)  │
└─────────────────────────────────────────────────────────────────┘
```

### Three architectural invariants

1. **Zero runtime reflection on `UnityEditor.VFX.*` / `UnityEngine.VFX.*` types** in `Editor/Kernel/` and `Editor/Generation/`. The catalog generator runs at build time only. The kernel uses generated wrappers and the soft-fork bridge — never `Type.GetType()` or `MethodInfo.Invoke` on Unity VFX types at runtime. Enforced by `VfxNoReflectionTests`.
2. **Locked kernel contracts.** `Editor/Kernel/VfxKernelContracts.cs` is the single source of truth for all kernel interfaces. Changes happen in dedicated single-commit "contracts bumps" — never as part of feature work.
3. **Three-part health gate at every transaction commit.** Every mutation runs through `VfxTransaction.Commit()`, which executes (a) YAML structural diff via a partial UnityYAML reader, (b) compile gate via `VFXGraph.CompileAndUpdateAsset`, and (c) console correlation via a `Application.logMessageReceived` ring buffer. Mutations that pass all three are reported as healthy; failures surface as structured warnings in the response.

### Identity and tokens

Models are addressed by **persistent tokens** stored in a sidecar file at `Library/VfxMcpIdentity.json` (NOT in the asset itself — zero asset mutation). Tokens are minted from a structural fingerprint (FNV-1a 64, lower 20 bits) so the same model recovers the same token across domain reloads, restarts, and re-clones. The token format is `<prefix>_xxxxx` where `xxxxx` is 5 hex chars and the prefix is **per-category**:

| Category | Prefix | Example |
|---|---|---|
| `VFXContext` (Spawn / Init / Update / Output) | `ctx_` | `ctx_a3f21` |
| `VFXBlock` (any block type) | `b_` | `b_9b332` |
| `VFXOperator` (Add, Multiply, Position, …) | `n_` | `n_3c890` |
| `VFXParameter` (exposed property) | `n_` | `n_7e215` |
| `VFXSubgraphRef` / fallback | `n_` | `n_5d440` |

Mint logic: `Editor/Kernel/VfxIdentity.cs` `PrefixFor(VFXModel)` (lines 143–151). Single mint call site at `VfxIdentity.Mint`.

### Response shape contract

All tool responses go through `VfxKernelContainer.Shaper`:

- **Mutating success** (via `Shape` / `ShapeMutation`): the per-action mutation payload (e.g. `{"added": [{"token": "...", "type": "..."}]}`) plus optional `warnings[]` plus optional `health` (verbose-only — terse mode strictly omits the `health` key per the release-gate token-savings rule).
- **Read-only success** (via `ShapeRead`): the action's payload directly.
- **Error** (via `ShapeError`): `{"success": false, "error": {"code": "...", "message": "...", "hint": "...", "details": {...}, "retry_after_hint_ms": 1234}}`. The top-level `success: false` discriminator is a defensive workaround for an upstream `coplaydev/unity-mcp` Python normalizer bug — see CHANGELOG `[Unreleased]` § "Known upstream blockers".
- **Honest "not implemented" stub** (via `ShapeRead`): `{"state": "not_implemented", "hint": "<actionable explanation>", ...optional shape-compat keys}`.

There is **no `tool_version` field** and **no top-level `ok: true` discriminator** on success responses. Success vs error is determined by the presence/absence of an `error` key.

## The 9 tool surfaces

Every tool is registered via `[McpForUnityTool("vfx_*", AutoRegister = true, Group = "vfx")]` and exposes a single `HandleCommand(JObject @params)` entry point that switches on the `action` parameter. All graph-mutation tools take a `graph` parameter (the asset path); asset-creation tools (`vfx_asset.create`, `vfx_subgraph.create`) take a `path` parameter instead.

### `vfx_asset` — asset lifecycle (4 actions)

- `create` — `path` → creates `.vfx` / `.vfxblock` / `.vfxoperator` on disk via the soft-fork bridge. Bypasses transactions (asset creation is not a graph mutation).
- `list` — enumerates all `VisualEffectAsset` / `VisualEffectSubgraphBlock` / `VisualEffectSubgraphOperator` instances in the project as `[{guid, path, kind}]`.
- `delete` — `path` → bypasses transactions (deletion is not a graph mutation; the post-delete health gate would have nothing to verify).
- `assign` — `path`, `gameObject` → assigns the VFX asset to a `VisualEffect` component on the named scene GameObject (creating the component if missing).

`vfx_asset.delete` is **intentionally non-batchable**: the post-delete YAML verifier in a batch's commit phase would run against a missing asset. Surfaced via `code="not_implemented"` if attempted in a `vfx_batch.commit`.

### `vfx_graph` — graph-level introspection and meta-operations (11 actions)

- `get_info` — `graph` → `{graph_path, child_count, system_count, system_names[], space, bounds_setting_mode, update_mode}`. `space` and `bounds_setting_mode` are walked across top-level contexts; `update_mode` is the documented constant `"per_system"` (VFX Graph has no graph-level update mode in Unity 6000.4).
- `save` — `graph` → `AssetDatabase.SaveAssetIfDirty`. Records no intent ops; commit is a no-op verification.
- `compile` — `graph` → triggers `VFXGraph.CompileAndUpdateAsset` and returns `{Ok, DurationMs, Errors[]}`. Result is cached per asset path for `compilation_status`.
- `compilation_status` — `graph` → returns the cached last compile result with `cached: true`. If no compile has run yet, returns `{state: "no_compile_yet", hint: ...}`.
- `read_console` — high-water-mark ring-buffer read of recent log lines (per-tool mark; persists across calls).
- `get_health` — `graph` → opens an empty no-op `VfxTransactionScope.SingleCall`, commits, and returns the three-part gate's `health` block. No mutation; pure inspection.
- `set_space` / `set_capacity` / `set_bounds` — convenience wrappers that auto-find the right target context/data and mutate it. `set_capacity` writes `VFXBasicInitialize.GetData() as VFXDataParticle`'s `capacity` field; `set_bounds` writes `boundsMode` (defaulting to `Manual`) and returns a hint about following up with `vfx_node.set_property` for center/size; `set_space` writes the context's `space` property. Returns `state="needs_explicit_target"` when zero or multiple candidates exist.
- `set_data_settings` — `graph`, `name`, `value` → conditional dispatch through the `@graph` synthetic-token branch in `NodeOps.SetSetting`. Returns `state="not_implemented"` for non-graph-level settings.
- `discard_changes` — `graph` → re-imports the asset, discarding in-memory changes.

### `vfx_node` — node manipulation (11 actions)

- `list` — `graph` (+ optional `page_size`, `offset`) → paginated `nodes[]` of `{token, type, x, y, parent_token, category, block_index}`. Walks `graph.children` and recurses one level into `VFXContext.children` for blocks. The dominant read-side surface.
- `add` — `graph`, `type` (+ optional `x`, `y`) → `{added: [{token, type}]}`. Returns `error.code="type_not_found"` (with `details.type_fqn` and `details.category`) if the FQN is not in the catalog.
- `remove` — `graph`, `token` → `{removed: [{token}]}`.
- `move` — `graph`, `token`, `x`, `y` → `{moved: {token, x, y}}`. Calls the first-class `IVfxNodeOps.MoveNode` and records intent op `Kind="move"`.
- `duplicate` — `graph`, `token` (+ optional `offset_x`, `offset_y`) → `{duplicated: {source, token, type}}`. Handles VFXBlock by recovering the parent context and routing through `AddBlock`.
- `connect` / `disconnect` — `graph`, `from_token`, `from_slot`, `to_token`, `to_slot` → `{connected/disconnected: {...}}`. Detects context-to-context calls and routes through `VFXContext.LinkTo`/`UnlinkTo` with the integer flow-port index (accepts `"o"`, `"i"`, `"flow"`, `""`, or any non-negative integer string as flow index 0).
- `set_setting` / `set_property` — `graph`, `token`, `name`, `value` → writes the corresponding field/slot. `set_property` coerces the value through `VfxCoercerDispatch.Coerce(value, slot.property.type)`, which handles `JObject {x,y,z}` / `JArray [x,y,z]` / hex strings for Vector2/3/4 and Color.
- `get_setting` / `get_property` — `graph`, `token`, `name` → `{token, name, value}`.

### `vfx_block` — block manipulation (6 actions)

- `add` — `graph`, `parent_token`, `type` (+ optional `index`) → `{added: [{token, type, parent_token}]}`.
- `remove` — `graph`, `token` → `{removed: [{token}]}`.
- `reorder` — `graph`, `token`, `new_index` → `{reordered: {token, new_index}}`. **Atomic on failure**: out-of-bounds index throws cleanly without leaving the block in an orphaned state.
- `set_activation` — `graph`, `token`, `active` → toggles `block.activationSlot.value` (the `_vfx_enabled` bool slot per `VFXBlock.activationSlotName`).
- `set_attribute` — `graph`, `token`, `attribute_name`, `value` → records a `SetSetting("attribute", name)` then a `SetProperty("_" + Capitalize(name), value)` against the block. Targets the canonical `VFXBlockSetAttribute.GenerateLocalAttributeName` slot path.
- `list_attributes` — read-only stub returning `state="not_implemented"`. Use `vfx_diag.list_attributes` for the catalog.

### `vfx_property` — exposed parameter manipulation (4 actions)

- `add` — `graph`, `type` (+ optional `x`, `y`) → creates a new `VFXParameter` of the given .NET type.
- `remove` — `graph`, `token` → removes the parameter.
- `set_value` — `graph`, `token`, `value` → writes the parameter's value via the public `parameter.value` property (which maps to `outputSlots[0].value` for input parameters; the canonical slot's `property.name` is `"o"`, not `"value"`).
- `set_exposed` — `graph`, `token`, `exposed` → toggles the `m_Exposed` `[VFXSetting]` field on `VFXParameter`.

### `vfx_subgraph` — subgraph reference manipulation (6 actions)

- `create` — `path` → creates a `.vfxblock` or `.vfxoperator` asset on disk via the soft-fork bridge.
- `add_ref` — `graph`, `subgraph` → adds a `VFXSubgraphRef` to the graph pointing at the given subgraph asset.
- `set_override` — `graph`, `token`, `name`, `value` → writes an override on a subgraph reference's input slot.
- `get_exposed` — `graph`, `token` → currently returns the canonical `state="not_implemented"` envelope. Real exposed-input enumeration is deferred.
- `inline` / `extract` — both throw `NotImplementedException("v0.3.1")` and surface as `code="not_implemented"`. Deferred to a future release.

### `vfx_recipe` — recipe catalog (1 action — execution deferred)

- `list` — returns 4 canonical recipe entries: `ecs_buffer_particles`, `simple_spawn_particles`, `gpu_event_chain`, `particle_strip_trail`. Each entry has `name` + `description`. **Recipe execution** (instantiating a recipe template into a graph) is deferred to v0.3.2; calling `create`/`instantiate` returns `code="unknown_action"`.

### `vfx_batch` — multi-tool batch dispatch (1 action)

- `commit` — `graph`, `ops[]` → executes a sequence of operations across multiple tools inside a single `VfxTransactionScope.Batch`. Each op is `{tool, action, ...per-op params}`. The dispatcher resolves the tool name via a compile-time switch (no runtime reflection on Unity VFX types) and calls the tool's static `ApplyInTransaction(opParams, scope)` method via reflection on the addon's own classes (allowed and tested by `VfxNoReflectionTests`). The three-part health gate runs once at the end of the batch. Tools that don't support batching surface `code="not_implemented"` for the offending op.

Allowed tool names in `ops[].tool`: `vfx_asset` (assign only), `vfx_graph` (save, set_data_settings), `vfx_node`, `vfx_block`, `vfx_property`, `vfx_subgraph`. Read-only tools (`vfx_diag`, `vfx_recipe`) cannot participate in batches.

### `vfx_diag` — catalog inspection and diagnostics (8 actions, all read-only)

- `list_node_types` / `list_block_types` / `list_contexts` — paginated catalogs walked at codegen time from `VFXLibrary.GetOperators` / `GetBlocks` / `GetContexts`.
- `list_attributes` — paginated list of built-in VFX attribute names (47 entries: `position`, `velocity`, `lifetime`, `color`, `alpha`, etc.). Real catalog (no longer a stub).
- `list_settings` — paginated `[{type_fqn, settings[]}]` listing every `VFXModel` subtype's `[VFXSetting]`-attributed field names. Catalog walker uses build-time field reflection on the catalog types only (does not violate the no-runtime-reflection rule).
- `list_subgraphs` — paginated `[{fqn, kind}]` listing of `VFXSubgraphOperator` and `VFXSubgraphBlock` types.
- `read_console` — same ring-buffer read as `vfx_graph.read_console` but with its own per-tool mark.
- `get_warnings` — paginated `warnings[]` (currently always empty; full implementation deferred to a future release once `IVFXErrorReporter.GetDirtyModelErrors` is reachable via a non-reflection surface).

## Pipeline support

URP-only. The package's create/inspect paths use `URP`-named templates and `VFXPlanarPrimitiveOutput`. HDRP technically works in VFX Graph but is not exercised by this package's tests.

## Soft-fork patches

Two files added to `com.unity.visualeffectgraph` in this project:

- `Editor/AssemblyInfo.cs` — `[InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")]` and `Tests` grants. Lets the addon assembly call `internal` methods on `VFXContext`, `VFXLibrary`, `VFXGraph.children`, `VFXSystemNames`, etc. without runtime reflection.
- `Editor/VfxMcpKernelHelpers.cs` — compile-time bridge for `VisualEffectResource.GetResourceAtPath` (which lives in `UnityEditor.VFXModule` and cannot receive an `InternalsVisibleTo` grant from a user package), plus `LoadGraphFromAsset`, `CreateVfxAsset`, `CreateVfxSubgraphBlock`, `CreateVfxSubgraphOperator`. All four are zero-reflection wrappers compiled inside the VFX editor assembly itself.

These patches must be re-applied after any `com.unity.visualeffectgraph` upgrade.

## Installation

1. Add `com.unity.visualeffectgraph` 17+ and `com.unity.render-pipelines.universal` 17+ to your project.
2. Apply the two soft-fork patches in `Packages/com.unity.visualeffectgraph/Editor/` (see above).
3. Add this package to your project (embedded under `Packages/com.spiralingstudio.mcp.vfxgraph` or via Git URL).
4. Start MCP for Unity and reconnect your client. The 9 `vfx_*` tools should appear in the custom-tools resource (`mcpforunity://custom-tools`).

## Testing

EditMode tests live in `Tests/Editor/`, organized by surface area:

- `Catalog/` — codegen output validation, coercer round-trips, override layer
- `Generation/` — walker / emitter / slot-tree builder unit tests
- `Kernel/` — busy gate, compile gate, console reader, identity sidecar, response shaper, structural fingerprint, transaction, YAML verifier, no-reflection guard, type-not-found mapping, context-flow connect, slot coercion
- `Tools/` — per-tool action coverage, smoke test exercising all 9 tools end-to-end, performance budgets against a 500-node synthetic graph, plus regression tests for every cluster fixed in the v0.3.1 E2E hardening pass

Run from the Unity Test Runner UI, or via MCP:
```
mcp__UnityMCP__run_tests mode=EditMode assembly_names=["com.spiralingstudio.mcp.vfxgraph.Editor.Tests"]
```

The package's CI workflow at `.github/workflows/unity-editmode-tests.yml` runs the same assembly on every PR.

## End-to-end production-readiness sweep

The package ships with a project-local skill at `.claude/skills/vfx-mcp-e2e-test/` that drives a structured E2E sweep against a live Unity instance via MCP. The skill defines 6 sequential phases (discovery, asset lifecycle, graph build, mutations, batch + recipes + subgraph, save/compile/health/cleanup) with one row per action in a structured JSON report. Re-run after any major change to catch contract regressions and silent-success bugs. See the skill's `SKILL.md` for full procedure.

## Known constraints

- **Composite VFX struct types** (`UnityEditor.VFX.Position`, `DirectionType`, `Vector` with coordinate-space metadata) are not yet in the coercer dispatch table. Plain `Vector3` works end-to-end via `VfxCoercerDispatch`; the VFX-specific wrappers require additional dispatch entries plus a default-coordinate-space design decision (deferred to v0.3.2).
- **Unity VFX internals are reflection-driven within Unity itself** and can change across Unity versions without notice. The package targets Unity 6000.4 / VFX Graph 17. The catalog regenerates from each Unity version's `VFXLibrary` so catalog drift is auto-detected.
- **Custom attribute API availability depends on VFX Graph 17+** internal APIs. Older versions may not expose the required serialized properties.
- **Two upstream `coplaydev/unity-mcp` host bugs** corrupt error envelopes on the wire. The package's `VfxResponseShaper` includes a defensive workaround (top-level `success: false` discriminator). See the CHANGELOG `[Unreleased]` § "Known upstream blockers" for the full root-cause analysis and the patches that need to land upstream for fully clean wire envelopes.

## Troubleshooting

- **Tool returns `state="not_implemented"`** — read the `hint` field; it points at the alternative path. Common cases: `vfx_subgraph.inline`/`extract` (deferred), `vfx_subgraph.get_exposed` (deferred), `vfx_block.list_attributes` (use `vfx_diag.list_attributes` instead).
- **Tool returns `error.code="vfx_busy"`** — the editor is compiling or in a domain reload. Sleep `error.retry_after_hint_ms` and retry. Do NOT call `mcp__UnityMCP__refresh_unity` during a session — it invalidates in-memory tokens.
- **Tool returns `error.code="node_lost"`** — the token doesn't resolve to any model. Possible causes: the node was removed since the token was minted, the sidecar at `Library/VfxMcpIdentity.json` was deleted, or the asset was deleted out from under us. Re-run `vfx_node.list` to recover fresh tokens.
- **`vfx_asset.delete` returns success but the asset is still on disk** — should not happen after the v0.3.1 E2E pass. If observed, file a regression with the asset path and Unity version.
- **Token format looks wrong** (`ctx_xxxxx` instead of `n_xxxxx`) — this is the canonical scheme. Tokens use per-category prefixes; see the Identity section above. The CHANGELOG `[Unreleased]` § Documentation has the full per-category table.

## See also

- **CHANGELOG**: [`CHANGELOG.md`](CHANGELOG.md) — release history including the full v0.3.1 E2E hardening pass
- **Spec doc** for the v0.3.1 E2E sweep: [`docs/superpowers/specs/2026-04-09-vfx-mcp-v0.3.1-e2e-production-ready-design.md`](../../docs/superpowers/specs/2026-04-09-vfx-mcp-v0.3.1-e2e-production-ready-design.md)
- **Phase 4a review** with disposition table: [`docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md`](../../docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md)
- **Phase 6 release gate** criteria: [`docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md`](../../docs/superpowers/specs/2026-04-07-rebuild-phase6-release-gate.md)
- **MCP for Unity host**: [github.com/CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
