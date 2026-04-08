# Objective Review: VFX Graph MCP Redesign Spec

**Reviewed spec:** `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`  
**Cross-checked against:** `docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/vfx_graph_mcp_tool_gaps.md`, `docs/MCP-VFX-Tool-Limitations.md`, `Packages/com.spiralingstudio.mcp.vfxgraph`, `Packages/com.unity.visualeffectgraph`, `Packages/com.unity.render-pipelines.core`

## Verdict

This is a strong redesign direction for a controlled `0.3.0` rebuild, but it is **not yet a complete production-ready spec**. It reads more like a high-quality architecture proposal for a version-locked core rewrite than a fully closed implementation spec for a "100% production ready" replacement.

The design has real strengths: it removes stale package surface, centralizes response shaping, replaces ad-hoc runtime behavior with generated catalogs, and adds a much-needed post-save verification story. However, the current draft still has unresolved blockers around identity, internal Unity editor ownership, subgraph coverage, rollback semantics, and release/test criteria.

## Blocking Findings

### B1. The "production ready" claim is not supported by the declared scope or release gate

The spec aims for a complete production-ready redesign, but its release bar is materially narrower:

- The test plan explicitly excludes the historically broken gap groups from end-to-end coverage and only greenlights the "Works well" allowlist from `docs/vfx_graph_mcp_tool_gaps.md`.
- `vfx_recipe` is intentionally shipped as a placeholder.
- There is no subgraph scope at all, even though subgraphs are a core production VFX Graph primitive.
- Generator sync is not enforced in CI; the spec explicitly declines a dedicated generator check.

That makes this a credible spec for a **production-capable core** on one locked stack, but not yet a spec for a fully production-ready replacement.

**Recommendation:** either narrow the claim to something like "production-ready core for URP + VFX Graph 17.4 without subgraphs" or expand scope and release gates until the production-ready statement is actually true.

### B2. The identity model is still not implementable as written, and the obvious fallback options are user-hostile

The spec's identity section assumes a durable writable label-like field on every `VFXModel` subtype. That is not true in the embedded VFX Graph source:

- `VFXContext` has a serialized `label`.
- `VFXModel` only exposes `virtual string name => string.Empty`.
- The broader model tree does not expose a universal writable label field.

The existing review in `docs/spec-review-vfx-graph-mcp-redesign.md` correctly calls this out. More importantly, the most likely fallback, writing tokens into visible names, is also a poor production answer because it pollutes artist-facing graph data and YAML diffs.

Stable identity is the center of the redesign. If it is not solved cleanly, the rest of the tool surface inherits instability.

**Recommendation:** replace the identity section with one of these explicit models:

- A real hidden metadata patch in the embedded VFX package for all relevant model types.
- A sidecar plus deterministic structural fingerprint strategy.
- A hybrid model with persisted sidecar identity and session-local aliases, but no artist-visible label/name pollution.

### B3. The redesign is effectively a maintained fork of Unity's editor internals, but the spec treats it like a normal addon

The spec's best ideas depend on editor-internal VFX APIs and generated wrappers over internal types. In practice, that means owning a fork boundary, not just an addon boundary:

- `VFXLibrary` is editor-internal and should be the authoritative catalog source.
- `VFXViewController` owns model creation paths such as `AddVFXModel`.
- Undo/restore behavior lives in editor internals such as `VFXGraphUndoStack`.
- The spec assumes `InternalsVisibleTo` changes in the embedded VFX Graph package.
- The current addon asmdef still references only runtime VFX assemblies, which shows how large this architectural change actually is.

This is not a criticism of the direction. It is a criticism of how the maintenance cost is framed. Once the tool depends on internal editor APIs plus package patching, version upgrades and upstream drift become first-class product risks and need to be treated that way in the spec.

**Recommendation:** add an explicit "embedded VFX package ownership" section covering:

- Required patches to the embedded VFX package.
- Expected merge burden on Unity upgrades.
- Compatibility assertions the generator/runtime must pass.
- Failure policy when the generated catalog and package version drift apart.

### B4. Subgraph support is missing from the spec even though it is part of real VFX Graph production usage

The spec never defines subgraph support. That omission is substantial because the embedded VFX Graph package clearly contains subgraph-specific models and dependency logic, including:

- `VFXSubgraphBlock`
- `VFXSubgraphContext`
- `VFXSubgraphOperator`
- compiled-data dependency handling for subgraph relationships

For small isolated graphs, this is survivable. For real production authoring, it is a major gap. Reusable subgraphs are a normal authoring pattern for shared logic, output shaping, and effect reuse.

**Recommendation:** decide before implementation planning whether subgraphs are:

- in MVP,
- explicitly unsupported for `0.3.0`, or
- required for the production-ready claim.

Right now the spec leaves that answer implicit, which is too risky.

## High-Risk Findings

### H1. The YAML verifier is necessary, but not sufficient as the primary correctness proof

The verifier is one of the strongest ideas in the spec. It directly addresses the current package's worst failure mode: reported success that diverges from saved graph state.

But YAML verification alone cannot prove the graph is actually healthy:

- some issues emerge during invalidation/sanitization rather than simple serialization
- some issues emerge at compile time
- some issues emerge during shader/output generation
- some issues are only visible through console/error-manager signals

The spec partly acknowledges this, but still leans too hard on YAML readback as the decisive consistency signal.

**Recommendation:** make graph health a three-part release gate for mutating flows:

1. save and structural diff
2. compile/error-manager status
3. console correlation for warnings that never become hard compile errors

### H2. Observability and performance are under-specified for large graphs

The response-shaping approach is sensible, but the current observability contract is too coarse for real graphs:

- one `verbose` boolean is the only visibility control
- there is no scoped graph query model beyond list pagination
- the design re-resolves live graph objects on every call
- the design performs YAML verification at transaction boundaries

That may be fine for small graphs, but the spec does not define acceptable latency, payload size, or graph-size expectations. A production tool needs to describe how it behaves on larger assets, not just on tiny test graphs.

**Recommendation:** add explicit budgets and scoped query mechanics:

- target latency ranges for single-op and batch flows
- paging/filtering for graph topology queries
- a policy for when verification is required vs optional
- a diagnostic mode for deep inspection without forcing all reads through one `verbose` switch

### H3. Rollback and `discard_changes` are still underspecified

The spec allows abort-on-first-failure batches to leave the in-memory graph dirty, then asks the agent to choose between retrying, saving anyway, or calling `vfx_graph.discard_changes`.

That is not specific enough for a production contract:

- the actual VFX editor undo story is nontrivial
- global Undo is risky
- the spec does not define whether discard means undo, asset reload, or controller rebuild
- tests explicitly do not cover `discard_changes`

**Recommendation:** define one rollback mechanism and commit to it. The safest default is usually reload-from-disk plus controller/state refresh, not implicit global Undo. Then add dedicated tests for it.

### H4. The background section understates the current addon surface

The spec describes the current addon as a `manage_vfx` mega-tool, which is directionally true but incomplete. In the current code and live Unity tool registration there is also a separate `manage_vfx_graph` tool and an `inspect_vfx_asset` tool.

That matters because migration and cleanup plans should start from the actual current surface area, even if that surface is stale or partly superseded.

**Recommendation:** update the background and migration sections so parity, cleanup, and deletion planning use the real current tool surface, not an older mental model.

### H5. Busy-editor and asset-pipeline coordination need a stronger runtime contract

The spec defines errors such as `asset_pipeline_busy`, but it does not fully describe how mutating flows coordinate with:

- asset import/refresh
- compilation
- domain reload
- deferred VFX graph invalidation

In a Unity editor tool, these are not edge cases. They are part of the normal operating model.

**Recommendation:** add preconditions and retry semantics for every mutating tool and batch commit, including when the tool should fail fast versus wait-and-retry.

## Medium Findings

### M1. The phase plan is optimistic about when the quirks layer becomes necessary

The spec lands all eight tools before the override layer is meaningfully populated. In practice, the override layer is where the VFX-specific pain will show up: collisions, hidden settings, non-obvious coercions, and Unity naming quirks.

**Recommendation:** move a minimal but real `Quirks.yaml` pass earlier so the tool surface is validated on the same assumptions it will actually ship with.

### M2. `vfx_node` still mixes several different failure domains

`vfx_node` owns add/remove/move/duplicate/connect/disconnect/get/set_setting/get/set_property. That is workable, but it combines lifecycle, topology, and configuration into one large surface with different error semantics.

**Recommendation:** either justify that grouping more explicitly or split topology concerns into a dedicated connection-focused tool or sub-surface.

### M3. Skipping generator sync enforcement in CI weakens reproducibility

The spec explicitly avoids a separate generator-in-CI check because committed generated files are treated as the runtime source of truth. That is convenient, but it makes it easier for committed artifacts to drift from the embedded package.

**Recommendation:** even for a personal-use tool, add one automated check that proves generated output still matches the embedded package and sentinel.

### M4. The token-savings policy may hide useful debugging context

The compact response design is one of the better parts of the spec, but the current version leans hard toward terseness. In tricky graph failures, agents may need more than `verbose: true|false`.

**Recommendation:** keep terse-by-default responses, but reserve room for targeted diagnostics such as scoped details, batch per-op traces, or correlation IDs.

## What The Spec Gets Right

- Replacing ad-hoc runtime behavior with generated catalogs is the right direction.
- Removing stale ParticleSystem, LineRenderer, and TrailRenderer code from this package is the right cleanup.
- Committing generated artifacts rather than depending on runtime generation is a good reproducibility choice.
- A single response-shaping chokepoint is a better long-term contract than today’s scattered result shaping.
- Post-save verification is a meaningful answer to the current package's silent-divergence failures.
- Separating quirks from hint templates is a maintainable way to encode Unity-specific behavior without burying it in handlers.

## Recommended Spec Changes Before Implementation Planning

1. Reframe the goal statement so it matches the actual release scope, or expand the release scope until the production-ready claim is defensible.
2. Replace the identity section with a concrete, implementable metadata strategy that does not rely on artist-visible labels or names.
3. Add an explicit ownership model for the embedded VFX Graph fork and its required patches.
4. Decide MVP status for subgraphs now, not during implementation.
5. Strengthen verification from "YAML diff" to "YAML diff + compile/error status + console correlation."
6. Define rollback behavior and test it directly.
7. Add performance budgets, scoped read APIs, and busy-editor coordination rules.
8. Update the background/migration sections to reflect the actual current addon surface, not just the oldest `manage_vfx` framing.

## Bottom Line

If the question is "is this a good basis for a serious rebuild?", the answer is **yes**.

If the question is "is this already a complete spec for a 100% production-ready VFX Graph MCP replacement?", the answer is **no**.

The current draft is best understood as a strong architecture-and-rollout proposal that still needs a tighter product definition, a real identity solution, an explicit internal-API ownership model, and a broader definition of release readiness.
