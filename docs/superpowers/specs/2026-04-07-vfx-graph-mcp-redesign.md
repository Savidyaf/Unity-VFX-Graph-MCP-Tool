# VFX Graph MCP Tool — Redesign Spec

**Date:** 2026-04-07
**Revised:** 2026-04-07 *(after two independent reviews: `docs/spec-review-vfx-graph-mcp-redesign.md` + `docs/objective-review-vfx-graph-mcp-redesign.md`)*
**Status:** Approved design, ready for implementation planning
**Branch (planned):** `vfxgraph-rebuild-v0.3`
**Package:** `com.spiralingstudio.mcp.vfxgraph` → version `0.3.0`
**Host:** `com.coplaydev.unity-mcp` (MCPForUnity)
**Targeted Unity packages (version-locked):**
- `com.unity.visualeffectgraph` 17.4.0 (embedded source, maintained as a soft fork)
- `com.unity.render-pipelines.core` 17.4.0 (embedded source, unmodified)

## Goal

Replace the current `manage_vfx` + `manage_vfx_graph` + `inspect_vfx_asset` surface with a generated, version-locked, type-safe, **production-ready** tool set that lets agents build and edit `.vfx` / `.vfxop` / `.vfxoperator` assets — including subgraphs — without working around tool gaps. Personal-use addon; no backwards-compat obligations; one-shot release as `0.3.0`.

## One-paragraph summary

A dev-time generator reflects Unity's curated `VFXLibrary` node descriptors (not raw assembly reflection) and emits a complete typed catalog, JSON↔Unity coercers (including compound slot trees), typed wrappers, and MCP tool schemas as committed C# files. The runtime never reflects — it calls into generated code via `[InternalsVisibleTo]` access patched into the embedded VFX Graph package. A short hand-authored YAML override layer patches Unity's quirks. Identity is a **pure sidecar + structural fingerprint** model that never mutates asset data. The nine grouped MCP tools share a single `VfxResponseShaper` chokepoint that enforces a token-savings policy. A three-part per-transaction health gate (YAML structural diff + compile/error-manager status + console-warning correlation) turns silent divergence into explicit response signals.

## Background

### Current addon surface (all three tools)

The existing addon registers **three** MCP tools, not one:

1. `[McpForUnityTool("manage_vfx")]` in `ManageVFX.cs` — a 47-action mega-tool that also accidentally handles ParticleSystem/LineRenderer/TrailRenderer at `HandleParticleSystemAction`/`HandleLineRendererAction`/`HandleTrailRendererAction`.
2. `[McpForUnityTool("manage_vfx_graph")]` in `ManageVfxGraph.cs:11` — a separate VFX graph editing tool with its own `HandleCommand` dispatcher.
3. `[McpForUnityTool("inspect_vfx_asset")]` in `InspectVFXAsset.cs:7` — an asset introspection tool.

Architecture across all three: action-string dispatchers with manual JSON normalization, alias maps, runtime reflection cache. The ParticleSystem/LineRenderer/TrailRenderer handling duplicates `MCPForUnity`'s host-level `manage_components` coverage and is never mentioned in the gaps doc. Existing tests in `Tests/Editor/` (`VfxActionsTests`, `VfxGraphReflectionCacheTests`, `VfxGraphResultMapperTests`, `VfxInputValidationTests`, `VfxToolContractTests`) test the old architecture and will be replaced, not migrated.

### The gaps doc (`docs/vfx_graph_mcp_tool_gaps.md`)

27 documented issues found while building a real ECS-driven thruster VFX, grouped:
1. **Silent success / wrong state** (#1–8) — type coercion failures, settings/properties confusion, post-save state divergence
2. **Hard failures that should work** (#9–12) — name resolution collisions, registry inconsistency
3. **Missing capability / workarounds** (#13–22) — missing operators, recipes, type-conversion shortcuts
4. **ID instability** (#23, #24) — block IDs reassigned mid-session, node positions reverting
5. **Misc diagnostics** (#25–27) — silent connection drops not surfaced, dropped slot warnings hidden in console, compilation status reports OK while graph is wrong

The "Works well" list at the bottom is the pre-existing known-good path allowlist. **Unlike the original spec, the revised spec does NOT use this list as a test scope limit** — production-ready means full `VFXLibrary` coverage with tests.

### Root cause

The current tools are string-dispatched reflection wrappers. Every action is hand-rolled, type coercion is ad-hoc per handler, the registry is a hardcoded enum, and there's no source of truth shared with Unity's actual VFX type catalog. The new design moves Unity's curated descriptor catalog directly into the codegen pipeline via `InternalsVisibleTo` patches on the embedded package.

## How this design eliminates the gaps doc

| Gap group | Mechanism |
|---|---|
| Name resolution (#9, #10, #16) | Generator walks `VFXLibrary.GetOperators/GetBlocks/GetContexts/GetParameters` — the same source Unity's own add-node UI uses. Single name table; lookup is O(1); collisions resolved at generation time via `Quirks.yaml`, not at runtime. |
| Type coercion (#5, #6) | Generator emits a typed coercer per `VFXSlot` type, walking the recursive slot tree. Compound types (`Vector3`, `Color`, `Transform`, `Sphere`) decompose into child-slot assignments. JSON values that don't fit a slot are rejected at the boundary. |
| Settings vs properties confusion (#8, #18) | Generator separates them at codegen. Typed methods are `set_setting(name, value)` vs `set_property(name, value)`. Sending a setting name to a property method is a structural rejection with the right hint pre-baked. |
| ID instability (#23, #24) | `VfxIdentity` is pure sidecar + structural fingerprint — never mutates the `.vfx` asset. Fingerprint recovery absorbs mid-session reshuffles, save/reload cycles, and editor restarts. |
| Silent connection drops (#4, #26, #27) | Three-part per-transaction health gate: (1) partial YAML diff, (2) compile/error-manager status, (3) console-warning correlation between pre-save and post-compile. All three run at `VfxTransaction.Commit()`. |
| Inconsistent registries (#10) | List endpoints AND create endpoints both read from the same generated catalog, which in turn reads `VFXLibrary` at generation time. |
| Silent invocation exceptions (#2) | The kernel `VfxNodeOps` is the ONLY place that wraps Unity calls in `try/catch (Exception)`. Structural discipline — it cannot be missed. |
| Missing subgraph support (not in gaps doc but critical for production) | New `vfx_subgraph` tool; subgraph operator/block/context are first-class citizens in the catalog and verifier. |

## Architecture

### Component groups

```
┌─────────────────────────────────────────────────────────────┐
│  GROUP A — Generator (dev-time, never ships at runtime)     │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ VfxCatalogGenerator (orchestrator)                   │   │
│  │   ├─ VfxLibraryWalker → CatalogIR                    │   │
│  │   │     (walks VFXLibrary.Get* descriptors,          │   │
│  │   │      NOT raw assembly reflection)                │   │
│  │   ├─ VfxQuirksLoader (Quirks.yaml) → CatalogIR'      │   │
│  │   ├─ VfxSlotTreeBuilder (recursive compound model)   │   │
│  │   └─ Emitters → Editor/Generated/*.g.cs              │   │
│  │       ├─ VfxCatalog.g.cs                             │   │
│  │       ├─ VfxCoercers.g.cs       (leaf + compound)    │   │
│  │       ├─ VfxNodeWrappers.g.cs   (via CreateInstance) │   │
│  │       ├─ VfxSubgraphWrappers.g.cs                    │   │
│  │       ├─ VfxToolSchemas.g.cs                         │   │
│  │       └─ VfxOverrides.g.cs                           │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ committed to git
┌─────────────────────────────────────────────────────────────┐
│  GROUP C — Committed artifacts (generated + hand-authored)  │
│   Editor/Generated/*.g.cs           (generated, read-only)  │
│   Editor/Generated/.catalog-version (generated, sentinel)   │
│   Editor/Generation/Quirks.yaml     (hand-authored)         │
│   Editor/Generation/Hints.yaml      (hand-authored)         │
│   Packages/com.unity.visualeffectgraph/                     │
│     Editor/Unity.VisualEffectGraph.Editor.asmdef            │
│                                     (PATCHED: +one line)    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ consumed at runtime
┌─────────────────────────────────────────────────────────────┐
│  GROUP B — Runtime kernel (~800 lines hand-written)         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ VfxIdentity         sidecar + structural fingerprint │   │
│  │ VfxNodeOps          ONLY layer that touches Unity    │   │
│  │ VfxResponseShaper   single output chokepoint         │   │
│  │ VfxTransaction      intent snapshot + 3-part gate    │   │
│  │ VfxYamlVerifier     partial UnityYAML diff (part 1)  │   │
│  │ VfxCompileGate      compile/error-manager (part 2)   │   │
│  │ VfxConsoleCorrelator  console harvest (part 3)       │   │
│  │ VfxBusyGate         pipeline/compile preconditions   │   │
│  │ VfxConsoleReader    per-session high-water mark      │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  GROUP D — 9 grouped MCP tools (~100–150 lines each)        │
│   vfx_asset      vfx_graph     vfx_node      vfx_block      │
│   vfx_property   vfx_subgraph  vfx_recipe    vfx_batch      │
│   vfx_diag                                                  │
│   Each = [McpForUnityTool], action enum, dispatch switch.   │
│   Schemas come from VfxToolSchemas.g.cs.                    │
└─────────────────────────────────────────────────────────────┘
```

### Group A — Generator

| Component | Responsibility |
|---|---|
| `VfxCatalogGenerator` | Orchestrator. Editor menu item `Tools/VFX MCP/Regenerate Catalog`. On editor load, an `EditorApplication.delayCall` checks `Editor/Generated/.catalog-version` against the live `com.unity.visualeffectgraph` package version; mismatch → console warning (non-blocking) + optional local git pre-commit hook (opt-in). |
| `VfxLibraryWalker` | Walks `VFXLibrary.GetOperators()`, `GetBlocks()`, `GetContexts()`, `GetParameters()`, and subgraph registrations. Uses `VFXModelDescriptor<T>` as the authoritative per-node source (this is what Unity's own UI uses, so it filters out abstract/deprecated/test-only types automatically). Zero raw assembly reflection. |
| `VfxQuirksLoader` | Reads `Editor/Generation/Quirks.yaml`. Validates schema. Merges into IR. |
| `VfxSlotTreeBuilder` | For each node descriptor, instantiates a template model, walks its `VFXSlot` hierarchy recursively, records the compound tree shape for the coercer emitter. Handles leaf types (`float`, `Texture2D`, `GraphicsBuffer`) and compound types (`Vector3` → `.x`/`.y`/`.z`, `Color`, `Transform`, `Sphere`, `AABox`). |
| `CatalogIR` | In-memory model: node descriptors, block descriptors, context descriptors, operator descriptors, subgraph descriptors, attribute descriptors, slot type trees, setting field maps, parameter shapes. Pure data; the only thing emitters consume. |
| `Emitters` | Each writes one `.g.cs` file. After emission, the generator runs a self-compile check; if its own output doesn't compile, the run fails loudly. |

### Group B — Runtime kernel

| Component | Responsibility |
|---|---|
| `VfxIdentity` | Pure sidecar + structural fingerprint. **Never mutates asset data.** Details in the Identity section below. |
| `VfxNodeOps` | The only layer that touches `VFXViewController`, `VFXGraph`, `VFXLibrary.Get*().CreateInstance()`, or `VFXModel.AddChild()`. Calls into generated wrappers + coercers. The only place wrapping `try/catch (Exception)` around Unity API calls. |
| `VfxResponseShaper` | Single output chokepoint. Applies the token-savings policy to every response. |
| `VfxTransaction` | Wraps a single call OR a batch OR an explicit save. Holds the intent snapshot for the three-part health gate at commit time. |
| `VfxYamlVerifier` | Partial UnityYAML reader (~200 lines, version-stable, only understands node identity + slot connections + parent links). Diffs actual vs intent. **Part 1 of the health gate.** |
| `VfxCompileGate` | After save, awaits `VisualEffectResource.CompileOrRuntimeError()`, reads `VFXErrorReporter` status, returns compile errors/warnings. **Part 2 of the health gate.** |
| `VfxConsoleCorrelator` | Harvests console lines between pre-save and post-compile; matches warning patterns (`Remove N linked slot(s)`, `slot type mismatch`, etc.) to specific intent ops to annotate dropped connections with *why*. **Part 3 of the health gate.** |
| `VfxBusyGate` | Precondition-checks `EditorApplication.isCompiling`, `AssetDatabase.IsAssetImportWorkerProcess`, `AssetDatabase.IsAssetImportWorkerProcess`. Every mutating action runs through `VfxBusyGate.EnsureIdle()` first. Fail-fast with `asset_pipeline_busy` + retry-after hint; no internal polling loops. |
| `VfxConsoleReader` | Existing file; adapted so console reads return only new lines since the last successful call within the current editor process. Cursor advances on every successful read; resets on assembly reload. |

### Group C — Committed artifacts (generated + hand-authored)

- `Editor/Generated/VfxCatalog.g.cs` — type registry, name resolution, FQN map, list-* backing arrays
- `Editor/Generated/VfxCoercers.g.cs` — leaf + compound JSON↔Unity coercers walking the slot tree
- `Editor/Generated/VfxNodeWrappers.g.cs` — typed `Create<NodeType>(...)` methods that call `VFXLibrary.Get<T>().First(d => d.modelType == typeof(NodeType)).CreateInstance()`
- `Editor/Generated/VfxSubgraphWrappers.g.cs` — subgraph operator/block/context wrappers
- `Editor/Generated/VfxToolSchemas.g.cs` — JSON schemas for the 9 tools
- `Editor/Generated/VfxOverrides.g.cs` — applied quirks
- `Editor/Generated/.catalog-version` — sentinel: which Unity package version this output was generated from
- `Editor/Generation/Quirks.yaml` — hand-authored quirks
- `Editor/Generation/Hints.yaml` — hand-authored hint templates keyed by error code
- `Packages/com.unity.visualeffectgraph/Editor/Unity.VisualEffectGraph.Editor.asmdef` — **PATCHED** with a single `InternalsVisibleTo` entry (see Embedded VFX Graph Package Ownership)

All `.g.cs` and `.yaml` files are committed. Runtime reads committed files; it never runs the generator.

### Group D — 9 grouped MCP tools

| Tool | Domain |
|---|---|
| `vfx_asset` | Create/list/assign/delete `.vfx` + `.vfxop` + `.vfxoperator` assets. List templates. |
| `vfx_graph` | `get_info`, `save`, `compile`, `compilation_status`, `read_console`, `set_space`, `set_capacity`, `set_bounds`, `set_data_settings`, `discard_changes`, `get_health` *(exposes three-part gate report)*. |
| `vfx_node` | `add`, `remove`, `move`, `duplicate`, `connect`, `disconnect`, `get_setting`, `set_setting`, `get_property`, `set_property`. See Justification for this grouping below. |
| `vfx_block` | `add`, `remove`, `reorder`, `set_activation`, attribute blocks, custom attributes. |
| `vfx_property` | Graph-level exposed properties — `add`, `remove`, `set_value`, `set_exposed`. |
| `vfx_subgraph` | **NEW.** `create` (new `.vfxop`/`.vfxoperator` asset), `add_ref` (place subgraph reference in parent graph), `inline` (expand a subgraph ref in place), `extract` (turn a selection into a subgraph), `get_exposed` (list what a subgraph exposes), `set_override` (override an exposed value at the ref). |
| `vfx_recipe` | Thin scaffold with description `"Recipe support — no recipes registered yet; deferred to v0.3.1"`. One placeholder action `list` returning `[]`. |
| `vfx_batch` | Batched ops with single round-trip and single end-of-batch three-part health gate. Auto-runs compile + returns status by default (can be opted out via `skip_compile: true`). |
| `vfx_diag` | `list_node_types`, `list_block_types`, `list_attributes`, `list_settings`, `list_subgraphs`, `read_console`, `get_warnings`. |

**Justification for `vfx_node` grouping (addressing Review 1 M2 / Review 2 M2):** Node lifecycle, slot configuration, and connection are the same workflow unit from the agent's perspective: "add a node, configure its settings and properties, wire it to other nodes, move on." Splitting connection into its own tool forces the agent to track "which tool do I call next" for what is a single mental step. Error domains are already separated by stable error codes (`slot_type_mismatch`, `property_used_as_setting`, etc.), so conflating them in one tool does not conflate them in error reporting. The 150-line tool class is not hard to navigate.

## Identity model — sidecar + structural fingerprint, zero asset mutation

**Critical constraint:** No writes to `VFXModel`, `VFXContext.label`, `ScriptableObject.name`, or any other asset-resident field. The `.vfx` asset stays pristine and artist-facing diffs are unaffected by the addon.

### Why not the obvious alternatives

| Alternative | Why rejected |
|---|---|
| Write token into `m.label` | `VFXModel` has no `label` field. Only `VFXContext.cs:71` does. Per Review 1 B1. |
| Write token into `((ScriptableObject)m).name` | Works mechanically — `VFXObject : ScriptableObject` and `ScriptableObject.name` is writable + serialized as `m_Name`. But pollutes `.vfx` YAML diffs with `m_Name: n_a3f9\|Lerp` entries that appear in artist version control. Rejected per Review 2 B2. |
| Patch a new hidden metadata field into `VFXModel` | Viable (we already maintain a soft fork), but adds maintenance burden on every Unity bump. Unnecessary given the sidecar approach is cleaner. |
| Use instance IDs | Unstable mid-session. Gap #23. Reason the current tool is broken. |

### The chosen model

**Structural fingerprint** (64-bit):

```
fingerprint = FNV1a64(
    graphGuid           || "|" ||
    parentFingerprint   || "|" ||  // "" for top-level nodes
    nodeTypeFQN         || "|" ||
    siblingIndexAmongSameType || "|" ||
    slotShapeHash       || "|" ||
    settingShapeHash
)
```

Where:
- `parentFingerprint` — recursively computed for the parent; top-level contexts use `""`
- `siblingIndexAmongSameType` — rank of this node among siblings sharing the exact same FQN type under the same parent. Stable across saves because `VFXModel.m_Children` is a serialized `List<VFXModel>` with stable order.
- `slotShapeHash` — hash of `(slot name, slot FQN)` pairs for all direct child slots. Does NOT include slot values.
- `settingShapeHash` — hash of `(setting name, setting type FQN)` pairs. Does NOT include setting values.

**Token format:** `n_<5-hex-of-fingerprint>` for nodes, `b_<5-hex>` for blocks, `ctx_<5-hex>` for contexts, `sg_<5-hex>` for subgraph refs. 5 hex chars = 20 bits = ~1M unique, more than enough for a 500-node graph. If a 5-hex collision ever happens within one asset, the sidecar records the full 64-bit fingerprint and disambiguates with a 2-char suffix (`n_a3f9a`).

**Sidecar:** `Library/VfxMcpIdentity.json`
```
{
  "version": 1,
  "assets": {
    "<graph asset GUID>": {
      "<token>": "<full 64-bit fingerprint hex>",
      ...
    }
  }
}
```

`Library/` is git-ignored and Unity may regenerate it on reimport. Recovery path handles this.

### Operations

**Mint (at create time):**
1. Create the node via `VFXLibrary.Get*().First(d => …).CreateInstance()` and `AddVFXModel`/`AddChild`
2. Compute the fingerprint from the live graph state
3. Generate short token (`n_<5-hex>` taking the top 20 bits)
4. Resolve collisions within the asset by appending suffix bytes from the full fingerprint
5. Write to the sidecar
6. Return the token

**Resolve (at every subsequent call):**
1. Look up `token → full-fingerprint` in the sidecar
2. Walk the graph, recomputing fingerprints on candidates
3. Return the first fingerprint match
4. If no exact match → RECOVER

**Recover:**
1. Walk the graph matching on `(nodeTypeFQN, parentFingerprint)` only — ignore sibling index and shape hashes
2. Among candidates, score by proximity of slot/setting shape to the sidecar fingerprint
3. If exactly one clear winner → update the sidecar, emit `warnings: [{code:"identity_drifted", token, old_fingerprint, new_fingerprint}]`, continue
4. If multiple matches or none → throw `VfxIdentityException("node_lost", { token, asset_guid })` with recovery hint

**Flush policy:**
- `VfxTransaction.Commit()` flushes all pending sidecar writes
- `AssemblyReloadEvents.beforeAssemblyReload` flushes pending writes before domain reload
- A crash between mint and commit leaves the sidecar slightly out-of-date; the recovery path heals it on next resolve

### What this survives

- Mid-session instance-ID reshuffles (gap #23)
- Domain reloads
- Save+reload cycles
- Unity editor restarts
- `Library/` deletion (recovery rebuilds the sidecar entry)
- User manually editing the graph in the VFX Graph editor (recovery absorbs sibling-index drift; identity_drifted warning surfaces)

### What this does NOT survive (known limitations, documented and accepted)

- **Structural doppelgänger:** Node deleted externally, then a different node of the *exact* same type added at the same parent with the same sibling index, same slot shape, and same setting shape → fingerprint is byte-identical, so the sidecar silently resolves to the new node. The agent sees no error but is operating on a different object than it thinks. This is fundamental to any non-mutating identity scheme: without writing into the asset, there's no way to distinguish two structurally-identical nodes. **Mitigation:** when the agent later asks for slot/setting VALUES and they don't match the agent's mental model, it will notice. The three-part health gate catches most real-world cases because the replacement node's slot wiring is almost never identical to the original's.
- **Cross-Unity-project move:** moving a `.vfx` asset to a different Unity project loses the sidecar. On first operation in the new project, every token resolves via the recovery path using `(type, parent fingerprint)`. For simple graphs this works; for ambiguous multi-match graphs, recovery may fail with `ambiguous_token`. Documented for completeness; not common in practice.

## Embedded VFX Graph Package Ownership

**Framing:** This addon is a **soft fork** of `com.unity.visualeffectgraph`. The fork boundary is exactly one line.

### Required patches

**Patch 1 — `InternalsVisibleTo` on VFX Graph editor assembly.** `.asmdef` files don't accept `InternalsVisibleTo` directly; the attribute lives in a companion `AssemblyInfo.cs` file inside the assembly's directory. Add a new file at `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")]
```

~2 lines total, no modification to the existing `Unity.VisualEffectGraph.Editor.asmdef`. This grants the addon's editor assembly access to `VFXLibrary`, `VFXModel`, `VFXContext`, `VFXBlock`, `VFXOperator`, `VFXSlot`, `VFXViewController`, and friends without runtime reflection.

**No other patches.** Identity is sidecar-based, so no hidden metadata field is needed.

### Maintenance policy

1. **On every Unity package bump**, the patch line must be preserved. A pre-commit hook (opt-in local, not CI) verifies the file exists before allowing commits to the embedded package directory.
2. **Failure-on-drift policy:** if the `AssemblyInfo.cs` is missing at assembly load, the generator fails at startup with a clear message: `"Missing InternalsVisibleTo patch at Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs. Re-apply from docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md."`
3. **Compatibility assertion:** generator self-check runs `VFXLibrary.GetOperators().Any()` at startup. If zero results, the patch didn't take and the generator refuses to run.
4. **Merge burden estimation:** the patched file is a standalone 2-line file, never touched by upstream Unity. Merge burden is effectively zero; the file just needs to still exist after an upgrade.

### Failure-policy when catalog and package version drift

- `Editor/Generated/.catalog-version` stores the package version string
- On editor load, if the file's recorded version doesn't match the live `package.json` version → console warning *(not blocking)*
- On first tool call after a drift is detected, the tool emits a one-time `catalog_stale` warning in the response

## Subgraph support

VFX Graph's subgraph primitives are first-class authoring constructs. Verified presence:

- `VFXSubgraphOperator.cs:66` (operator subgraph, `.vfxoperator` asset)
- `VFXSubgraphContext.cs:12` (context subgraph, `.vfxop` asset)
- `VFXSubgraphBlock.cs:9` (block subgraph, in-place authoring)
- `VFXSubgraphUtility`, `SubgraphInfos` in compilation

### Catalog coverage

`VfxLibraryWalker` walks subgraph descriptors the same way it walks operator/block/context descriptors. Subgraph-specific metadata captured:
- Exposed slot list (inputs)
- Exposed property list (configurable from ref)
- Output slot list
- Subgraph asset GUID for ref binding

### `vfx_subgraph` tool actions

| Action | Description |
|---|---|
| `create` | Create a new `.vfxop` or `.vfxoperator` asset at a given path. Seeds with a minimal skeleton. |
| `add_ref` | Add a subgraph reference to a parent graph. Requires `parent_graph`, `subgraph_asset` (path to .vfxop/.vfxoperator), `position`. Returns a `sg_<hash>` token. |
| `inline` | Expand a subgraph reference in-place, dropping the ref and inlining its content into the parent. Destructive but recoverable via `discard_changes`. |
| `extract` | Select a subset of nodes in a parent graph, move them into a new subgraph asset, replace with a reference. Returns (new_asset_path, new_ref_token). |
| `get_exposed` | List a subgraph's exposed properties (their names, types, defaults). Used before `set_override`. |
| `set_override` | On a subgraph reference token, override an exposed property value. Uses the same coercer machinery as `vfx_property.set_value`. |

### Editing inside a subgraph

A subgraph asset IS a `.vfxop` or `.vfxoperator` file. The agent edits its contents using the same 9 tools against the subgraph asset directly — `vfx_asset.open`, `vfx_node.add`, etc. No special "edit inside subgraph" action is needed.

### Subgraph verification

The three-part health gate covers subgraphs naturally:
- YAML diff reads the subgraph asset OR the parent graph asset depending on which was mutated
- Compile gate awaits compilation of the asset that was saved; parent graphs compile their subgraph dependencies automatically
- Console correlator scopes warnings to the asset being verified

**Subgraph-specific invariants the verifier must check:**
- Parent graph's subgraph ref still resolves after save
- Exposed slot bindings on the ref match the subgraph's current exposed list
- If a subgraph adds/removes an exposed property, parent refs get `warnings: [{code:"subgraph_interface_changed", token, delta}]`

## Three-part health gate

The original spec's single YAML verifier is one of three checks. All three run at `VfxTransaction.Commit()`:

### Part 1 — YAML structural diff

`VfxYamlVerifier` (partial UnityYAML reader, ~250 lines) reads the saved asset and diffs against the transaction's intent snapshot.

**Detects:**
- Missing nodes (intent said add, YAML says no)
- Dropped connections (intent said connect, YAML says no edge)
- Drifted slot bindings (connection exists but target slot changed)
- Identity fingerprint drift (recovery path usage)

### Part 2 — Compile / error-manager status

`VfxCompileGate` awaits VFX compilation and reads `VFXErrorReporter`.

**Detects:**
- Shader compilation errors
- Type mismatches that pass YAML but fail compile
- Subgraph interface mismatches
- Invalid context chains

**Implementation note:** VFX compilation may be asynchronous. `VfxCompileGate` uses `VisualEffectResource.CompileOrRuntimeError()` + polling `resource.runtimeErrors.Any()` with a bounded timeout (target: 2s for a 500-node graph; configurable).

### Part 3 — Console correlation

`VfxConsoleCorrelator` captures the console high-water mark before save and reads all new lines after compile. It matches known Unity warning patterns to intent operations:

- `"Remove N linked slot(s) that couldn't be deserialized from <slot>"` → correlate to connect ops targeting that slot path → emit `connection_dropped_at_save` with the actual slot name from the warning
- `"VFXInlineOperator ... no type assigned"` → correlate to the most recent `vfx_node.add type:VFXInlineOperator` → emit `hint` referencing the `m_Type` setting
- `"Shader error in ..."` → correlate to compile-part failures

**What the correlator does NOT do:** interpret warnings it doesn't recognize. Unknown warnings are passed through as `warnings: [{code:"vfx_console_raw", line:"…"}]` without op-correlation.

### Response shaping

The health gate's output is folded into every commit response:

```json
{
  "added": [ ... ],
  "modified": [ ... ],
  "warnings": [
    {"code":"connection_dropped_at_save", "op_index":2, "slot":"_Scale.x", "reason":"Cannot deserialize slot type mismatch"},
    {"code":"identity_drifted", "token":"n_a3f9", "old_fingerprint":"...", "new_fingerprint":"..."}
  ],
  "health": {
    "yaml_diff": "clean" | "warnings" | "errors",
    "compile": "ok" | "errors" | "timeout",
    "console": "ok" | "warnings",
    "compile_ms": 127,
    "yaml_bytes": 48320
  }
}
```

The `health` object is only present when `verbose: true`. Otherwise just `warnings` (non-empty only on problems) appears.

## Token-savings policy

Every response goes through `VfxResponseShaper` which applies these rules:

1. **Compact tokens as the only IDs the agent sees.** `n_a3f9`, `b_bx12`, `ctx_c9a2`, `sg_d7f1`. Never 64-bit fingerprints or instance IDs.
2. **One verbosity boolean: `verbose: true|false`, defaulting to `false`.** Terse mode returns shape (topology with names + tokens + types, no slot values, no setting values, no `health` object). Verbose returns everything plus per-op traces in batch responses plus the `health` object. No named modes, no field projection.
3. **Type aliasing in responses.** Short names by default (`Lerp`, `SetAttribute`); the full FQN appears once in a `$types` legend at the top of any response that uses ambiguous names.
4. **Mutation responses return diffs, not state.** `add_node` returns `{added:[{token, type, context}]}`. `connect_nodes` returns `{added:[…], dropped:[…]}` where `dropped` comes from the three-part gate.
5. **No echoed input. No version stamps. No `ok: true` wrapper.** The error envelope is the only structured wrapper.
6. **Pagination on `vfx_diag.list_*` enumerations.** Default page size 50, with a continuation token. Backed by the static arrays in the generated catalog — free.
7. **Console reads return only NEW lines since the last call.** Per-session high-water mark.
8. **Scoped graph queries (NEW).** `vfx_graph.get_info` accepts `filter: {context_token?, node_types?, around_token?, depth?}` so agents can query sub-slices of large graphs without paying the full-graph cost. Default (no filter) is full graph, which is fine at the 500-node budget.
9. **Batch verbose mode includes a `correlation_id`** so agents can reference the batch in follow-up calls, plus per-op `trace: [{op_index, duration_ms, result}]`.

## Performance budgets

**Target graph size: 500 nodes.** Graphs larger than 500 nodes should be refactored into subgraphs; this is enforced as a convention, not as a hard limit (no error is thrown at 501 nodes, but performance may degrade beyond ~1000).

| Operation | p95 target (excluding Unity save/compile unless noted) |
|---|---|
| Single-call mutation (`vfx_node.add`) | < 100 ms |
| Single-call read (`vfx_graph.get_info`, default filter, 500 nodes) | < 150 ms |
| Single-call read (scoped filter) | < 50 ms |
| Batch commit (20 ops, 500-node graph) | < 300 ms (+ Unity save/compile) |
| Identity token resolve | < 5 ms |
| Identity recovery walk | < 20 ms |
| Part 1: YAML structural diff (500 nodes) | < 100 ms |
| Part 2: Compile gate (500 nodes, cache warm) | < 500 ms Unity + 10 ms overhead |
| Part 3: Console correlation | < 20 ms |
| Token-savings shaping | < 10 ms |
| Catalog regeneration (full walk) | < 30 s (dev-time, one-off) |

**Measurement:** Performance tests are part of test category 11 (new). They run against a synthetic 500-node graph under `Assets/Tests/Performance/`. Budgets are asserted; a regression fails the test.

**When budgets won't be met:** if the real graph exceeds 500 nodes OR Unity's save/compile latency dwarfs the budget, the tool continues to work but responses include `warnings: [{code:"perf_budget_exceeded", target, actual}]` so the agent knows the cost shape changed. No hard failure.

## Busy-editor coordination

**Principle: fail fast, never poll internally.** The agent decides when to retry.

Every mutating tool action precondition-checks via `VfxBusyGate.EnsureIdle()`:

| Check | Fails with |
|---|---|
| `EditorApplication.isCompiling` | `asset_pipeline_busy` + hint `"Wait for Unity to finish compiling (~Xs remaining per recent history), then retry."` |
| `AssetDatabase.IsAssetImportWorkerProcess` | `asset_pipeline_busy` + hint `"Asset import in progress. Retry in 1-2s."` |
| Domain reload pending (`AssemblyReloadEvents.beforeAssemblyReload` latched) | `asset_pipeline_busy` + hint `"Domain reload imminent. Retry after reload."` |
| Graph currently compiling (`resource.isCompiling` on the target) | `asset_pipeline_busy` + hint `"Graph compile in progress. Retry in 1-2s."` |
| Target asset locked by version control | `asset_locked` + hint `"Check out the asset before mutation."` |

Read-only actions (`get_info`, `list_*`, `read_console`) skip the busy gate to avoid blocking the agent on pure introspection.

**Retry semantics:** the error envelope includes `retry_after_hint_ms` (integer, best-effort) based on recent-history averaging of the blocking operation.

## Data flows

### Flow 1 — Dev-time generation

```
Tools/VFX MCP/Regenerate Catalog
   ↓
VfxCatalogGenerator
   ↓
VfxLibraryWalker.Walk()
   ↓
   ├─ VFXLibrary.GetOperators()  → operator descriptors
   ├─ VFXLibrary.GetBlocks()     → block descriptors
   ├─ VFXLibrary.GetContexts()   → context descriptors
   ├─ VFXLibrary.GetParameters() → parameter descriptors
   └─ subgraph descriptors       → subgraph operator/block/context descriptors
   ↓
VfxSlotTreeBuilder.Build(descriptors) → compound slot trees
   ↓
VfxQuirksLoader.Apply(Quirks.yaml) → CatalogIR'
   ↓
Emitters write Editor/Generated/*.g.cs
   ↓
Self-compile check (fails the run if output doesn't compile)
   ↓
Runtime sanity check: VFXLibrary.GetOperators().Any()
   (fails if InternalsVisibleTo patch is missing)
   ↓
Console: "Catalog regenerated: N ops, M blocks, K contexts, S subgraphs, T slot types"
   ↓
Update Editor/Generated/.catalog-version
```

### Flow 2 — Single tool call (corrected API)

```
Agent → vfx_node {action:"add", graph:"…", type:"Lerp", context:"$ctx_init"}
   ↓
MCPForUnity host → VfxNode tool class
   ↓
VfxBusyGate.EnsureIdle()  ← precondition check
   ↓
VfxTransaction.Begin(scope=single-call)
   ↓
1. VfxCatalog.Resolve("Lerp")
      → descriptor for UnityEditor.VFX.Operator.Lerp
      (override resolves collision with UIElements.Experimental.Lerp)
2. VfxIdentity.Resolve("$ctx_init")
      → live VFXContext (walked from sidecar fingerprint)
3. VfxCoercers.Coerce(params)
      → typed args (none for plain add)
4. VfxNodeOps.AddOperator(graph, descriptor, pos):
      4a. VFXOperator op = (VFXOperator)descriptor.CreateInstance();
      4b. controller.AddVFXModel(pos, op);        // operator attaches to VFXGraph root
      // For blocks under a context, the flow is different:
      //    VFXBlock blk = (VFXBlock)blockDescriptor.CreateInstance();
      //    context.AddChild(blk, index);          // blocks belong to their context
      // For contexts under a graph:
      //    VFXContext ctx = (VFXContext)ctxDescriptor.CreateInstance();
      //    controller.AddVFXModel(pos, ctx);      // contexts attach like operators
5. VfxIdentity.Mint(op)
      → compute fingerprint → token "n_a3f9" → write sidecar
      (NO asset mutation)
6. Transaction records intent: {add, type:Lerp, token:n_a3f9, parent:$ctx_init}
   ↓
VfxTransaction.Commit()
   ↓
7. AssetDatabase.SaveAssetIfDirty(graph)
8. Three-part health gate:
      8a. VfxYamlVerifier.Verify(intent)
      8b. VfxCompileGate.AwaitAndCheck(graph)
      8c. VfxConsoleCorrelator.Harvest(pre_snapshot)
   ↓
VfxResponseShaper.Shape(diff, warnings, verbose=false)
   ↓
Agent ← {added:[{token:"n_a3f9", type:"Lerp"}]}
```

### Flow 3 — Batch (single round trip, single commit)

```
Agent → vfx_batch {ops:[
  {tool:"vfx_node",  action:"add",     type:"Lerp",              context:"$ctx_init", as:"@n_lerp"},
  {tool:"vfx_node",  action:"add",     type:"VFXInlineOperator", as:"@n_in"},
  {tool:"vfx_node",  action:"connect", from:"@n_in.o",           to:"@n_lerp.a"},
  {tool:"vfx_block", action:"add",     type:"SetAttribute",      parent:"$ctx_init", as:"@b_set"},
]}
   ↓
VfxBusyGate.EnsureIdle()
   ↓
VfxTransaction.Begin(scope=batch)
   ↓
For each op, run Flow 2 steps (1)–(6) with health gate DEFERRED.
Symbolic refs resolve against:
  1. Batch-local aliases:   @name  → minted in this batch
  2. Pre-existing tokens:   $name  → looked up in sidecar
COLLISION is fail-fast: if @name resolves to both a batch-local and a pre-
existing token, throw batch_ref_collision BEFORE running the batch.
   ↓
VfxTransaction.Commit()  ← ONE save
   ↓
Three-part health gate (ONCE, union of all intents)
   ↓
VfxResponseShaper.Shape({added:[…], dropped:[…], warnings:[…]}, verbose=false)
   ↓
Agent ← {added:[…], dropped:[]}  (or with warnings if divergence)
```

### Flow 4 — Identity (mint, resolve, recover) — sidecar-only

```
MINT (at add):
  op = descriptor.CreateInstance()
  controller.AddVFXModel(pos, op)  (or parent.AddChild(op))
  fingerprint = ComputeFingerprint(op)  // recursive up the parent chain
  token = "n_" + TopHexBits(fingerprint, 20)  // 5 hex
  resolve collisions within asset by appending 2 more hex chars if needed
  sidecar[graphGuid][token] = fingerprint
  return token

RESOLVE:
  record = sidecar[graphGuid][token]   // load if not in memory
  for each candidate in WalkAllModels(graph):
    if ComputeFingerprint(candidate) == record:
      return candidate
  → not found → RECOVER

RECOVER:
  loose matches = [c for c in WalkAllModels(graph) if
                   c.GetType().FullName == record.type AND
                   ComputeFingerprint(c.GetParent()) == record.parentFp]
  if exactly one → update sidecar + emit identity_drifted warning, return
  if zero or >1 → throw VfxIdentityException("node_lost", details)
```

### Flow 5 — Three-part health gate

```
Transaction.intent = list of ops
   ↓
AssetDatabase.SaveAssetIfDirty(graph)
   ↓
PART 1: YAML structural diff
  yaml = File.ReadAllText(graphPath)
  parsed = PartialUnityYamlReader.Parse(yaml)
  for each intent:
    locate node by recomputing fingerprint
    compare expected slot/connection shape
  → yaml_diff = {dropped_connections, drifted_slots, missing_nodes}
   ↓
PART 2: Compile gate
  resource = GetVisualEffectResource(graph)
  await resource.CompileOrRuntimeError() with 2s bounded wait
  errors = VFXErrorReporter.GetErrors(resource)
  → compile = {errors, warnings, ms}
   ↓
PART 3: Console correlation
  new_lines = VfxConsoleReader.ReadSincePre(transaction.pre_snapshot)
  for each known warning pattern:
    match to intent op by slot path / type name / fingerprint hash
  → console = {correlated_warnings, raw_warnings}
   ↓
compose response:
  warnings = yaml_diff.warnings ∪ compile.warnings ∪ console.correlated_warnings
  if yaml_diff.missing_nodes ∨ compile.errors:
    upgrade to error envelope(intent_diverged | compile_error)
  else:
    return shape + warnings (+ health if verbose)
```

### Flow 6 — Subgraph lifecycle

```
Agent → vfx_subgraph {action:"create", path:"Assets/VFX/Thruster/Smoke.vfxop"}
   ↓
VfxNodeOps.CreateSubgraphAsset(path, kind="context")
  → AssetDatabase.CreateAsset(minimalSkeleton, path)
  → sidecar entry registered
Agent ← {created: {asset: "Assets/VFX/Thruster/Smoke.vfxop"}}

Agent → vfx_subgraph {action:"add_ref", parent_graph:"Assets/VFX/Parent.vfx",
                      subgraph_asset:"Assets/VFX/Thruster/Smoke.vfxop",
                      position:[0,0]}
   ↓
VfxNodeOps.AddSubgraphRef(parent, subAsset, pos)
  → descriptor = VFXLibrary.GetContexts().First(d => d.modelType == typeof(VFXSubgraphContext))
  → ref = descriptor.CreateInstance()
  → // bind the asset reference via the generated subgraph wrapper
  → VfxSubgraphWrappers.BindAsset(ref, subAsset)  // uses the correct concrete asset type per kind
  → controller.AddVFXModel(pos, ref)
  → VfxIdentity.Mint(ref) → "sg_d7f1"
Agent ← {added: [{token: "sg_d7f1", type: "VFXSubgraphContext", subgraph: "…"}]}

Agent → vfx_subgraph {action:"set_override", token:"sg_d7f1",
                      property:"intensity", value:2.5}
   ↓
VfxNodeOps.SetSubgraphOverride(ref_token, prop_name, value)
  → look up ref → find exposed property → coerce value → set via SerializedObject
Agent ← {modified: [{token: "sg_d7f1", property: "intensity"}]}
```

## Error handling

### Envelope

```json
{
  "error": {
    "code":    "stable_snake_case",
    "message": "human-readable one-liner",
    "hint":    "actionable next step",
    "details": { "field": "...", "expected": "...", "got": "..." },
    "retry_after_hint_ms": 1500   // optional, set by VfxBusyGate
  }
}
```

### Stable error codes

```
Validation
  unknown_action, unknown_node_type, unknown_block_type,
  unknown_attribute, unknown_setting, unknown_property,
  name_collision, setting_used_as_property,
  property_used_as_setting, slot_type_mismatch,
  slot_value_invalid, missing_required_param,
  unsupported_pipeline

Identity
  node_lost, ambiguous_token, parent_context_invalid,
  identity_drifted     // warning only, not error

Batch
  batch_ref_collision  // @name clashes with pre-existing token

Asset
  asset_not_found, asset_pipeline_busy, asset_locked

Subgraph
  subgraph_not_found, subgraph_interface_changed,
  subgraph_cycle_detected

Transaction
  intent_diverged      // verifier upgrades to error
  compile_error        // compile gate upgrades to error
  compile_timeout      // compile gate exceeded 2s budget
  vfx_exception        // Unity threw — catches gap #2

Performance
  perf_budget_exceeded // warning only

Catalog
  catalog_stale        // warning only
```

### Where errors are raised and caught

| Layer | Throws | Catches |
|---|---|---|
| Generated catalog | `VfxValidationException(code, details)` | — |
| Generated coercers | `VfxValidationException(code, details)` | — |
| `VfxIdentity` | `VfxIdentityException(code, details)` | — |
| `VfxBusyGate` | `VfxBusyException(code, details, retry_after_hint_ms)` | — |
| `VfxNodeOps` (only Unity-touching layer) | rethrows VfxException, **wraps Unity exceptions** as `vfx_exception(inner.message, top-5-stack-lines)` | catches `Exception` around every Unity API call |
| Tool dispatcher (9 tool classes) | — | safety-net: catches `VfxException` and any escaped `Exception`, converts to envelope |
| Three-part health gate | never throws | emits warnings OR upgrades to `intent_diverged` / `compile_error` / `compile_timeout` |

### Hint table (`Editor/Generation/Hints.yaml`)

```yaml
hints:
  setting_used_as_property:
    template: "'{name}' on {type} is a setting, not a property. Use vfx_node.set_setting."
  name_collision:
    template: "'{name}' is ambiguous. Candidates: {candidates}. Use the qualified name."
  slot_type_mismatch:
    template: "Slot '{slot}' expects {expected}; got {got}. {coerce_hint}"
  node_lost:
    template: "Token {token} no longer resolves. Re-query with vfx_graph.get_info."
  asset_pipeline_busy:
    template: "Unity is busy ({reason}). Retry in ~{retry_after_hint_ms}ms."
  batch_ref_collision:
    template: "Batch alias @{name} collides with pre-existing token ${name}. Rename the alias."
  subgraph_interface_changed:
    template: "Subgraph {path} added/removed exposed properties. Parent refs may need updating."
  compile_timeout:
    template: "VFX compilation exceeded {budget_ms}ms. Graph may be too large; consider splitting into subgraphs."
  vfx_exception:
    template: "Unity raised: {inner}. This is usually a {category} issue."
```

## Testing

### Scope (production-ready, not works-well-allowlist)

The revised spec removes the "Works well list only" limit from the original. Test scope is full catalog coverage.

### Test categories

| # | Category | What it protects | New file |
|---|---|---|---|
| 1 | Codegen determinism | Same Unity input always produces byte-identical `.g.cs` output. | `VfxCatalogGeneratorTests.cs` |
| 2 | Generator self-check | Generator's own emitted output must compile. | (in generator) |
| 3 | Catalog completeness | Generated catalog count matches `VFXLibrary.Get*().Count()` exactly. Flags silent failure of InternalsVisibleTo patch. | `VfxCatalogCompletenessTests.cs` |
| 4 | Type-catalog round-trip | Every reflectable `VFXOperator`/`VFXBlock`/`VFXContext`/`VFXParameter`/subgraph descriptor can be created via wrappers, queried, removed. | `VfxCatalogRoundTripTests.cs` |
| 5 | Coercer round-trip (leaf + compound) | Every slot type's coercer round-trips: `JsonToken` → Unity value → `JsonToken` structurally equal. **Compound types are explicit: Vector3 from [x,y,z], Color from hex, Transform from nested obj, etc.** | `VfxCoercerTests.cs` |
| 6 | Identity (sidecar + fingerprint) | Mint/resolve/recover including: mid-session drift, sibling reshuffle, `Library/` deletion, assembly reload. Token never contains instance ID. | `VfxIdentityTests.cs` |
| 7 | Token-savings policy | Terse mode strips verbose-only fields. Type legend correct. Pagination cursors round-trip. Console reader respects high-water mark. Scoped filter works on 500-node test graph. | `VfxResponseShaperTests.cs` |
| 8 | Partial YAML verifier | Partial reader handles every node/connection shape from (4). Diff produces correct warnings on synthetic divergence. | `VfxYamlVerifierTests.cs` |
| 9 | Compile gate | Hand-crafted graphs with known compile errors produce `compile_error`; clean graphs produce `ok`; infinite-compile case produces `compile_timeout`. | `VfxCompileGateTests.cs` |
| 10 | Console correlator | Hand-crafted console lines match correct intent ops; unknown lines pass through as raw. | `VfxConsoleCorrelatorTests.cs` |
| 11 | Performance budgets | 500-node synthetic graph asserts all performance targets. | `VfxPerformanceTests.cs` |
| 12 | Busy-editor coordination | Every mutating action fails fast with `asset_pipeline_busy` when compilation/import is simulated busy. | `VfxBusyGateTests.cs` |
| 13 | Override layer | Quirks resolve name collisions correctly. Hints fire with correct substitution. | `VfxOverrideTests.cs` |
| 14 | Subgraph lifecycle (E2E) | Create `.vfxop` → create parent `.vfx` → add subgraph ref → set_override → save → verify → modify subgraph → reload parent → interface_changed warning fires. | `VfxSubgraphTests.cs` |
| 15 | End-to-end smoke (all categories) | Build a real particle system end-to-end using the new tool surface. Covers all 9 tools in sequence. | `VfxSmokeTests.cs` |
| 16 | Discard-changes | Dirty the graph, discard, assert last-saved state. | `VfxDiscardChangesTests.cs` |
| 17 | Rollback / abort-on-first-failure | Abort mid-batch; verify no save; verify `rolled_back: false` semantics; retry from op N works. | `VfxBatchAbortTests.cs` |
| 18 | No-reflection enforcement | Static analyzer test scans kernel code; fails if it finds `typeof(VFX*).GetMethod\|GetField\|GetProperty` calls. | `VfxNoReflectionTests.cs` |

### Explicitly OUT of test scope (deferred to v0.3.1 "fix & polish")

- Recipe execution tests (scaffold only in v0.3.0)
- Tests replicating gaps-doc bugs red-then-green (per memory note; the rebuild's health gate makes most gaps structurally impossible so no red phase exists)

### Test infrastructure

- NUnit + Unity Test Runner under `Tests/Editor/`
- Test assets under `Assets/Tests/VfxFixtures/` including:
  - A 500-node synthetic graph for performance tests
  - Hand-crafted YAML for verifier tests
  - Hand-crafted `.vfxop` subgraph for subgraph tests
  - Graph with known compile errors for compile-gate tests
- Cleaned up after each test class

## Production-ready release gate

These criteria must all be green for v0.3.0 to ship:

1. **Test categories 1–18 all pass.**
2. **Generated catalog matches `VFXLibrary.Get*().Count()` exactly** (category 3, guards the InternalsVisibleTo patch).
3. **Zero runtime reflection** enforced by category 18 static analyzer test.
4. **Performance budgets met** on the 500-node synthetic graph (category 11).
5. **Three-part health gate** passes on the smoke E2E (category 15) with zero warnings.
6. **Subgraph lifecycle** E2E (category 14) passes including `subgraph_interface_changed` detection.
7. **Identity sidecar** survives all drift scenarios in category 6.
8. **Override layer populated** for every ambiguity found during phase 4 dogfooding. `Quirks.yaml` is not empty.
9. **`Hints.yaml`** has a template for every error code in the codes list.
10. **The 9 tools are all discoverable** via the MCP tool listing with typed schemas sourced from `VfxToolSchemas.g.cs`.
11. **Embedded package patch** (`AssemblyInfo.cs`) is committed and the sanity check passes at startup.
12. **CHANGELOG entry** forward-links `docs/vfx_graph_mcp_tool_gaps.md` and the two review docs.
13. **Code review** from subagent-driven review passes (phase 4a, max effort, see Migration).

## Migration & rollout

### Phase ordering with subagent decomposition

Each phase is a commit or commit cluster on `vfxgraph-rebuild-v0.3`. Old code coexists with new code (different tool names) until phase 7. Phases 3 and 4 use the `subagent-driven-development` skill for parallel execution.

| # | Phase | What lands | Subagent decomposition | Verification |
|---|---|---|---|---|
| 1 | **Embedded package patch** | New `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` with `InternalsVisibleTo` line. Documentation in CHANGELOG. | single-agent | Addon editor assembly compiles with access to `VFXLibrary` (smoke check: one test that calls `VFXLibrary.GetOperators().Any()`) |
| 2 | **Generator scaffold** | `VfxLibraryWalker`, `CatalogIR`, `VfxSlotTreeBuilder`, **one** emitter (`VfxCatalog.g.cs`). Editor menu item. Self-compile check. Startup sanity check for the patch. | single-agent | Generator runs; emitted file compiles; determinism test passes; catalog completeness count matches (category 3) |
| 3 | **Generator full coverage + runtime kernel** | **Parallel subagents** (see below) | **4 parallel subagents** — each gets an isolated contract | Test categories 4, 5, 6, 7, 8, 9, 10 pass |
| 4 | **All 9 MCP tools at once** | **Parallel subagents** (see below) | **3 parallel subagents** — each owns 3 tools | Test categories 12, 13, 14, 15, 16, 17 pass |
| 4a | **Subagent code review (max effort)** | Spawn the `superpowers:code-reviewer` subagent at **max effort** to review the entire phase 3 + 4 output against: this spec, the two external reviews, the gaps doc, and CLAUDE.md coding standards. | single reviewer subagent | Review approved; any blocking findings resolved in follow-up commits |
| 5 | **Override layer fill-in + perf tests** | Populate `Quirks.yaml` with quirks discovered during phases 3–4. Populate `Hints.yaml` for every error code. Perf budget test suite. Busy-editor test suite. | single-agent | Test categories 11, 13 pass |
| 6 | **Production-ready validation** | Run the full release gate (criteria 1–13). Fix any remaining issues. | single-agent | All release gate criteria green |
| 7 | **Cleanup** | Delete old `ManageVFX.cs`, `ManageVfxGraph.cs`, `InspectVFXAsset.cs`, all 10 Particle/Line/Trail files, all old `Vfx*` runtime files, all 5 old test files. **Files kept:** `VfxConsoleReader.cs` (adapted), `VfxAssemblyInternals.cs` (addon→tests InternalsVisibleTo declaration, unrelated to the VFX Graph package patch). Update `CHANGELOG.md`, bump `package.json` to `0.3.0`. | single-agent | All new tests still green; no references to deleted files |

### Phase 3 subagent decomposition (4 parallel subagents)

Each subagent gets an isolated contract and produces non-overlapping files:

**Subagent 3A — Generator full coverage**
- Input: Phase 2 scaffold + the catalog data model
- Output: `VfxCoercers.g.cs`, `VfxNodeWrappers.g.cs`, `VfxSubgraphWrappers.g.cs`, `VfxToolSchemas.g.cs`, `VfxOverrides.g.cs` (all emitters working)
- Tests: categories 4, 5

**Subagent 3B — Identity + verifier**
- Input: Phase 2 catalog
- Output: `VfxIdentity.cs`, `VfxYamlVerifier.cs` (partial YAML reader)
- Tests: categories 6, 8

**Subagent 3C — Compile gate + console correlator + busy gate**
- Input: Phase 2 catalog
- Output: `VfxCompileGate.cs`, `VfxConsoleCorrelator.cs`, `VfxBusyGate.cs`
- Tests: categories 9, 10

**Subagent 3D — Transaction + response shaper + NodeOps stubs**
- Input: Phase 2 catalog, contracts from 3A/3B/3C (interfaces declared upfront)
- Output: `VfxTransaction.cs`, `VfxResponseShaper.cs`, `VfxNodeOps.cs` with stub methods to be filled in phase 4
- Tests: category 7

**Interface locking (critical for parallelism):** Before phase 3 starts, the main agent commits a single contracts file `Editor/Tools/Vfx/VfxKernelContracts.cs` containing:
- `IVfxIdentity`, `IVfxYamlVerifier`, `IVfxCompileGate`, `IVfxConsoleCorrelator`, `IVfxBusyGate` interfaces (method signatures for 3B/3C outputs)
- `IVfxTransaction`, `IVfxResponseShaper` interfaces (3D outputs)
- `IVfxNodeOps` with full method signatures (3D stubs against this, 3A's generated wrappers called through this)
- `CatalogIR` public type from phase 2 (all subagents read it)
- Response envelope types (used by 3D and all tools in phase 4)

All four subagents consume the same contracts file; none of them modify it. 3A implements the generated wrappers that 3D's NodeOps will call. 3B/3C implement the kernel services. 3D wires them together via the interfaces. At phase 3 end, `VfxNodeOps` is still stubbed — it gets filled during phase 4 when tools need actual mutation calls. This prevents merge conflicts and ensures the four outputs compose cleanly.

### Phase 4 subagent decomposition (3 parallel subagents)

**Subagent 4A — vfx_asset, vfx_graph, vfx_diag**
- Input: Phase 3 kernel
- Output: `VfxAsset.cs`, `VfxGraph.cs`, `VfxDiag.cs`

**Subagent 4B — vfx_node, vfx_block, vfx_property**
- Input: Phase 3 kernel
- Output: `VfxNode.cs`, `VfxBlock.cs`, `VfxProperty.cs`

**Subagent 4C — vfx_subgraph, vfx_recipe, vfx_batch**
- Input: Phase 3 kernel
- Output: `VfxSubgraph.cs`, `VfxRecipe.cs`, `VfxBatch.cs`

Each subagent owns three tool files exclusively; no overlap. The test suites land together under `Tests/Editor/Tools/`.

### Phase 4a — max-effort code review

Spawn the `superpowers:code-reviewer` subagent at max effort. The review prompt explicitly includes:

- Link to this spec (`docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`)
- Link to both external reviews (`docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/objective-review-vfx-graph-mcp-redesign.md`)
- Link to `docs/vfx_graph_mcp_tool_gaps.md`
- Link to CLAUDE.md
- Explicit instruction: "validate that every gap in the gaps doc and every finding in the two external reviews has been structurally addressed, not just acknowledged. Cross-check the generated catalog against `VFXLibrary.Get*()` counts. Flag any kernel code that does runtime reflection. Flag any tool class that echoes input or uses verbose envelopes. Flag any identity write to `m.label` / `m.name` / asset fields."

Review produces a findings document under `docs/superpowers/specs/`. Blocking findings must be resolved before phase 5 begins.

### Branch strategy

- New branch `vfxgraph-rebuild-v0.3` off current branch's HEAD
- Old branch `vfxgraph-package-v0.2` is tagged before rebuild starts
- Rebuild branch becomes the shipping branch after phase 7 lands

### Regeneration trigger (Unity package bumps)

1. Editor loads → sentinel mismatch → console warning (non-blocking)
2. Verify `AssemblyInfo.cs` patch is still present (pre-commit hook helps)
3. Run `Tools/VFX MCP/Regenerate Catalog`
4. Review the `Editor/Generated/*.g.cs` diff in the PR/commit
5. Update `Quirks.yaml` if type names changed
6. Run tests; commit

### Rollback story

Each phase is a revertable commit. Phase 1 (the embedded package patch) is the only one that touches a git-ignored-by-convention area; document the patch clearly in CHANGELOG so it's not accidentally reverted. Worst-case rollback is `git reset` to the pre-rebuild tag.

## Out of scope (deferred to v0.3.1 "fix & polish")

- **Recipes** (gaps #20–22) — `vfx_recipe` ships as an empty scaffold with a clear description. Actual recipes are authored in v0.3.1.
- **`get_warnings` separation** beyond what the three-part gate already covers.
- **Particle System / Line Renderer / Trail Renderer code** — deleted entirely; `manage_components` covers them at the host level.
- **Backwards-compat aliases** for old tool names — hard cut.
- **Package registry publish** — personal use only.
- **Gap-doc-specific regression tests** — the new architecture makes most gaps structurally impossible; the next run writes any residual red→green tests after dogfooding.
- **Recipes for KillWhen / smoke_trail / add_sample_buffer_indexed_by** — v0.3.1.

## Forward links

- Next run picks up: recipes, gap-doc residuals found during dogfooding, any v0.3.0 limitations discovered post-release.
- The gaps doc (`docs/vfx_graph_mcp_tool_gaps.md`) stays as input for the next run.
- Both external reviews (`docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/objective-review-vfx-graph-mcp-redesign.md`) stay as context for the next run.
- CHANGELOG entry for `0.3.0` forward-links all three docs.

## Decisions log

| # | Decision | Rationale |
|---|---|---|
| 1 | **Hybrid codegen: walk `VFXLibrary.Get*()` descriptors + YAML override layer** | `VFXLibrary` is the curated, filtered, non-deprecated source of truth Unity's own UI uses. Raw reflection produces a superset with abstract/deprecated types that fail at `CreateInstance()`. |
| 2 | **Pure sidecar + structural fingerprint identity, zero asset mutation** | `VFXModel.label` doesn't exist (Review 1 B1); `ScriptableObject.name` pollutes YAML diffs (Review 2 B2); sidecar + fingerprint sidesteps both concerns and survives all known drift scenarios. |
| 3 | **9 grouped MCP tools, including new `vfx_subgraph`** | `vfx_node` grouping justified by agent workflow unity; `vfx_subgraph` new because subgraphs have their own identity, verification, and lifecycle semantics. |
| 4 | **One `verbose` boolean for verbosity** | Simplest API. Batch verbose includes per-op trace + correlation_id. No field projection, no named modes. |
| 5 | **Kill Particle/Line/Trail renderer code entirely** | MCPForUnity's `manage_components` covers them at the host level. |
| 6 | **Recipe scaffold now with "deferred to v0.3.1" description, recipes themselves deferred** | Avoids agent confusion over empty tool; registration plumbing lands now so v0.3.1 doesn't restart MCPForUnity. |
| 7 | **Hard cut, no backwards-compat for any of the three old tools** | Personal use, version-locked, no migration cost. All three (`manage_vfx`, `manage_vfx_graph`, `inspect_vfx_asset`) deleted in phase 7. |
| 8 | **Sentinel staleness check is a console warning + opt-in local pre-commit hook** | Non-blocking, no CI dependency, still catches drift before commits. |
| 9 | **Partial UnityYAML reader for verifier (~250 lines), not full** | Version-stable, minimum surface for the diff. |
| 10 | **`discard_changes` = `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` + VFXViewController re-fetch, NOT `Undo.PerformUndo`** | Safer; doesn't affect global undo stack. |
| 11 | **Separate `Quirks.yaml` and `Hints.yaml` files** | User preference. |
| 12 | **Phases 3 + 4 collapsed with subagent decomposition; phase 4a = max-effort code review** | User requested "split work carefully among sub agents, don't cut any corners." 4+3 subagents with interface-locked contracts. |
| 13 | **Three-part health gate (YAML diff + compile/error-manager + console correlation), not YAML alone** | Review 2 H1: YAML diff doesn't catch compile-time or async-sanitization issues. |
| 14 | **500-node target with subgraph refactoring for larger graphs** | User preference: refactor is the convention, not raw scaling. |
| 15 | **Embedded VFX Graph package is a soft fork with one patch line** | Required for `InternalsVisibleTo` to hit internal types without runtime reflection. Documented maintenance policy + failure-on-drift check. |
| 16 | **Subgraphs are first-class in catalog, verifier, and tools** | `VFXSubgraphOperator/Context/Block` are real production primitives. `vfx_subgraph` owns their domain. |
| 17 | **Batch symbolic refs use `@name` (batch-local) vs `$name` (pre-existing) prefixes; collision is fail-fast** | Review 1 H4: unambiguous syntax + fail-fast at batch parse time. |
| 18 | **`vfx_batch` auto-compiles by default (opt-out with `skip_compile:true`)** | Matches the "transaction commit" semantics. Explicit opt-out for read-only batches. |
| 19 | **`VfxBusyGate` precondition-checks; no internal retry loops** | Fail-fast + `retry_after_hint_ms` lets the agent decide. |
| 20 | **Full catalog test coverage, NOT just the gaps-doc "Works well" allowlist** | Production-ready release gate requires real coverage. Memory note's concern about codifying buggy paths is satisfied because the new architecture makes most gaps structurally impossible. |
