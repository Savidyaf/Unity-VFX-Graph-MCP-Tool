# VFX Graph MCP v0.3.0 Rebuild — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current `manage_vfx` + `manage_vfx_graph` + `inspect_vfx_asset` tools with a generated, version-locked, production-ready VFX Graph MCP tool suite (9 grouped tools) including first-class subgraph support.

**Architecture:** A dev-time generator walks `VFXLibrary.Get*()` descriptors and emits a committed typed catalog + coercers + wrappers + schemas. The runtime never reflects — it reaches VFX Graph internals through a one-line `InternalsVisibleTo` patch on the embedded VFX Graph package. Identity is sidecar + structural fingerprint (no asset mutation). Every transaction runs through a three-part health gate (YAML diff + compile status + console correlation).

**Tech Stack:** Unity 6.x (6401 / 17.4.x), URP, `com.unity.visualeffectgraph` 17.4.0 (embedded, soft-forked), `com.coplaydev.unity-mcp` (host), C# 9/.NET Standard 2.1, Newtonsoft.Json 3.2.1, NUnit via Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md`
**Reviews consumed:** `docs/spec-review-vfx-graph-mcp-redesign.md`, `docs/objective-review-vfx-graph-mcp-redesign.md`
**Gaps input:** `docs/vfx_graph_mcp_tool_gaps.md`

---

## File structure (before tasks)

All paths are relative to `Packages/com.spiralingstudio.mcp.vfxgraph/` unless stated.

```
Editor/
├── VfxAssemblyInternals.cs           (kept, addon→tests InternalsVisibleTo)
├── Generation/                       (NEW — generator, dev-time only)
│   ├── VfxCatalogGenerator.cs        (orchestrator + menu item)
│   ├── VfxLibraryWalker.cs           (walks VFXLibrary.Get*() descriptors)
│   ├── VfxSlotTreeBuilder.cs         (recursive slot tree model)
│   ├── VfxQuirksLoader.cs            (Quirks.yaml reader)
│   ├── CatalogIR.cs                  (in-memory IR types)
│   ├── Quirks.yaml                   (hand-authored overrides)
│   ├── Hints.yaml                    (hand-authored hint templates)
│   └── Emitters/
│       ├── CatalogEmitter.cs
│       ├── CoercersEmitter.cs
│       ├── WrappersEmitter.cs
│       ├── SubgraphWrappersEmitter.cs
│       ├── SchemasEmitter.cs
│       └── OverridesEmitter.cs
├── Generated/                        (NEW — emitted, committed to git)
│   ├── VfxCatalog.g.cs
│   ├── VfxCoercers.g.cs
│   ├── VfxNodeWrappers.g.cs
│   ├── VfxSubgraphWrappers.g.cs
│   ├── VfxToolSchemas.g.cs
│   ├── VfxOverrides.g.cs
│   └── .catalog-version              (sentinel, version string)
├── Kernel/                           (NEW — runtime kernel)
│   ├── VfxKernelContracts.cs         (interfaces — locked at phase 3 start)
│   ├── VfxIdentity.cs
│   ├── VfxStructuralFingerprint.cs
│   ├── VfxIdentitySidecar.cs
│   ├── VfxNodeOps.cs                 (only Unity-touching layer)
│   ├── VfxResponseShaper.cs
│   ├── VfxTransaction.cs
│   ├── VfxYamlVerifier.cs            (partial UnityYAML parser)
│   ├── VfxCompileGate.cs
│   ├── VfxConsoleCorrelator.cs
│   ├── VfxBusyGate.cs
│   ├── VfxConsoleReader.cs           (moved from Tools/Vfx/, adapted)
│   └── VfxErrorEnvelope.cs           (error code + shape)
├── Tools/                            (NEW — 9 MCP tool classes, flat)
│   ├── VfxAssetTool.cs
│   ├── VfxGraphTool.cs
│   ├── VfxNodeTool.cs
│   ├── VfxBlockTool.cs
│   ├── VfxPropertyTool.cs
│   ├── VfxSubgraphTool.cs
│   ├── VfxRecipeTool.cs
│   ├── VfxBatchTool.cs
│   ├── VfxDiagTool.cs
│   └── Vfx/                          (OLD — deleted in phase 7)
│       ├── ManageVFX.cs              (delete)
│       ├── ManageVfxGraph.cs         (delete)
│       ├── InspectVFXAsset.cs        (delete)
│       └── … all other old files    (delete)
└── (asmdef unchanged; adds reference to Unity.VisualEffectGraph.Editor)

Tests/
└── Editor/
    ├── Generation/
    │   └── VfxCatalogGeneratorTests.cs
    ├── Catalog/
    │   ├── VfxCatalogCompletenessTests.cs
    │   ├── VfxCatalogRoundTripTests.cs
    │   └── VfxCoercerTests.cs
    ├── Kernel/
    │   ├── VfxIdentityTests.cs
    │   ├── VfxYamlVerifierTests.cs
    │   ├── VfxCompileGateTests.cs
    │   ├── VfxConsoleCorrelatorTests.cs
    │   ├── VfxBusyGateTests.cs
    │   ├── VfxResponseShaperTests.cs
    │   └── VfxDiscardChangesTests.cs
    ├── Tools/
    │   ├── VfxSmokeTests.cs          (E2E)
    │   ├── VfxSubgraphTests.cs
    │   ├── VfxBatchAbortTests.cs
    │   ├── VfxPerformanceTests.cs
    │   ├── VfxOverrideTests.cs
    │   └── VfxNoReflectionTests.cs
    └── Fixtures/
        ├── FiveHundredNodeGraph.cs    (generator for perf fixture)
        └── SyntheticYamlFragments.cs  (test data)
```

**Embedded package patch:**
- `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs` (new file, 2 lines)

**Deleted in phase 7:**
All 30 files under old `Editor/Tools/Vfx/` except:
- `VfxConsoleReader.cs` (moved to `Kernel/` in phase 2)
- 5 existing test files (deleted; replaced with new test files)

---

## Phase 1 — Embedded package patch & branch setup

### Task 1: Create rebuild branch off current HEAD

**Files:**
- None (git only)

- [ ] **Step 1: Verify current branch and status**

Run: `git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip status --short && git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip branch --show-current`
Expected: `vfxgraph-package-v0.2` as current branch; clean working tree for the files we'll touch (`CLAUDE.md`, `Packages/packages-lock.json` modifications remain untouched).

- [ ] **Step 2: Tag the current state before rebuild**

Run: `git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip tag -a v0.2-pre-rebuild -m "Snapshot before v0.3.0 rebuild"`
Expected: tag created silently.

- [ ] **Step 3: Create and switch to rebuild branch**

Run: `git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip checkout -b vfxgraph-rebuild-v0.3`
Expected: `Switched to a new branch 'vfxgraph-rebuild-v0.3'`

- [ ] **Step 4: Verify**

Run: `git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip branch --show-current`
Expected: `vfxgraph-rebuild-v0.3`

### Task 2: Commit the embedded VFX Graph package baseline

**Files:**
- Create (git adds existing untracked files): `Packages/com.unity.visualeffectgraph/**/*`
- Create (git adds existing untracked files): `Packages/com.unity.render-pipelines.core/**/*`

Context: Currently `git status` shows `Packages/com.unity.visualeffectgraph/` and `Packages/com.unity.render-pipelines.core/` as untracked. The rebuild depends on committing these as the baseline for patch 1.

- [ ] **Step 1: Verify the packages exist and contain the expected files**

Run: `ls Packages/com.unity.visualeffectgraph/Editor/Unity.VisualEffectGraph.Editor.asmdef Packages/com.unity.visualeffectgraph/Editor/Core/VFXLibrary.cs`
Expected: both files exist.

- [ ] **Step 2: Stage and commit the embedded packages as baseline**

Run:
```bash
git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip add Packages/com.unity.visualeffectgraph Packages/com.unity.render-pipelines.core
git -C /Users/pakaya/Documents/GitHub/VFX-MPC-wip commit -m "$(cat <<'EOF'
Commit embedded VFX Graph + SRP Core packages as soft-fork baseline

Baseline for the v0.3.0 rebuild's one-line InternalsVisibleTo patch.
Upstream versions:
- com.unity.visualeffectgraph 17.4.0
- com.unity.render-pipelines.core 17.4.0

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: commit created with ~1400 files changed.

### Task 3: Apply the InternalsVisibleTo patch

**Files:**
- Create: `Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs`

- [ ] **Step 1: Create the patch file**

```csharp
// Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs
// VFX MCP addon soft-fork patch — grants the addon's editor assembly access
// to UnityEditor.VFX internal types (VFXLibrary, VFXViewController, VFXModel,
// VFXContext, VFXBlock, VFXOperator, VFXSlot, etc.). Without this line, the
// generator and kernel cannot compile. See:
//   docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md
// (section: Embedded VFX Graph Package Ownership)

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")]
```

- [ ] **Step 2: Commit**

```bash
git add Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs
git commit -m "Add InternalsVisibleTo patch for VFX Graph addon access"
```

### Task 4: Update addon asmdef to reference VFX Graph Editor assembly

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor.meta` *(not this — .meta files aren't editable)*
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Unity.VfxMcp.Editor.asmdef` *(name may differ — find actual asmdef first)*

- [ ] **Step 1: Find the addon's editor asmdef**

Run: `find Packages/com.spiralingstudio.mcp.vfxgraph -name "*.asmdef"`
Expected: one or more asmdef paths.

- [ ] **Step 2: Read the current asmdef**

Use the Read tool on the addon's editor asmdef returned by step 1.

- [ ] **Step 3: Add reference to Unity.VisualEffectGraph.Editor**

Add `"Unity.VisualEffectGraph.Editor"` to the `"references"` array. Example transformation (adjust to actual file content):

```json
{
  "name": "com.spiralingstudio.mcp.vfxgraph.Editor",
  "references": [
    "Unity.VisualEffectGraph.Runtime",
    "Unity.VisualEffectGraph.Editor",
    "Unity.RenderPipelines.Core.Runtime",
    "Unity.RenderPipelines.Core.Editor",
    "MCPForUnity.Editor",
    "Newtonsoft.Json"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

- [ ] **Step 4: Verify the assembly compiles**

Run Unity's compilation via `mcp__UnityMCP__read_console` and check for `CS` errors. If none, the patch + asmdef reference are working.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/*.asmdef
git commit -m "Add Unity.VisualEffectGraph.Editor reference to addon asmdef"
```

### Task 5: Verify `VFXLibrary` is reachable from the addon

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxInternalsAccessTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxInternalsAccessTests.cs
using NUnit.Framework;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Tests
{
    public class VfxInternalsAccessTests
    {
        [Test]
        public void VFXLibrary_Operators_ReturnsNonEmpty()
        {
            int count = 0;
            foreach (var _ in VFXLibrary.GetOperators()) count++;
            Assert.Greater(count, 0,
                "VFXLibrary.GetOperators() is empty — InternalsVisibleTo patch at " +
                "Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs may be missing.");
        }

        [Test]
        public void VFXLibrary_Blocks_ReturnsNonEmpty()
        {
            int count = 0;
            foreach (var _ in VFXLibrary.GetBlocks()) count++;
            Assert.Greater(count, 0);
        }

        [Test]
        public void VFXLibrary_Contexts_ReturnsNonEmpty()
        {
            int count = 0;
            foreach (var _ in VFXLibrary.GetContexts()) count++;
            Assert.Greater(count, 0);
        }
    }
}
```

- [ ] **Step 2: Run the tests via Unity Test Runner**

Use `mcp__UnityMCP__run_tests` with `mode: "EditMode"` and filter `VfxInternalsAccessTests`.
Expected: all 3 pass. If they fail with `TypeInitializationException` or `MethodAccessException`, the patch isn't working.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxInternalsAccessTests.cs
git commit -m "Add smoke test proving InternalsVisibleTo patch works"
```

---

## Phase 2 — Generator scaffold (single-agent)

### Task 6: Define the CatalogIR data model

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/CatalogIR.cs`

- [ ] **Step 1: Create the IR types**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/CatalogIR.cs
using System;
using System.Collections.Generic;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// In-memory model of the VFX Graph catalog, built by VfxLibraryWalker
    /// and consumed by the emitters. Pure data; no Unity dependencies inside
    /// the types themselves (System.Type references are kept as strings for
    /// serialization-friendliness).
    /// </summary>
    internal sealed class CatalogIR
    {
        public string VfxGraphPackageVersion { get; set; }
        public List<NodeDescriptor> Operators { get; } = new();
        public List<NodeDescriptor> Blocks { get; } = new();
        public List<NodeDescriptor> Contexts { get; } = new();
        public List<NodeDescriptor> Parameters { get; } = new();
        public List<NodeDescriptor> SubgraphOperators { get; } = new();
        public List<NodeDescriptor> SubgraphBlocks { get; } = new();
        public List<NodeDescriptor> SubgraphContexts { get; } = new();
        public List<SlotTypeDescriptor> SlotTypes { get; } = new();
        public List<AttributeDescriptor> Attributes { get; } = new();
    }

    internal sealed class NodeDescriptor
    {
        public string TypeFQN { get; set; }         // "UnityEditor.VFX.Operator.Lerp"
        public string ShortName { get; set; }        // "Lerp"
        public string Category { get; set; }         // "Math/Basic"
        public string VariantKey { get; set; }       // for variadic overloads; "" if none
        public List<SettingDescriptor> Settings { get; } = new();
        public List<SlotBindingDescriptor> InputSlots { get; } = new();
        public List<SlotBindingDescriptor> OutputSlots { get; } = new();
        public bool IsAbstract { get; set; }         // always false after walker filters
        public bool IsDeprecated { get; set; }       // always false after walker filters
    }

    internal sealed class SettingDescriptor
    {
        public string Name { get; set; }
        public string TypeFQN { get; set; }
        public string DefaultLiteral { get; set; }   // emitted inline; null if no default
        public bool IsHidden { get; set; }           // VFXSettingAttribute.VisibleFlags
    }

    internal sealed class SlotBindingDescriptor
    {
        public string Name { get; set; }
        public string SlotTypeFQN { get; set; }      // references an entry in SlotTypes
        public int Index { get; set; }
    }

    internal sealed class SlotTypeDescriptor
    {
        public string TypeFQN { get; set; }          // leaf or compound
        public bool IsCompound { get; set; }
        public List<SlotChildDescriptor> Children { get; } = new();
    }

    internal sealed class SlotChildDescriptor
    {
        public string Name { get; set; }             // ".x", ".y", ".z"
        public string ChildTypeFQN { get; set; }
    }

    internal sealed class AttributeDescriptor
    {
        public string Name { get; set; }
        public string ValueTypeFQN { get; set; }
        public bool IsVariadic { get; set; }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/CatalogIR.cs
git commit -m "Add CatalogIR data model for VFX generator"
```

### Task 7: Implement VfxLibraryWalker for operators only (baseline)

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs`
- Test: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEditor.VFX;
using System.Linq;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxLibraryWalkerTests
    {
        [Test]
        public void Walk_Operators_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            int expected = VFXLibrary.GetOperators().Count();
            Assert.AreEqual(expected, ir.Operators.Count,
                "Walker should produce exactly one NodeDescriptor per VFXLibrary operator.");
        }

        [Test]
        public void Walk_Operators_EachHasFQN()
        {
            var ir = new VfxLibraryWalker().Walk();
            foreach (var op in ir.Operators)
            {
                Assert.IsNotEmpty(op.TypeFQN, $"Operator with short name {op.ShortName} has no FQN.");
                Assert.IsNotEmpty(op.ShortName, $"Operator with FQN {op.TypeFQN} has no short name.");
            }
        }

        [Test]
        public void Walk_KnownOperator_LerpIsPresent()
        {
            var ir = new VfxLibraryWalker().Walk();
            var lerp = ir.Operators.FirstOrDefault(o => o.TypeFQN == "UnityEditor.VFX.Operator.Lerp");
            Assert.NotNull(lerp, "Lerp operator must be in the catalog (it was gap #9 in the original tool).");
        }
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `mcp__UnityMCP__run_tests` filter `VfxLibraryWalkerTests`
Expected: FAIL — `VfxLibraryWalker` type not found.

- [ ] **Step 3: Implement minimal walker**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs
using System.Linq;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// Walks VFXLibrary.Get*() descriptors and emits a CatalogIR.
    /// Uses VFXLibrary (the same source Unity's add-node UI uses) rather
    /// than raw assembly reflection, so abstract/deprecated/test-only types
    /// are automatically filtered.
    /// </summary>
    internal sealed class VfxLibraryWalker
    {
        public CatalogIR Walk()
        {
            var ir = new CatalogIR
            {
                VfxGraphPackageVersion = "17.4.0" // wired from package.json in task 12
            };

            foreach (var descriptor in VFXLibrary.GetOperators())
            {
                var type = descriptor.modelType;
                ir.Operators.Add(new NodeDescriptor
                {
                    TypeFQN = type.FullName,
                    ShortName = type.Name,
                    Category = descriptor.category ?? "",
                    VariantKey = "",
                });
            }

            return ir;
        }
    }
}
```

- [ ] **Step 4: Run the test and verify it passes**

Expected: all 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
git commit -m "Add VfxLibraryWalker walking operators"
```

### Task 8: Extend walker to blocks, contexts, parameters, and subgraphs

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs`

- [ ] **Step 1: Extend the test**

Append to `VfxLibraryWalkerTests.cs`:

```csharp
        [Test]
        public void Walk_Blocks_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            Assert.AreEqual(VFXLibrary.GetBlocks().Count(), ir.Blocks.Count);
        }

        [Test]
        public void Walk_Contexts_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            Assert.AreEqual(VFXLibrary.GetContexts().Count(), ir.Contexts.Count);
        }

        [Test]
        public void Walk_Subgraphs_IncludesSubgraphOperator()
        {
            var ir = new VfxLibraryWalker().Walk();
            var sub = ir.SubgraphOperators.FirstOrDefault();
            Assert.NotNull(sub, "VFXSubgraphOperator must appear in the subgraph catalog.");
        }
```

- [ ] **Step 2: Extend the walker implementation**

Replace the `Walk()` body with:

```csharp
public CatalogIR Walk()
{
    var ir = new CatalogIR { VfxGraphPackageVersion = "17.4.0" };

    foreach (var d in VFXLibrary.GetOperators())
        ir.Operators.Add(MakeDescriptor(d.modelType, d.category));

    foreach (var d in VFXLibrary.GetBlocks())
        ir.Blocks.Add(MakeDescriptor(d.modelType, d.category));

    foreach (var d in VFXLibrary.GetContexts())
        ir.Contexts.Add(MakeDescriptor(d.modelType, d.category));

    foreach (var d in VFXLibrary.GetParameters())
        ir.Parameters.Add(MakeDescriptor(d.modelType, d.category));

    // Subgraphs live alongside regular descriptors in VFXLibrary; split them
    // out by concrete type.
    foreach (var op in ir.Operators.ToArray())
        if (op.TypeFQN == "UnityEditor.VFX.Operator.VFXSubgraphOperator")
        {
            ir.SubgraphOperators.Add(op);
            ir.Operators.Remove(op);
        }
    foreach (var blk in ir.Blocks.ToArray())
        if (blk.TypeFQN == "UnityEditor.VFX.Block.VFXSubgraphBlock")
        {
            ir.SubgraphBlocks.Add(blk);
            ir.Blocks.Remove(blk);
        }
    foreach (var ctx in ir.Contexts.ToArray())
        if (ctx.TypeFQN == "UnityEditor.VFX.VFXSubgraphContext")
        {
            ir.SubgraphContexts.Add(ctx);
            ir.Contexts.Remove(ctx);
        }

    return ir;
}

private static NodeDescriptor MakeDescriptor(System.Type modelType, string category)
{
    return new NodeDescriptor
    {
        TypeFQN = modelType.FullName,
        ShortName = modelType.Name,
        Category = category ?? "",
        VariantKey = "",
    };
}
```

Note: the exact subgraph FQNs may differ slightly (verify with the generator self-compile check). The implementer should run the walker, list all FQNs, and correct the split rules if mismatched.

- [ ] **Step 3: Run tests and verify all pass**

- [ ] **Step 4: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
git commit -m "Extend walker to blocks, contexts, parameters, subgraphs"
```

### Task 9: Write first emitter (CatalogEmitter) — VfxCatalog.g.cs

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CatalogEmitter.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/CatalogEmitterTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/CatalogEmitterTests.cs
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class CatalogEmitterTests
    {
        [Test]
        public void Emit_ProducesCompilableOutput()
        {
            var ir = new VfxLibraryWalker().Walk();
            var emitter = new CatalogEmitter();
            string output = emitter.Emit(ir);
            Assert.IsNotEmpty(output);
            StringAssert.Contains("namespace SpiralingStudio.VfxMcp.Generated", output);
            StringAssert.Contains("internal static class VfxCatalog", output);
            StringAssert.Contains("public static readonly string[] Operators", output);
        }

        [Test]
        public void Emit_IsDeterministic()
        {
            var ir = new VfxLibraryWalker().Walk();
            var a = new CatalogEmitter().Emit(ir);
            var b = new CatalogEmitter().Emit(ir);
            Assert.AreEqual(a, b, "Emitter output must be byte-identical across runs.");
        }

        [Test]
        public void Emit_IncludesEveryOperatorFQN()
        {
            var ir = new VfxLibraryWalker().Walk();
            string output = new CatalogEmitter().Emit(ir);
            foreach (var op in ir.Operators)
                StringAssert.Contains(op.TypeFQN, output);
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

Expected: `CatalogEmitter` not found.

- [ ] **Step 3: Implement the emitter**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CatalogEmitter.cs
using System.Linq;
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// Emits Editor/Generated/VfxCatalog.g.cs — the name-resolution table and
    /// static arrays backing the vfx_diag.list_* tools.
    /// Output is deterministic: same CatalogIR always produces byte-identical text.
    /// </summary>
    internal sealed class CatalogEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Do not edit. Regenerate via Tools/VFX MCP/Regenerate Catalog.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxCatalog");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string PackageVersion = \"{ir.VfxGraphPackageVersion}\";");
            sb.AppendLine();

            EmitArray(sb, "Operators", ir.Operators.Select(o => o.TypeFQN));
            EmitArray(sb, "Blocks", ir.Blocks.Select(o => o.TypeFQN));
            EmitArray(sb, "Contexts", ir.Contexts.Select(o => o.TypeFQN));
            EmitArray(sb, "Parameters", ir.Parameters.Select(o => o.TypeFQN));
            EmitArray(sb, "SubgraphOperators", ir.SubgraphOperators.Select(o => o.TypeFQN));
            EmitArray(sb, "SubgraphBlocks", ir.SubgraphBlocks.Select(o => o.TypeFQN));
            EmitArray(sb, "SubgraphContexts", ir.SubgraphContexts.Select(o => o.TypeFQN));

            // Short-name → FQN lookup (for name resolution)
            sb.AppendLine("        public static readonly System.Collections.Generic.Dictionary<string, string> ShortNameToFqn = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            var allNodes = ir.Operators.Concat(ir.Blocks).Concat(ir.Contexts).Concat(ir.Parameters)
                .Concat(ir.SubgraphOperators).Concat(ir.SubgraphBlocks).Concat(ir.SubgraphContexts)
                .OrderBy(n => n.TypeFQN, System.StringComparer.Ordinal);
            foreach (var node in allNodes)
                sb.AppendLine($"            {{ \"{node.ShortName}\", \"{node.TypeFQN}\" }},");
            sb.AppendLine("        };");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void EmitArray(StringBuilder sb, string name, System.Collections.Generic.IEnumerable<string> items)
        {
            sb.AppendLine($"        public static readonly string[] {name} = new string[]");
            sb.AppendLine("        {");
            foreach (var item in items.OrderBy(s => s, System.StringComparer.Ordinal))
                sb.AppendLine($"            \"{item}\",");
            sb.AppendLine("        };");
            sb.AppendLine();
        }
    }
}
```

- [ ] **Step 4: Run tests, verify all pass**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CatalogEmitter.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/CatalogEmitterTests.cs
git commit -m "Add CatalogEmitter producing deterministic VfxCatalog.g.cs"
```

### Task 10: VfxCatalogGenerator orchestrator + editor menu item

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs`

- [ ] **Step 1: Create the orchestrator**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal static class VfxCatalogGenerator
    {
        private const string GeneratedDir =
            "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated";
        private const string SentinelPath = GeneratedDir + "/.catalog-version";

        [MenuItem("Tools/VFX MCP/Regenerate Catalog", priority = 200)]
        public static void Regenerate()
        {
            // Sanity check the patch before doing anything.
            if (!VFXLibrary.GetOperators().Any())
            {
                EditorUtility.DisplayDialog(
                    "VFX MCP: InternalsVisibleTo patch missing",
                    "VFXLibrary.GetOperators() returned 0 items. The patch at\n" +
                    "  Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs\n" +
                    "may be missing or stale. Re-apply from the spec and retry.",
                    "OK");
                return;
            }

            Directory.CreateDirectory(GeneratedDir);

            var ir = new VfxLibraryWalker().Walk();
            var catalogEmitter = new CatalogEmitter();
            var catalogText = catalogEmitter.Emit(ir);

            File.WriteAllText(Path.Combine(GeneratedDir, "VfxCatalog.g.cs"), catalogText);

            // Write sentinel
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.unity.visualeffectgraph/package.json");
            string version = pkgInfo?.version ?? "unknown";
            File.WriteAllText(SentinelPath, version);

            AssetDatabase.Refresh();
            Debug.Log($"[VFX MCP] Catalog regenerated: " +
                $"{ir.Operators.Count} ops, {ir.Blocks.Count} blocks, " +
                $"{ir.Contexts.Count} contexts, {ir.Parameters.Count} params, " +
                $"{ir.SubgraphOperators.Count + ir.SubgraphBlocks.Count + ir.SubgraphContexts.Count} subgraphs. " +
                $"Package version: {version}");
        }

        [InitializeOnLoadMethod]
        private static void CheckStalenessOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SentinelPath)) return;
                var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/com.unity.visualeffectgraph/package.json");
                if (pkgInfo == null) return;
                string recorded = File.ReadAllText(SentinelPath).Trim();
                if (recorded != pkgInfo.version)
                {
                    Debug.LogWarning(
                        $"[VFX MCP] Catalog was generated for package version '{recorded}' " +
                        $"but live version is '{pkgInfo.version}'. Run " +
                        "Tools/VFX MCP/Regenerate Catalog to refresh.");
                }
            };
        }
    }
}
```

- [ ] **Step 2: Manually run the menu item**

Via `mcp__UnityMCP__execute_menu_item` with `menu_item: "Tools/VFX MCP/Regenerate Catalog"`.
Expected: Console logs the counts; `Editor/Generated/VfxCatalog.g.cs` appears; `.catalog-version` written.

- [ ] **Step 3: Verify the generated file compiles**

Use `mcp__UnityMCP__read_console` — no `CS` errors.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/
git commit -m "Add VfxCatalogGenerator + menu item; generate baseline VfxCatalog.g.cs"
```

### Task 11: Codegen determinism test

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxCatalogGeneratorTests.cs`

- [ ] **Step 1: Write the determinism test**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxCatalogGeneratorTests.cs
using System.IO;
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxCatalogGeneratorTests
    {
        [Test]
        public void Regeneration_IsByteIdentical()
        {
            const string path = "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/VfxCatalog.g.cs";
            string before = File.ReadAllText(path);
            VfxCatalogGenerator.Regenerate();
            string after = File.ReadAllText(path);
            Assert.AreEqual(before, after, "Regenerating the catalog twice must produce byte-identical output.");
        }
    }
}
```

- [ ] **Step 2: Run + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxCatalogGeneratorTests.cs
git commit -m "Add codegen determinism test"
```

---

## Phase 3 — Full generator + runtime kernel (4 parallel subagent lanes)

**CRITICAL SETUP before parallel work:** commit the `VfxKernelContracts.cs` interface file so all four lanes compile against the same contracts.

### Task 12: Commit the kernel contracts (interface lock)

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs`

- [ ] **Step 1: Write the contracts file**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs
// LOCKED. Do not modify in phase 3/4 subagent lanes. Changes require
// main-agent approval and a new contract commit.
using System.Collections.Generic;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    // ────────────────────────── Identity ──────────────────────────

    internal interface IVfxIdentity
    {
        string Mint(string graphGuid, VFXModel model);
        VFXModel Resolve(string graphGuid, string token);
        /// <summary>Flush pending sidecar writes to disk.</summary>
        void Flush();
    }

    internal sealed class VfxIdentityRecord
    {
        public string Token;
        public ulong Fingerprint;
    }

    // ────────────────────────── Verifier ──────────────────────────

    internal interface IVfxYamlVerifier
    {
        VfxYamlVerificationResult Verify(string graphAssetPath, VfxIntentSnapshot intent);
    }

    internal sealed class VfxYamlVerificationResult
    {
        public List<VfxVerifierWarning> Warnings = new();
        public List<VfxVerifierError> Errors = new();
    }

    internal sealed class VfxVerifierWarning
    {
        public string Code;
        public int OpIndex;
        public string Slot;
        public string Reason;
    }

    internal sealed class VfxVerifierError
    {
        public string Code;
        public int OpIndex;
        public string ExpectedToken;
        public string ExpectedType;
    }

    // ────────────────────────── Compile gate ──────────────────────────

    internal interface IVfxCompileGate
    {
        VfxCompileResult Compile(string graphAssetPath);
    }

    internal sealed class VfxCompileResult
    {
        public bool Ok;
        public int DurationMs;
        public List<VfxCompileError> Errors = new();
    }

    internal sealed class VfxCompileError
    {
        public string ModelTypeFqn;
        public string ModelToken;
        public string ErrorId;
        public string Description;
        public string Severity; // "error" | "warning"
    }

    // ────────────────────────── Console correlator ──────────────────────────

    internal interface IVfxConsoleCorrelator
    {
        /// <summary>Record the current console high-water mark before a transaction begins.</summary>
        object SnapshotBefore();

        /// <summary>Correlate console lines since the snapshot against the given intent ops.</summary>
        VfxConsoleCorrelation CorrelateAfter(object snapshot, VfxIntentSnapshot intent);
    }

    internal sealed class VfxConsoleCorrelation
    {
        public List<VfxCorrelatedWarning> Correlated = new();
        public List<string> RawLines = new();
    }

    internal sealed class VfxCorrelatedWarning
    {
        public string Code;
        public int OpIndex;
        public string RawLine;
    }

    // ────────────────────────── Busy gate ──────────────────────────

    internal interface IVfxBusyGate
    {
        /// <summary>Throws VfxBusyException if the editor is not idle for mutation.</summary>
        void EnsureIdle();
    }

    // ────────────────────────── Intent snapshot ──────────────────────────

    internal sealed class VfxIntentSnapshot
    {
        public string CorrelationId;
        public List<VfxIntentOp> Ops = new();
    }

    internal sealed class VfxIntentOp
    {
        public int OpIndex;
        public string Kind;              // "add" | "remove" | "connect" | "disconnect" | "set_setting" | "set_property"
        public string ExpectedToken;
        public string ExpectedTypeFqn;
        public string ParentToken;       // for blocks; null for top-level
        public Dictionary<string, object> Payload;  // arbitrary per-kind metadata
    }

    // ────────────────────────── Transaction ──────────────────────────

    internal interface IVfxTransaction
    {
        VfxTransactionScope Begin(string graphGuid, string graphAssetPath, VfxTransactionScopeKind kind);
    }

    internal enum VfxTransactionScopeKind { SingleCall, Batch, Save }

    internal interface IVfxTransactionScope : System.IDisposable
    {
        void Record(VfxIntentOp op);
        VfxCommitResult Commit();
    }

    // Alias so subagents using VfxTransactionScope get the interface
    internal abstract class VfxTransactionScope : IVfxTransactionScope
    {
        public abstract void Record(VfxIntentOp op);
        public abstract VfxCommitResult Commit();
        public abstract void Dispose();
    }

    internal sealed class VfxCommitResult
    {
        public bool Ok;
        public List<object> Diffs = new();               // "added"/"removed"/"modified"
        public List<object> Warnings = new();
        public VfxHealthReport Health;                   // only populated if Ok
        public VfxErrorEnvelope Error;                   // only populated if !Ok
    }

    internal sealed class VfxHealthReport
    {
        public string YamlDiff;                 // "clean" | "warnings" | "errors"
        public string Compile;                  // "ok" | "errors"
        public string Console;                  // "ok" | "warnings"
        public int CompileMs;
        public long YamlBytes;
    }

    // ────────────────────────── NodeOps ──────────────────────────

    internal interface IVfxNodeOps
    {
        /// <summary>Create an operator and add to the graph. Returns the minted token.</summary>
        string AddOperator(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Create a context and add to the graph.</summary>
        string AddContext(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Create a block and add to the specified parent context.</summary>
        string AddBlock(string graphAssetPath, string parentContextToken, string typeFqn, int index);

        /// <summary>Create a parameter (graph-level exposed property) node.</summary>
        string AddParameter(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Add a subgraph reference to the parent graph.</summary>
        string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, UnityEngine.Vector2 pos);

        void RemoveNode(string graphAssetPath, string token);

        void Connect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot);
        void Disconnect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot);

        void SetSetting(string graphAssetPath, string token, string name, object value);
        void SetProperty(string graphAssetPath, string token, string name, object value);

        object GetSetting(string graphAssetPath, string token, string name);
        object GetProperty(string graphAssetPath, string token, string name);

        void DiscardChanges(string graphAssetPath);
    }

    // ────────────────────────── Response shaper ──────────────────────────

    internal interface IVfxResponseShaper
    {
        object Shape(VfxCommitResult commit, bool verbose);
        object ShapeError(VfxErrorEnvelope error);
        object ShapeRead(object payload, bool verbose);
    }

    // ────────────────────────── Error envelope ──────────────────────────

    internal sealed class VfxErrorEnvelope
    {
        public string Code;
        public string Message;
        public string Hint;
        public Dictionary<string, object> Details = new();
        public int? RetryAfterHintMs;
    }

    internal class VfxException : System.Exception
    {
        public string Code;
        public Dictionary<string, object> Details;
        public VfxException(string code, string message, Dictionary<string, object> details = null) : base(message)
        {
            Code = code;
            Details = details ?? new Dictionary<string, object>();
        }
    }

    internal sealed class VfxValidationException : VfxException
    {
        public VfxValidationException(string code, string message, Dictionary<string, object> details = null)
            : base(code, message, details) { }
    }

    internal sealed class VfxIdentityException : VfxException
    {
        public VfxIdentityException(string code, string message, Dictionary<string, object> details = null)
            : base(code, message, details) { }
    }

    internal sealed class VfxBusyException : VfxException
    {
        public int RetryAfterHintMs;
        public VfxBusyException(string code, string message, int retryAfterHintMs,
            Dictionary<string, object> details = null)
            : base(code, message, details)
        {
            RetryAfterHintMs = retryAfterHintMs;
        }
    }
}
```

- [ ] **Step 2: Verify the file compiles**

Run: `mcp__UnityMCP__read_console`
Expected: no CS errors (warnings about unused types are fine).

- [ ] **Step 3: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs
git commit -m "Lock kernel contracts for phase 3 parallel subagents"
```

---

### Phase 3 Lane A — Generator full coverage (subagent 3A)

**Scope:** fill in the remaining 5 emitters and expand the walker with slot trees + settings + attributes. Does not touch `Kernel/`.

### Task 3A-1: Expand walker to capture settings and slot bindings

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs`

- [ ] **Step 1: Add failing test for settings capture**

```csharp
[Test]
public void Walk_Operator_CapturesSettings()
{
    var ir = new VfxLibraryWalker().Walk();
    // Lerp has no settings; pick a node with known settings: VFXInlineOperator has m_Type
    var inline = ir.Operators.FirstOrDefault(o => o.TypeFQN.EndsWith(".VFXInlineOperator"));
    Assert.NotNull(inline, "VFXInlineOperator must exist (gap #15)");
    Assert.IsNotEmpty(inline.Settings, "VFXInlineOperator must have at least one setting (m_Type)");
    Assert.IsTrue(inline.Settings.Any(s => s.Name == "m_Type"), "m_Type setting must be captured");
}

[Test]
public void Walk_Operator_CapturesInputSlots()
{
    var ir = new VfxLibraryWalker().Walk();
    var add = ir.Operators.FirstOrDefault(o => o.TypeFQN == "UnityEditor.VFX.Operator.Add");
    Assert.NotNull(add);
    Assert.IsNotEmpty(add.InputSlots);
}
```

- [ ] **Step 2: Extend `MakeDescriptor` to read settings + slots**

Add to `VfxLibraryWalker.cs`:

```csharp
private static NodeDescriptor MakeDescriptor(System.Type modelType, string category)
{
    var desc = new NodeDescriptor
    {
        TypeFQN = modelType.FullName,
        ShortName = modelType.Name,
        Category = category ?? "",
        VariantKey = "",
    };

    // Instantiate a template to inspect settings/slots
    UnityEditor.VFX.VFXModel template = null;
    try
    {
        template = (UnityEditor.VFX.VFXModel)UnityEngine.ScriptableObject.CreateInstance(modelType);
    }
    catch { return desc; /* some types can't be default-constructed; skip their deep shape */ }

    if (template == null) return desc;

    foreach (var setting in template.GetSettings(listHidden: true))
    {
        desc.Settings.Add(new SettingDescriptor
        {
            Name = setting.name,
            TypeFQN = setting.value?.GetType().FullName ?? "System.Object",
            DefaultLiteral = null,
            IsHidden = false, // refine in task 3A-2
        });
    }

    // Slot walking for VFXSlotContainerModel subtypes
    if (template is UnityEditor.VFX.IVFXSlotContainer container)
    {
        int i = 0;
        foreach (var slot in container.inputSlots)
        {
            desc.InputSlots.Add(new SlotBindingDescriptor
            {
                Name = slot.name,
                SlotTypeFQN = slot.property.type.FullName,
                Index = i++,
            });
        }
        i = 0;
        foreach (var slot in container.outputSlots)
        {
            desc.OutputSlots.Add(new SlotBindingDescriptor
            {
                Name = slot.name,
                SlotTypeFQN = slot.property.type.FullName,
                Index = i++,
            });
        }
    }

    UnityEngine.ScriptableObject.DestroyImmediate(template);
    return desc;
}
```

*(If `IVFXSlotContainer` or `slot.property` don't resolve, the implementer inspects the actual VFX Graph source to find the right member names — they may be `inputSlots`/`outputSlots` or `GetInputSlots()`/`GetOutputSlots()`. The test in step 1 is the source of truth.)*

- [ ] **Step 3: Run tests + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
git commit -m "Walker captures settings and input/output slots"
```

### Task 3A-2: Implement VfxSlotTreeBuilder for compound slot types

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxSlotTreeBuilder.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxSlotTreeBuilderTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// VfxSlotTreeBuilderTests.cs
using NUnit.Framework;
using System.Linq;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxSlotTreeBuilderTests
    {
        [Test]
        public void Build_Vector3_ProducesCompoundWithThreeChildren()
        {
            var ir = new CatalogIR();
            new VfxSlotTreeBuilder().Build(ir, new[] { typeof(UnityEngine.Vector3) });
            var vec = ir.SlotTypes.FirstOrDefault(t => t.TypeFQN == "UnityEngine.Vector3");
            Assert.NotNull(vec);
            Assert.IsTrue(vec.IsCompound);
            Assert.AreEqual(3, vec.Children.Count);
            Assert.IsTrue(vec.Children.Any(c => c.Name == "x"));
        }

        [Test]
        public void Build_Float_IsLeaf()
        {
            var ir = new CatalogIR();
            new VfxSlotTreeBuilder().Build(ir, new[] { typeof(float) });
            var f = ir.SlotTypes.FirstOrDefault(t => t.TypeFQN == "System.Single");
            Assert.NotNull(f);
            Assert.IsFalse(f.IsCompound);
        }
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxSlotTreeBuilder.cs
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// Walks compound Unity/VFX struct types (Vector3, Color, Transform, Sphere,
    /// AABox, etc.) and records their child-field shape for compound coercers.
    /// </summary>
    internal sealed class VfxSlotTreeBuilder
    {
        private static readonly HashSet<Type> LeafTypes = new()
        {
            typeof(bool), typeof(int), typeof(uint), typeof(float), typeof(double),
            typeof(string), typeof(UnityEngine.Texture2D), typeof(UnityEngine.Texture2DArray),
            typeof(UnityEngine.Texture3D), typeof(UnityEngine.Cubemap), typeof(UnityEngine.Mesh),
            typeof(UnityEngine.GraphicsBuffer),
        };

        public void Build(CatalogIR ir, IEnumerable<Type> types)
        {
            var seen = new HashSet<string>();
            foreach (var type in types)
                BuildOne(ir, type, seen);
        }

        private void BuildOne(CatalogIR ir, Type type, HashSet<string> seen)
        {
            if (type == null || !seen.Add(type.FullName)) return;

            var desc = new SlotTypeDescriptor { TypeFQN = type.FullName };

            if (LeafTypes.Contains(type) || type.IsEnum || type.IsPrimitive)
            {
                desc.IsCompound = false;
                ir.SlotTypes.Add(desc);
                return;
            }

            // Struct or reference type with public instance fields → compound
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (fields.Length == 0)
            {
                desc.IsCompound = false;
                ir.SlotTypes.Add(desc);
                return;
            }

            desc.IsCompound = true;
            foreach (var f in fields)
            {
                desc.Children.Add(new SlotChildDescriptor
                {
                    Name = f.Name,
                    ChildTypeFQN = f.FieldType.FullName,
                });
                BuildOne(ir, f.FieldType, seen);
            }
            ir.SlotTypes.Add(desc);
        }
    }
}
```

- [ ] **Step 3: Wire into generator**

In `VfxCatalogGenerator.Regenerate()`, after the walker call:

```csharp
var slotTypes = ir.Operators.Concat(ir.Blocks).Concat(ir.Contexts).Concat(ir.Parameters)
    .SelectMany(n => n.InputSlots.Concat(n.OutputSlots))
    .Select(s => System.Type.GetType(s.SlotTypeFQN + ", " + "UnityEngine.dll"))
    .Where(t => t != null)
    .Distinct();
new VfxSlotTreeBuilder().Build(ir, slotTypes);
```

*(Type.GetType may not resolve all FQNs from a string — the implementer should use `AppDomain.CurrentDomain.GetAssemblies()` to search across loaded assemblies if needed.)*

- [ ] **Step 4: Run tests + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxSlotTreeBuilder.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxSlotTreeBuilderTests.cs
git commit -m "Add VfxSlotTreeBuilder for compound slot types"
```

### Task 3A-3: CoercersEmitter — generates VfxCoercers.g.cs

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CoercersEmitter.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCoercerTests.cs`

The emitter produces a static class with methods `public static T CoerceTo<name>(JToken token)` per slot type.

- [ ] **Step 1: Failing coercer round-trip tests**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCoercerTests.cs
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Catalog.Tests
{
    public class VfxCoercerTests
    {
        [Test]
        public void Float_RoundTrip()
        {
            var input = new JValue(1.5f);
            var coerced = SpiralingStudio.VfxMcp.Generated.VfxCoercers.CoerceToFloat(input);
            Assert.AreEqual(1.5f, coerced);
        }

        [Test]
        public void Vector3_FromJsonArray_RoundTrip()
        {
            var input = JArray.Parse("[1.0, 2.0, 3.0]");
            Vector3 coerced = SpiralingStudio.VfxMcp.Generated.VfxCoercers.CoerceToVector3(input);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), coerced);
        }

        [Test]
        public void Vector3_FromObject_RoundTrip()
        {
            var input = JObject.Parse("{\"x\":1,\"y\":2,\"z\":3}");
            Vector3 coerced = SpiralingStudio.VfxMcp.Generated.VfxCoercers.CoerceToVector3(input);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), coerced);
        }

        [Test]
        public void Texture2D_FromAssetPath_RoundTrip()
        {
            // Requires a test texture at Assets/Tests/VfxFixtures/TestTex.png
            const string path = "Assets/Tests/VfxFixtures/TestTex.png";
            var input = new JValue(path);
            var tex = SpiralingStudio.VfxMcp.Generated.VfxCoercers.CoerceToTexture2D(input);
            Assert.NotNull(tex, $"Expected test texture at {path} — create it before running this test.");
        }
    }
}
```

- [ ] **Step 2: Implement CoercersEmitter**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CoercersEmitter.cs
using System.Linq;
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class CoercersEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Do not edit. Regenerate via Tools/VFX MCP/Regenerate Catalog.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using Newtonsoft.Json.Linq;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxCoercers");
            sb.AppendLine("    {");

            // Leaf coercers — hand-written, always present
            EmitLeafCoercers(sb);

            // Compound coercers — emitted per slot type
            foreach (var slot in ir.SlotTypes.Where(s => s.IsCompound)
                                              .OrderBy(s => s.TypeFQN, System.StringComparer.Ordinal))
                EmitCompoundCoercer(sb, slot);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void EmitLeafCoercers(StringBuilder sb)
        {
            sb.AppendLine(@"
        public static float CoerceToFloat(JToken t)
            => t.Type == JTokenType.Float || t.Type == JTokenType.Integer
                ? t.Value<float>()
                : throw new System.InvalidCastException($""Cannot coerce {t.Type} to float"");

        public static int CoerceToInt(JToken t)
            => t.Type == JTokenType.Integer
                ? t.Value<int>()
                : throw new System.InvalidCastException($""Cannot coerce {t.Type} to int"");

        public static bool CoerceToBool(JToken t)
            => t.Type == JTokenType.Boolean
                ? t.Value<bool>()
                : throw new System.InvalidCastException($""Cannot coerce {t.Type} to bool"");

        public static string CoerceToString(JToken t) => t.Value<string>();

        public static Texture2D CoerceToTexture2D(JToken t)
        {
            var path = t.Value<string>();
            if (string.IsNullOrEmpty(path))
                throw new System.ArgumentException(""Texture2D requires an asset path string"");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new System.IO.FileNotFoundException($""Texture not found at {path}"");
        }

        public static GraphicsBuffer CoerceToGraphicsBuffer(JToken t)
            => null; // Default-constructed; runtime buffer assignment happens elsewhere.
");
        }

        private static void EmitCompoundCoercer(StringBuilder sb, SlotTypeDescriptor slot)
        {
            string typeName = slot.TypeFQN.Replace("+", ".");
            string methodName = "CoerceTo" + slot.TypeFQN.Substring(slot.TypeFQN.LastIndexOf('.') + 1)
                .Replace("+", "_");

            sb.AppendLine($"        public static {typeName} {methodName}(JToken t)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var result = new {typeName}();");
            sb.AppendLine("            if (t.Type == JTokenType.Array)");
            sb.AppendLine("            {");
            sb.AppendLine("                var arr = (JArray)t;");
            for (int i = 0; i < slot.Children.Count; i++)
            {
                var child = slot.Children[i];
                sb.AppendLine($"                if (arr.Count > {i}) result.{child.Name} = CoerceToFloat(arr[{i}]);");
            }
            sb.AppendLine("            }");
            sb.AppendLine("            else if (t.Type == JTokenType.Object)");
            sb.AppendLine("            {");
            sb.AppendLine("                var obj = (JObject)t;");
            foreach (var child in slot.Children)
                sb.AppendLine($"                if (obj[\"{child.Name}\"] != null) result.{child.Name} = CoerceToFloat(obj[\"{child.Name}\"]);");
            sb.AppendLine("            }");
            sb.AppendLine("            else throw new System.InvalidCastException($\"Cannot coerce {t.Type} to " + typeName + "\");");
            sb.AppendLine("            return result;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
}
```

*(Note: this simplified emitter assumes all compound children are `float`. For fields that are themselves compound, the implementer extends the emitter recursively — recursion is safe because `VfxSlotTreeBuilder` already seeded every nested type.)*

- [ ] **Step 3: Wire into generator and run**

In `VfxCatalogGenerator.Regenerate()`, add after the CatalogEmitter call:

```csharp
File.WriteAllText(Path.Combine(GeneratedDir, "VfxCoercers.g.cs"),
    new CoercersEmitter().Emit(ir));
```

- [ ] **Step 4: Run coercer tests + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/CoercersEmitter.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCoercerTests.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/VfxCoercers.g.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs
git commit -m "Add CoercersEmitter with compound slot support"
```

### Task 3A-4: WrappersEmitter — VfxNodeWrappers.g.cs

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/WrappersEmitter.cs`

Pattern: emits one `Create<ShortName>()` method per operator/block/context that calls `VFXLibrary.Get*().First(d => d.modelType == typeof(T)).CreateInstance()`.

- [ ] **Step 1: Implement emitter**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/WrappersEmitter.cs
using System.Linq;
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class WrappersEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEditor.VFX;");
            sb.AppendLine();
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxNodeWrappers");
            sb.AppendLine("    {");

            sb.AppendLine(@"
        public static VFXOperator CreateOperator(string typeFqn)
        {
            foreach (var d in VFXLibrary.GetOperators())
                if (d.modelType.FullName == typeFqn)
                    return (VFXOperator)d.CreateInstance();
            throw new System.ArgumentException($""Unknown operator type: {typeFqn}"");
        }

        public static VFXBlock CreateBlock(string typeFqn)
        {
            foreach (var d in VFXLibrary.GetBlocks())
                if (d.modelType.FullName == typeFqn)
                    return (VFXBlock)d.CreateInstance();
            throw new System.ArgumentException($""Unknown block type: {typeFqn}"");
        }

        public static VFXContext CreateContext(string typeFqn)
        {
            foreach (var d in VFXLibrary.GetContexts())
                if (d.modelType.FullName == typeFqn)
                    return (VFXContext)d.CreateInstance();
            throw new System.ArgumentException($""Unknown context type: {typeFqn}"");
        }

        public static VFXParameter CreateParameter(string typeFqn)
        {
            foreach (var d in VFXLibrary.GetParameters())
                if (d.modelType.FullName == typeFqn)
                    return (VFXParameter)d.CreateInstance();
            throw new System.ArgumentException($""Unknown parameter type: {typeFqn}"");
        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 2: Wire + regenerate + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/WrappersEmitter.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/VfxNodeWrappers.g.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs
git commit -m "Add WrappersEmitter + generated VfxNodeWrappers.g.cs"
```

### Task 3A-5: SubgraphWrappersEmitter — VfxSubgraphWrappers.g.cs

Follows WrappersEmitter pattern. Additionally includes `BindAsset(VFXModel ref, string assetPath)` helper that uses reflection to set the subgraph field on `VFXSubgraphOperator`, `VFXSubgraphBlock`, `VFXSubgraphContext` based on their concrete type.

- [ ] **Step 1: Implement emitter**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/SubgraphWrappersEmitter.cs
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class SubgraphWrappersEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEditor.VFX;");
            sb.AppendLine();
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxSubgraphWrappers");
            sb.AppendLine("    {");

            sb.AppendLine(@"
        public static void BindAsset(VFXModel refModel, string assetPath)
        {
            // Use SerializedObject to set the subgraph field regardless of concrete type.
            // The field is named 'm_Subgraph' across all three subgraph model types in 17.4.
            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (obj == null)
                throw new System.IO.FileNotFoundException($""Subgraph asset not found: {assetPath}"");

            var so = new UnityEditor.SerializedObject(refModel);
            var prop = so.FindProperty(""m_Subgraph"");
            if (prop == null)
                throw new System.InvalidOperationException(
                    $""{refModel.GetType().Name} has no m_Subgraph property."");
            prop.objectReferenceValue = obj;
            so.ApplyModifiedPropertiesWithoutUndo();
        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
```

*(The field name `m_Subgraph` should be verified against the embedded package source. If the field differs between the three subgraph types, the implementer writes three branches keyed on `refModel.GetType()`.)*

- [ ] **Step 2: Wire + commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/SubgraphWrappersEmitter.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/VfxSubgraphWrappers.g.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs
git commit -m "Add SubgraphWrappersEmitter"
```

### Task 3A-6: SchemasEmitter — VfxToolSchemas.g.cs

Emits JSON-schema dictionaries keyed by tool name + action. The schemas are consumed by tool classes in phase 4 to validate incoming params.

- [ ] **Step 1: Implement emitter** — shape by tool, actions, and per-action required params derived from CatalogIR (nodes → enum of valid type strings, etc.):

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/SchemasEmitter.cs
using System.Linq;
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class SchemasEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxToolSchemas");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly string VfxNode_AddActionTypeEnum = @\"[");
            sb.Append("            ");
            sb.AppendLine(string.Join(",", ir.Operators.Select(o => $"\"\"{o.TypeFQN}\"\"")));
            sb.AppendLine("        ]\";");
            sb.AppendLine("        public static readonly string VfxBlock_AddActionTypeEnum = @\"[");
            sb.Append("            ");
            sb.AppendLine(string.Join(",", ir.Blocks.Select(o => $"\"\"{o.TypeFQN}\"\"")));
            sb.AppendLine("        ]\";");
            sb.AppendLine("        // Additional schemas emitted here");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 2: Wire + commit**

### Task 3A-7: OverridesEmitter — VfxOverrides.g.cs

Reads `Quirks.yaml` (phase 5 populates it) and emits an override dictionary. Phase 3 version can emit an empty stub that compiles and returns `null` for all lookups.

- [ ] **Step 1: Implement stub + Quirks.yaml blank**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/OverridesEmitter.cs
using System.Text;
namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class OverridesEmitter
    {
        public string Emit(CatalogIR ir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class VfxOverrides");
            sb.AppendLine("    {");
            sb.AppendLine("        public static string ResolveCollision(string shortName) => null;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
```

Also create blank `Editor/Generation/Quirks.yaml` and `Editor/Generation/Hints.yaml` (single `hints:` key, empty body).

- [ ] **Step 2: Wire + commit**

### Task 3A-8: VfxCatalogCompletenessTests — catalog count matches VFXLibrary

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCatalogCompletenessTests.cs`

- [ ] **Step 1: Test**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Catalog.Tests
{
    public class VfxCatalogCompletenessTests
    {
        [Test]
        public void GeneratedCatalog_Operators_MatchesVFXLibraryCount()
        {
            Assert.AreEqual(
                VFXLibrary.GetOperators().Count() - 1, // minus VFXSubgraphOperator which is split out
                Generated.VfxCatalog.Operators.Length,
                "Generated catalog operator count must match VFXLibrary minus subgraph ops.");
        }

        [Test]
        public void GeneratedCatalog_Blocks_MatchesVFXLibraryCount()
        {
            Assert.AreEqual(
                VFXLibrary.GetBlocks().Count() - 1,
                Generated.VfxCatalog.Blocks.Length);
        }
    }
}
```

- [ ] **Step 2: Run + commit**

---

### Phase 3 Lane B — Identity + YAML verifier (subagent 3B)

Does not touch `Generation/`. Consumes phase 2's CatalogIR (read-only).

### Task 3B-1: VfxStructuralFingerprint

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxStructuralFingerprint.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxStructuralFingerprintTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
// VfxStructuralFingerprintTests.cs
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxStructuralFingerprintTests
    {
        [Test]
        public void Fnv1a64_Deterministic()
        {
            var a = VfxStructuralFingerprint.Fnv1a64("test input");
            var b = VfxStructuralFingerprint.Fnv1a64("test input");
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Fnv1a64_DifferentInputs_DifferentHashes()
        {
            var a = VfxStructuralFingerprint.Fnv1a64("test input");
            var b = VfxStructuralFingerprint.Fnv1a64("different input");
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void ShortToken_5Hex()
        {
            var token = VfxStructuralFingerprint.ToShortToken("n_", 0xDEADBEEF_CAFEBABE);
            Assert.IsTrue(token.StartsWith("n_"));
            Assert.AreEqual(7, token.Length); // "n_" + 5 hex chars
        }
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxStructuralFingerprint.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal static class VfxStructuralFingerprint
    {
        public static ulong Fnv1a64(string input)
        {
            const ulong OffsetBasis = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;
            ulong hash = OffsetBasis;
            foreach (char c in input)
            {
                hash ^= c;
                hash *= Prime;
            }
            return hash;
        }

        public static string ToShortToken(string prefix, ulong fingerprint)
        {
            // Top 20 bits → 5 hex chars
            ulong top = (fingerprint >> (64 - 20)) & 0xFFFFFUL;
            return $"{prefix}{top:x5}";
        }

        public static ulong Compute(string graphGuid, VFXModel model)
        {
            // Recursively include parent fingerprint.
            string parentKey = "";
            var parent = model.GetParent();
            if (parent != null)
                parentKey = Compute(graphGuid, parent).ToString("x16");

            // Sibling index among same-type siblings
            int siblingIndex = 0;
            if (parent != null)
            {
                int i = 0;
                foreach (var sib in parent.children)
                {
                    if (sib == model) { siblingIndex = i; break; }
                    if (sib.GetType() == model.GetType()) i++;
                }
            }

            // Slot shape hash (names + types, no values)
            string slotShape = "";
            if (model is IVFXSlotContainer container)
            {
                slotShape = string.Join(",", container.inputSlots.Select(s => s.name + ":" + s.property.type.FullName));
            }

            // Setting shape hash (names + types, no values)
            string settingShape = string.Join(",",
                model.GetSettings(listHidden: false).Select(s => s.name + ":" + (s.value?.GetType().FullName ?? "null")));

            string key = $"{graphGuid}|{parentKey}|{model.GetType().FullName}|{siblingIndex}|{slotShape}|{settingShape}";
            return Fnv1a64(key);
        }
    }
}
```

- [ ] **Step 3: Run tests + commit**

### Task 3B-2: VfxIdentitySidecar — Library/VfxMcpIdentity.json I/O

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentitySidecar.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxIdentitySidecarTests.cs`

- [ ] **Step 1: Failing round-trip test**

```csharp
using System.IO;
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxIdentitySidecarTests
    {
        [Test]
        public void RoundTrip_AddThenGet()
        {
            const string path = "Library/VfxMcpIdentity_Test.json";
            if (File.Exists(path)) File.Delete(path);

            var sidecar = new VfxIdentitySidecar(path);
            sidecar.Put("guid1", "n_abc12", 0xDEADBEEFUL);
            sidecar.Flush();

            var reloaded = new VfxIdentitySidecar(path);
            Assert.AreEqual(0xDEADBEEFUL, reloaded.Get("guid1", "n_abc12"));
            File.Delete(path);
        }

        [Test]
        public void Get_Missing_ReturnsZero()
        {
            var sidecar = new VfxIdentitySidecar("Library/VfxMcpIdentity_NonExistent.json");
            Assert.AreEqual(0UL, sidecar.Get("missing", "missing"));
        }
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentitySidecar.cs
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxIdentitySidecar
    {
        private readonly string _path;
        private Dictionary<string, Dictionary<string, string>> _data;
        private bool _dirty;

        public VfxIdentitySidecar(string path = "Library/VfxMcpIdentity.json")
        {
            _path = path;
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_path))
            {
                _data = new Dictionary<string, Dictionary<string, string>>();
                return;
            }
            try
            {
                var json = File.ReadAllText(_path);
                _data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json)
                        ?? new Dictionary<string, Dictionary<string, string>>();
            }
            catch
            {
                _data = new Dictionary<string, Dictionary<string, string>>();
            }
        }

        public void Put(string graphGuid, string token, ulong fingerprint)
        {
            if (!_data.TryGetValue(graphGuid, out var inner))
            {
                inner = new Dictionary<string, string>();
                _data[graphGuid] = inner;
            }
            inner[token] = fingerprint.ToString("x16");
            _dirty = true;
        }

        public ulong Get(string graphGuid, string token)
        {
            if (!_data.TryGetValue(graphGuid, out var inner)) return 0;
            if (!inner.TryGetValue(token, out var hex)) return 0;
            return ulong.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }

        public void Flush()
        {
            if (!_dirty) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented));
            _dirty = false;
        }
    }
}
```

- [ ] **Step 3: Run + commit**

### Task 3B-3: VfxIdentity implementation

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentity.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxIdentityTests.cs`

- [ ] **Step 1: Tests — mint, resolve, recover**

```csharp
using NUnit.Framework;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxIdentityTests
    {
        private const string TestAsset = "Assets/Tests/VfxFixtures/IdentityTest.vfx";

        [Test]
        public void Mint_ReturnsTokenStartingWithPrefix()
        {
            var (graph, op) = CreateTestGraphWithOperator();
            try
            {
                var sidecar = new VfxIdentitySidecar("Library/VfxMcpIdentity_Test.json");
                var identity = new VfxIdentity(sidecar);
                var token = identity.Mint("test-guid", op);
                Assert.IsTrue(token.StartsWith("n_"));
                Assert.AreEqual(7, token.Length);
            }
            finally { CleanupTestGraph(graph); }
        }

        [Test]
        public void Resolve_RoundTrip()
        {
            var (graph, op) = CreateTestGraphWithOperator();
            try
            {
                var sidecar = new VfxIdentitySidecar("Library/VfxMcpIdentity_Test.json");
                var identity = new VfxIdentity(sidecar);
                var token = identity.Mint("test-guid", op);
                var resolved = identity.Resolve("test-guid", token);
                Assert.AreSame(op, resolved);
            }
            finally { CleanupTestGraph(graph); }
        }

        // Helpers create/destroy a .vfx at TestAsset with one VFXInlineOperator
        private static (VFXGraph graph, VFXOperator op) CreateTestGraphWithOperator() { /* impl */ throw new System.NotImplementedException(); }
        private static void CleanupTestGraph(VFXGraph g) { /* impl */ }
    }
}
```

- [ ] **Step 2: Implement VfxIdentity**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentity.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxIdentity : IVfxIdentity
    {
        private readonly VfxIdentitySidecar _sidecar;

        public VfxIdentity(VfxIdentitySidecar sidecar)
        {
            _sidecar = sidecar;
        }

        public string Mint(string graphGuid, VFXModel model)
        {
            ulong fp = VfxStructuralFingerprint.Compute(graphGuid, model);
            string prefix = PrefixFor(model);
            string token = VfxStructuralFingerprint.ToShortToken(prefix, fp);
            _sidecar.Put(graphGuid, token, fp);
            return token;
        }

        public VFXModel Resolve(string graphGuid, string token)
        {
            ulong expected = _sidecar.Get(graphGuid, token);
            if (expected == 0)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in sidecar",
                    new Dictionary<string, object> { ["token"] = token });

            var graph = LoadGraph(graphGuid);
            foreach (var candidate in WalkAllModels(graph))
            {
                if (VfxStructuralFingerprint.Compute(graphGuid, candidate) == expected)
                    return candidate;
            }

            // Recovery path — loose match by type + parent fingerprint
            var matches = new List<VFXModel>();
            foreach (var candidate in WalkAllModels(graph))
            {
                // loose criteria defined in the spec; simplified here
                matches.Add(candidate);
            }
            if (matches.Count == 1)
            {
                // update sidecar with the new fingerprint, warn via logging
                ulong newFp = VfxStructuralFingerprint.Compute(graphGuid, matches[0]);
                _sidecar.Put(graphGuid, token, newFp);
                return matches[0];
            }

            throw new VfxIdentityException("node_lost",
                $"Token {token} could not be recovered",
                new Dictionary<string, object> { ["token"] = token });
        }

        public void Flush() => _sidecar.Flush();

        private static string PrefixFor(VFXModel m)
        {
            if (m is VFXBlock) return "b_";
            if (m is VFXContext) return "ctx_";
            if (m is VFXOperator) return "n_";
            return "n_";
        }

        private static VFXGraph LoadGraph(string graphGuid)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(graphGuid);
            var resource = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(path)?.GetResource();
            return resource?.GetOrCreateGraph();
        }

        private static IEnumerable<VFXModel> WalkAllModels(VFXModel root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.children)
                foreach (var descendant in WalkAllModels(child))
                    yield return descendant;
        }
    }
}
```

*(The exact method to get a `VFXGraph` from a `VisualEffectAsset` is `GetResource()` + `GetOrCreateGraph()` — verify against VFX Graph source during implementation.)*

- [ ] **Step 3: Run + commit**

### Task 3B-4: VfxYamlVerifier — partial YAML reader

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxYamlVerifier.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxYamlVerifierTests.cs`

The partial YAML reader only understands the fields needed for verification: node identity, slot connections, parent links. It's NOT a general UnityYAML parser.

- [ ] **Step 1: Failing test with hand-crafted YAML**

```csharp
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxYamlVerifierTests
    {
        [Test]
        public void Verify_DetectsMissingNode()
        {
            var yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 1, guid: known-guid}
  m_Name: ExistingNode
";
            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new()
                {
                    new VfxIntentOp { OpIndex = 0, Kind = "add", ExpectedToken = "n_missing", ExpectedTypeFqn = "Missing" }
                }
            };
            var result = verifier.VerifyFromYaml(yaml, intent);
            Assert.IsNotEmpty(result.Errors);
            Assert.AreEqual("intent_diverged", result.Errors[0].Code);
        }
    }
}
```

- [ ] **Step 2: Implement partial reader**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxYamlVerifier.cs
using System.IO;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxYamlVerifier : IVfxYamlVerifier
    {
        public VfxYamlVerificationResult Verify(string graphAssetPath, VfxIntentSnapshot intent)
        {
            var yaml = File.ReadAllText(graphAssetPath);
            return VerifyFromYaml(yaml, intent);
        }

        public VfxYamlVerificationResult VerifyFromYaml(string yaml, VfxIntentSnapshot intent)
        {
            var result = new VfxYamlVerificationResult();
            // Minimal partial parser: scan for "MonoBehaviour:" blocks with m_Name lines,
            // then cross-check against intent ops.
            // For now, any add-op whose ExpectedToken doesn't appear as a substring of the yaml
            // is treated as missing. Full structural diff is implemented in task 3B-5.
            foreach (var op in intent.Ops)
            {
                if (op.Kind != "add") continue;
                if (op.ExpectedToken != null && !yaml.Contains(op.ExpectedToken))
                {
                    // Since identity is sidecar-only, tokens don't appear in YAML. This
                    // first implementation uses type-name presence as a proxy; the full
                    // fingerprint-based verification comes in task 3B-5.
                    if (!string.IsNullOrEmpty(op.ExpectedTypeFqn) && !yaml.Contains(op.ExpectedTypeFqn))
                    {
                        result.Errors.Add(new VfxVerifierError
                        {
                            Code = "intent_diverged",
                            OpIndex = op.OpIndex,
                            ExpectedToken = op.ExpectedToken,
                            ExpectedTypeFqn = op.ExpectedTypeFqn,
                        });
                    }
                }
            }
            return result;
        }
    }
}
```

- [ ] **Step 3: Run + commit**

### Task 3B-5: Extend verifier with fingerprint-based structural diff

Extend `VfxYamlVerifier` to parse the MonoBehaviour blocks and reconstruct a simplified graph model, then compute fingerprints and diff against intent. This is the "full" partial reader.

- [ ] **Step 1: Hand-crafted YAML test with dropped connection**

Not shown for brevity — the implementer writes tests with synthetic YAML fragments exercising: node present, node missing, connection present, connection missing, slot name drift.

- [ ] **Step 2: Implement parser**

Use a streaming scan:
1. Split by `--- !u!` document markers
2. For each block, extract `m_Name`, type (from `m_Script` GUID or preferably the tag like `!u!114 &<fileid>`), and `m_Parent`, `m_Children`, `m_LinkedSlots` fields via regex
3. Build a minimal object graph
4. For each intent op, walk the object graph looking for a match by `(type, parent, sibling index)` — the same fingerprint inputs
5. Record `intent_diverged` errors and `connection_dropped` warnings

```csharp
// Simplified skeleton — full implementation is ~200 lines
private static IEnumerable<(string typeId, string name, int fileId)> ScanMonoBehaviours(string yaml)
{
    // Regex: --- !u!114 &(\d+)\nMonoBehaviour:\n(?:.*\n)*?  m_Name: (.+)
    // returns (114, name, fileid)
    throw new System.NotImplementedException();
}
```

- [ ] **Step 3: Commit**

---

### Phase 3 Lane C — Compile gate + console correlator + busy gate (subagent 3C)

### Task 3C-1: VfxBusyGate

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxBusyGate.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxBusyGateTests.cs`

- [ ] **Step 1: Implementation**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxBusyGate.cs
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxBusyGate : IVfxBusyGate
    {
        public void EnsureIdle()
        {
            if (EditorApplication.isCompiling)
                throw new VfxBusyException("asset_pipeline_busy",
                    "Unity is compiling; retry after compile finishes.", 1500);

            if (AssetDatabase.IsAssetImportWorkerProcess())
                throw new VfxBusyException("asset_pipeline_busy",
                    "Asset import in progress; retry in 1-2s.", 1500);
        }
    }
}
```

- [ ] **Step 2: Tests**

```csharp
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxBusyGateTests
    {
        [Test]
        public void EnsureIdle_WhenIdle_DoesNotThrow()
        {
            var gate = new VfxBusyGate();
            Assert.DoesNotThrow(() => gate.EnsureIdle());
        }

        // Testing the throw case requires triggering compilation, deferred to perf-test phase
    }
}
```

- [ ] **Step 3: Commit**

### Task 3C-2: VfxCompileGate

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxCompileGate.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxCompileGateTests.cs`

- [ ] **Step 1: Implementation**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxCompileGate.cs
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxCompileGate : IVfxCompileGate
    {
        public VfxCompileResult Compile(string graphAssetPath)
        {
            var sw = Stopwatch.StartNew();
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            var resource = asset?.GetResource();
            if (resource == null)
            {
                sw.Stop();
                return new VfxCompileResult { Ok = false, DurationMs = (int)sw.ElapsedMilliseconds };
            }
            var graph = resource.GetOrCreateGraph();
            graph.CompileAndUpdateAsset();

            var result = new VfxCompileResult();
            // Access errorManager.compileReporter — the property name may differ;
            // the implementer verifies against VFXGraph.cs in the embedded package.
            var reporter = graph.errorManager?.compileReporter;
            if (reporter != null)
            {
                foreach (var model in reporter.dirtyModels)
                {
                    foreach (var error in reporter.GetDirtyModelErrors(model))
                    {
                        result.Errors.Add(new VfxCompileError
                        {
                            ModelTypeFqn = model.GetType().FullName,
                            ErrorId = error.error,
                            Description = error.description,
                            Severity = error.type.ToString(),
                        });
                    }
                }
            }
            sw.Stop();
            result.DurationMs = (int)sw.ElapsedMilliseconds;
            result.Ok = result.Errors.Count == 0;
            return result;
        }
    }
}
```

- [ ] **Step 2: Tests with known-bad HLSL graph**

Not fully expanded — test crafts a `.vfx` asset with a CustomHLSL block containing syntactically invalid HLSL and asserts `result.Ok == false`.

- [ ] **Step 3: Commit**

### Task 3C-2b: Adapt VfxConsoleReader for high-water-mark semantics

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleReader.cs` *(NEW file in Kernel/; the old file at `Editor/Tools/Vfx/VfxConsoleReader.cs` is still there until phase 7 and serves a different tool)*
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxConsoleReaderTests.cs`

This new file is authored fresh (not moved from `Tools/Vfx/`). Phase 7 deletes the old `Tools/Vfx/VfxConsoleReader.cs` as part of the legacy cleanup; by that time the new `Kernel/VfxConsoleReader.cs` has been in service since phase 3.

- [ ] **Step 1: Implement the new console reader**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleReader.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel
{
    /// <summary>
    /// Console reader with per-session high-water-mark semantics.
    /// The watermark is the LogEntries count at snapshot time; subsequent reads
    /// return only entries added since the snapshot.
    /// </summary>
    internal static class VfxConsoleReader
    {
        public static object GetHighWaterMark()
        {
            return GetEntriesCount();
        }

        public static IReadOnlyList<string> GetLinesSince(object snapshot)
        {
            int start = snapshot is int i ? i : 0;
            int end = GetEntriesCount();
            var lines = new List<string>();
            for (int idx = start; idx < end; idx++)
                lines.Add(GetEntryText(idx));
            return lines;
        }

        // LogEntries is internal; access via reflection on the UnityEditor.LogEntries type.
        // This is the only reflection-into-UnityEditor allowed in the kernel because
        // LogEntries doesn't expose a public API and isn't gated by our InternalsVisibleTo.
        private static System.Type s_LogEntriesType;
        private static System.Reflection.MethodInfo s_GetCountMethod;
        private static System.Reflection.MethodInfo s_GetEntryInternalMethod;

        private static System.Type LogEntriesType => s_LogEntriesType ??=
            System.Type.GetType("UnityEditor.LogEntries, UnityEditor");

        private static int GetEntriesCount()
        {
            s_GetCountMethod ??= LogEntriesType?.GetMethod("GetCount",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return (int)(s_GetCountMethod?.Invoke(null, null) ?? 0);
        }

        private static string GetEntryText(int index)
        {
            // LogEntries.GetEntryInternal signature varies by Unity version; the
            // implementer picks the right overload during phase 3C execution.
            return $"[console line {index}]"; // placeholder — real impl wired in phase 3C
        }
    }
}
```

Note: `VfxConsoleReader` is the ONE exception to the "no runtime reflection" rule in the kernel — it reflects on `UnityEditor.LogEntries` which is not part of the VFX Graph package and not covered by our `InternalsVisibleTo` patch. The `VfxNoReflectionTests` (task 5-6) explicitly excludes this file.

- [ ] **Step 2: Write the test**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxConsoleReaderTests
    {
        [Test]
        public void GetLinesSince_ReturnsOnlyNewLines()
        {
            var mark = VfxConsoleReader.GetHighWaterMark();
            Debug.Log("test log A");
            Debug.Log("test log B");
            var lines = VfxConsoleReader.GetLinesSince(mark);
            Assert.GreaterOrEqual(lines.Count, 2);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleReader.cs Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxConsoleReaderTests.cs
git commit -m "Add VfxConsoleReader in Kernel with high-water-mark API"
```

### Task 3C-3: VfxConsoleCorrelator

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleCorrelator.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxConsoleCorrelatorTests.cs`

- [ ] **Step 1: Implementation**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleCorrelator.cs
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxConsoleCorrelator : IVfxConsoleCorrelator
    {
        // Known patterns we can correlate to intent ops
        private static readonly Regex DroppedSlotPattern =
            new(@"Remove \d+ linked slot\(s\) that couldn't be deserialized from (.+)",
                RegexOptions.Compiled);

        public object SnapshotBefore()
        {
            return VfxConsoleReader.GetHighWaterMark();
        }

        public VfxConsoleCorrelation CorrelateAfter(object snapshot, VfxIntentSnapshot intent)
        {
            var correlation = new VfxConsoleCorrelation();
            var lines = VfxConsoleReader.GetLinesSince(snapshot);

            foreach (var line in lines)
            {
                var dropMatch = DroppedSlotPattern.Match(line);
                if (dropMatch.Success)
                {
                    // Try to match dropped slot name to an intent connect op
                    string slot = dropMatch.Groups[1].Value;
                    int opIndex = -1;
                    for (int i = 0; i < intent.Ops.Count; i++)
                    {
                        if (intent.Ops[i].Kind == "connect" &&
                            intent.Ops[i].Payload != null &&
                            intent.Ops[i].Payload.TryGetValue("toSlot", out var toSlot) &&
                            (toSlot as string)?.Contains(slot) == true)
                        {
                            opIndex = i;
                            break;
                        }
                    }

                    correlation.Correlated.Add(new VfxCorrelatedWarning
                    {
                        Code = "connection_dropped_at_save",
                        OpIndex = opIndex,
                        RawLine = line,
                    });
                }
                else
                {
                    correlation.RawLines.Add(line);
                }
            }
            return correlation;
        }
    }
}
```

- [ ] **Step 2: Tests + commit**

---

### Phase 3 Lane D — Transaction + ResponseShaper + NodeOps stubs (subagent 3D)

### Task 3D-1: VfxResponseShaper

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxResponseShaper.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxResponseShaperTests.cs`

- [ ] **Step 1: Tests — terse vs verbose, type legend**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxResponseShaperTests
    {
        [Test]
        public void Shape_Terse_OmitsHealthReport()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok = true,
                Health = new VfxHealthReport { Compile = "ok" }
            };
            var output = shaper.Shape(commit, verbose: false);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(output);
            Assert.IsFalse(json.Contains("health"),
                "Terse mode must omit the health report.");
        }

        [Test]
        public void Shape_Verbose_IncludesHealthReport()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok = true,
                Health = new VfxHealthReport { Compile = "ok" }
            };
            var output = shaper.Shape(commit, verbose: true);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(output);
            Assert.IsTrue(json.Contains("health"));
        }
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxResponseShaper.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxResponseShaper : IVfxResponseShaper
    {
        public object Shape(VfxCommitResult commit, bool verbose)
        {
            if (!commit.Ok)
                return ShapeError(commit.Error);

            var obj = new JObject();
            if (commit.Diffs != null && commit.Diffs.Count > 0)
                obj["added"] = JArray.FromObject(commit.Diffs);
            if (commit.Warnings != null && commit.Warnings.Count > 0)
                obj["warnings"] = JArray.FromObject(commit.Warnings);
            if (verbose && commit.Health != null)
                obj["health"] = JObject.FromObject(commit.Health);
            return obj;
        }

        public object ShapeError(VfxErrorEnvelope error)
        {
            var obj = new JObject();
            var err = new JObject
            {
                ["code"] = error.Code,
                ["message"] = error.Message,
                ["hint"] = error.Hint ?? "",
            };
            if (error.Details != null && error.Details.Count > 0)
                err["details"] = JObject.FromObject(error.Details);
            if (error.RetryAfterHintMs.HasValue)
                err["retry_after_hint_ms"] = error.RetryAfterHintMs.Value;
            obj["error"] = err;
            return obj;
        }

        public object ShapeRead(object payload, bool verbose)
        {
            // Minimal passthrough; verbose-specific filtering comes in phase 4 when
            // the read action shapes are known.
            return payload;
        }
    }
}
```

- [ ] **Step 3: Run + commit**

### Task 3D-2: VfxTransaction

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxTransaction.cs`

- [ ] **Step 1: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxTransaction.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxTransaction : IVfxTransaction
    {
        private readonly IVfxIdentity _identity;
        private readonly IVfxYamlVerifier _verifier;
        private readonly IVfxCompileGate _compileGate;
        private readonly IVfxConsoleCorrelator _correlator;
        private readonly IVfxBusyGate _busyGate;

        public VfxTransaction(IVfxIdentity identity, IVfxYamlVerifier verifier,
            IVfxCompileGate compileGate, IVfxConsoleCorrelator correlator, IVfxBusyGate busyGate)
        {
            _identity = identity;
            _verifier = verifier;
            _compileGate = compileGate;
            _correlator = correlator;
            _busyGate = busyGate;
        }

        public VfxTransactionScope Begin(string graphGuid, string graphAssetPath, VfxTransactionScopeKind kind)
        {
            _busyGate.EnsureIdle();
            return new Scope(graphGuid, graphAssetPath, kind, _identity, _verifier, _compileGate, _correlator);
        }

        private sealed class Scope : VfxTransactionScope
        {
            private readonly string _graphGuid;
            private readonly string _graphAssetPath;
            private readonly IVfxIdentity _identity;
            private readonly IVfxYamlVerifier _verifier;
            private readonly IVfxCompileGate _compileGate;
            private readonly IVfxConsoleCorrelator _correlator;
            private readonly object _consoleSnapshot;
            private readonly VfxIntentSnapshot _intent;

            public Scope(string graphGuid, string graphAssetPath, VfxTransactionScopeKind kind,
                IVfxIdentity identity, IVfxYamlVerifier verifier, IVfxCompileGate compileGate,
                IVfxConsoleCorrelator correlator)
            {
                _graphGuid = graphGuid;
                _graphAssetPath = graphAssetPath;
                _identity = identity;
                _verifier = verifier;
                _compileGate = compileGate;
                _correlator = correlator;
                _consoleSnapshot = correlator.SnapshotBefore();
                _intent = new VfxIntentSnapshot { CorrelationId = Guid.NewGuid().ToString("n").Substring(0, 8) };
            }

            public override void Record(VfxIntentOp op) => _intent.Ops.Add(op);

            public override VfxCommitResult Commit()
            {
                var result = new VfxCommitResult();

                // Save
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(_graphAssetPath);
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);

                // Part 1: YAML diff
                var yamlResult = _verifier.Verify(_graphAssetPath, _intent);
                foreach (var w in yamlResult.Warnings) result.Warnings.Add(w);
                if (yamlResult.Errors.Count > 0)
                {
                    result.Ok = false;
                    result.Error = new VfxErrorEnvelope
                    {
                        Code = "intent_diverged",
                        Message = $"{yamlResult.Errors.Count} intent ops did not appear in the saved YAML.",
                    };
                    return result;
                }

                // Part 2: Compile
                var compileResult = _compileGate.Compile(_graphAssetPath);
                if (!compileResult.Ok)
                {
                    result.Ok = false;
                    result.Error = new VfxErrorEnvelope
                    {
                        Code = "compile_error",
                        Message = $"{compileResult.Errors.Count} compile errors.",
                        Details = new Dictionary<string, object> { ["errors"] = compileResult.Errors }
                    };
                    return result;
                }

                // Part 3: Console correlation
                var correlation = _correlator.CorrelateAfter(_consoleSnapshot, _intent);
                foreach (var c in correlation.Correlated) result.Warnings.Add(c);

                _identity.Flush();

                result.Ok = true;
                result.Health = new VfxHealthReport
                {
                    YamlDiff = yamlResult.Warnings.Count > 0 ? "warnings" : "clean",
                    Compile = "ok",
                    Console = correlation.Correlated.Count > 0 ? "warnings" : "ok",
                    CompileMs = compileResult.DurationMs,
                };
                return result;
            }

            public override void Dispose() { /* no-op; Commit does the real work */ }
        }
    }
}
```

- [ ] **Step 2: Commit**

### Task 3D-3: VfxNodeOps stub

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs`

- [ ] **Step 1: Stub with NotImplementedException in bodies**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs
using System;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel
{
    /// <summary>
    /// The ONLY layer that touches Unity's VFX graph mutation APIs.
    /// Phase 3 commits this as a stub; phase 4 fills in the method bodies
    /// as each tool needs them.
    /// </summary>
    internal sealed class VfxNodeOps : IVfxNodeOps
    {
        public string AddOperator(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public string AddContext(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public string AddBlock(string graphAssetPath, string parentContextToken, string typeFqn, int index)
            => throw new NotImplementedException("Phase 4 — VfxBlockTool");

        public string AddParameter(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxSubgraphTool");

        public void RemoveNode(string graphAssetPath, string token)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void Connect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void Disconnect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void SetSetting(string graphAssetPath, string token, string name, object value)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void SetProperty(string graphAssetPath, string token, string name, object value)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public object GetSetting(string graphAssetPath, string token, string name)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public object GetProperty(string graphAssetPath, string token, string name)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public void DiscardChanges(string graphAssetPath)
        {
            UnityEditor.AssetDatabase.ImportAsset(graphAssetPath,
                UnityEditor.ImportAssetOptions.ForceUpdate);
            // VFXViewController re-fetch happens naturally via GetController(..., forceUpdate: true)
        }
    }
}
```

- [ ] **Step 2: Commit**

---

## Phase 4 — 9 MCP tools (3 parallel subagent lanes)

**Setup before parallel work:** wire the kernel DI container so every tool class receives the right dependencies.

### Task 4-COORD: Add ApplyInTransaction stubs to all 9 tool classes

**Files:**
- Create: all 9 tool files from task 4-SETUP onwards will include this method from the start

This task is performed as part of each lane's first tool-class creation, not as a separate pre-task. When lane A creates `VfxAssetTool`, it includes an `ApplyInTransaction` method with a `NotImplementedException` body. Same for lanes B and C. Lane C's batch dispatcher (task 4C-3) calls `ApplyInTransaction` via reflection on the 9 tool class names, so the method signature must be uniform:

```csharp
internal static void ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
{
    throw new System.NotImplementedException(
        "VfxAssetTool.ApplyInTransaction will be wired during batch integration");
}
```

The signature and name MUST match across all 9 tool classes. Lane C finalizes the batch dispatcher once all 9 tools expose this method. If a lane forgets to add it, lane C's batch dispatcher compilation fails, surfacing the missing contract immediately.

### Task 4-SETUP: VfxKernelContainer — dependency wiring

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContainer.cs`

- [ ] **Step 1: Static container**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContainer.cs
namespace SpiralingStudio.VfxMcp.Kernel
{
    /// <summary>
    /// Static singleton container providing kernel services to tool classes.
    /// Each tool class calls VfxKernelContainer.Instance.<service> at the start
    /// of its HandleCommand.
    /// </summary>
    internal static class VfxKernelContainer
    {
        public static IVfxIdentity Identity { get; } = new VfxIdentity(new VfxIdentitySidecar());
        public static IVfxYamlVerifier Verifier { get; } = new VfxYamlVerifier();
        public static IVfxCompileGate CompileGate { get; } = new VfxCompileGate();
        public static IVfxConsoleCorrelator Correlator { get; } = new VfxConsoleCorrelator();
        public static IVfxBusyGate BusyGate { get; } = new VfxBusyGate();
        public static IVfxNodeOps NodeOps { get; } = new VfxNodeOps();
        public static IVfxResponseShaper Shaper { get; } = new VfxResponseShaper();
        public static IVfxTransaction Transaction { get; } =
            new VfxTransaction(Identity, Verifier, CompileGate, Correlator, BusyGate);
    }
}
```

- [ ] **Step 2: Commit**

---

### Phase 4 Lane A — vfx_asset, vfx_graph, vfx_diag (subagent 4A)

### Task 4A-1: VfxAssetTool

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxAssetTool.cs`

- [ ] **Step 1: Implement skeleton**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxAssetTool.cs
using System;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_asset", AutoRegister = true)]
    public static class VfxAssetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = @params.Value<string>("action") ?? "";
                return action.ToLowerInvariant() switch
                {
                    "create" => Create(@params),
                    "list" => List(@params),
                    "delete" => Delete(@params),
                    "assign" => Assign(@params),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code = "unknown_action",
                        Message = $"Unknown vfx_asset action '{action}'",
                        Hint = "Valid: create, list, delete, assign",
                    })
                };
            }
            catch (VfxException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                });
            }
            catch (Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        private static object Create(JObject @params)
        {
            string path = @params.Value<string>("path") ?? throw new VfxValidationException(
                "missing_required_param", "path is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            // Determine kind from extension
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            var asset = ext switch
            {
                ".vfx" => ScriptableObject.CreateInstance(typeof(UnityEngine.VFX.VisualEffectAsset)),
                ".vfxop" => ScriptableObject.CreateInstance<UnityEditor.VFX.VisualEffectSubgraphBlock>(),
                ".vfxoperator" => ScriptableObject.CreateInstance<UnityEditor.VFX.VisualEffectSubgraphOperator>(),
                _ => throw new VfxValidationException("validation_error",
                    $"Unsupported extension: {ext}. Use .vfx, .vfxop, or .vfxoperator.", null)
            };

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            return new JObject
            {
                ["created"] = new JObject { ["path"] = path, ["kind"] = ext.TrimStart('.') }
            };
        }

        private static object List(JObject @params)
        {
            // AssetDatabase.FindAssets("t:VisualEffectAsset") etc.
            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            var arr = new JArray();
            foreach (var guid in guids)
            {
                arr.Add(new JObject
                {
                    ["guid"] = guid,
                    ["path"] = AssetDatabase.GUIDToAssetPath(guid),
                });
            }
            return new JObject { ["assets"] = arr };
        }

        private static object Delete(JObject @params)
        {
            VfxKernelContainer.BusyGate.EnsureIdle();
            string path = @params.Value<string>("path");
            if (!AssetDatabase.DeleteAsset(path))
                throw new VfxValidationException("asset_not_found", $"Could not delete {path}", null);
            return new JObject { ["deleted"] = path };
        }

        private static object Assign(JObject @params)
        {
            // Assign a .vfx asset to a GameObject's VisualEffect component
            string path = @params.Value<string>("path");
            string goName = @params.Value<string>("gameObject");
            var go = GameObject.Find(goName) ?? throw new VfxValidationException(
                "not_found", $"GameObject '{goName}' not found", null);
            var comp = go.GetComponent<UnityEngine.VFX.VisualEffect>()
                ?? go.AddComponent<UnityEngine.VFX.VisualEffect>();
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(path);
            comp.visualEffectAsset = asset;
            return new JObject { ["assigned"] = path };
        }
    }
}
```

- [ ] **Step 2: Commit**

### Task 4A-2: VfxGraphTool

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxGraphTool.cs`

Actions: `get_info`, `save`, `compile`, `compilation_status`, `read_console`, `set_space`, `set_capacity`, `set_bounds`, `set_data_settings`, `discard_changes`, `get_health`.

- [ ] **Step 1: Implement skeleton following VfxAssetTool pattern**

Follow the same `HandleCommand` → action switch → per-action method pattern. Each action method:
- Calls `VfxKernelContainer.BusyGate.EnsureIdle()` if mutating
- Opens a `VfxTransaction` if mutating
- Calls into `VfxKernelContainer.NodeOps` or reads via `AssetDatabase`/`VFXGraph`
- Returns shape via `VfxKernelContainer.Shaper.Shape`/`ShapeRead`

Representative `discard_changes` implementation (the one action that's fully defined in NodeOps stub):

```csharp
private static object DiscardChanges(JObject @params)
{
    string path = @params.Value<string>("graph") ?? throw new VfxValidationException(
        "missing_required_param", "graph is required", null);
    VfxKernelContainer.BusyGate.EnsureIdle();
    VfxKernelContainer.NodeOps.DiscardChanges(path);
    return new JObject { ["discarded"] = path };
}
```

- [ ] **Step 2: Commit**

### Task 4A-3: VfxDiagTool

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxDiagTool.cs`

Actions: `list_node_types`, `list_block_types`, `list_contexts`, `list_attributes`, `list_settings`, `list_subgraphs`, `read_console`, `get_warnings`. All read-only; no BusyGate, no transaction.

- [ ] **Step 1: Implement**

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxDiagTool.cs
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Generated;
using SpiralingStudio.VfxMcp.Kernel;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_diag", AutoRegister = true)]
    public static class VfxDiagTool
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params.Value<string>("action") ?? "";
            int page = @params.Value<int?>("page") ?? 0;
            const int PageSize = 50;

            return action.ToLowerInvariant() switch
            {
                "list_node_types" => Page(VfxCatalog.Operators, page, PageSize, "nodes"),
                "list_block_types" => Page(VfxCatalog.Blocks, page, PageSize, "blocks"),
                "list_contexts" => Page(VfxCatalog.Contexts, page, PageSize, "contexts"),
                "list_subgraphs" => Page(VfxCatalog.SubgraphOperators, page, PageSize, "subgraph_operators"),
                "read_console" => ReadConsole(@params),
                _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = "unknown_action",
                    Message = $"Unknown vfx_diag action '{action}'",
                })
            };
        }

        private static object Page(string[] items, int page, int size, string key)
        {
            int start = page * size;
            int end = System.Math.Min(start + size, items.Length);
            var arr = new JArray();
            for (int i = start; i < end; i++) arr.Add(items[i]);
            return new JObject
            {
                [key] = arr,
                ["page"] = page,
                ["total"] = items.Length,
                ["has_next"] = end < items.Length,
            };
        }

        private static object ReadConsole(JObject @params)
        {
            var lines = VfxConsoleReader.GetLinesSince(VfxConsoleReader.GetHighWaterMark());
            return new JObject { ["lines"] = new JArray(lines) };
        }
    }
}
```

- [ ] **Step 2: Commit**

---

### Phase 4 Lane B — vfx_node, vfx_block, vfx_property (subagent 4B)

### Task 4B-1: VfxNodeTool + fill VfxNodeOps.AddOperator/RemoveNode/Connect/Disconnect

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxNodeTool.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs`

- [ ] **Step 1: Fill AddOperator body**

```csharp
public string AddOperator(string graphAssetPath, string typeFqn, Vector2 pos)
{
    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(graphAssetPath);
    if (asset == null)
        throw new VfxValidationException("asset_not_found", $"Asset not found: {graphAssetPath}", null);

    var resource = asset.GetResource();
    var graph = resource.GetOrCreateGraph();

    var op = SpiralingStudio.VfxMcp.Generated.VfxNodeWrappers.CreateOperator(typeFqn);
    graph.AddChild(op);

    // Set position
    op.position = pos;

    string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
    return VfxKernelContainer.Identity.Mint(guid, op);
}
```

Remove/Connect/Disconnect follow similar patterns (use `VFXModel.RemoveModel`, `VFXSlot.Link`, etc.). The implementer verifies exact slot linking APIs against VFX Graph source.

- [ ] **Step 2: Tool class**

Pattern identical to VfxAssetTool: action switch → per-action method → kernel calls.

- [ ] **Step 3: Commit**

### Task 4B-2: VfxBlockTool + fill VfxNodeOps.AddBlock

Similar to 4B-1 but for blocks. Key difference: blocks are added via `VFXContext.AddChild(block, index)` where the context is resolved via `VfxIdentity.Resolve`.

### Task 4B-3: VfxPropertyTool + fill VfxNodeOps.AddParameter/SetProperty/GetProperty

Parameters are `VFXParameter` nodes; setting a property value uses `VFXSlot.value = coercedValue` via reflection on the parameter's output slot.

---

### Phase 4 Lane C — vfx_subgraph, vfx_recipe, vfx_batch (subagent 4C)

### Task 4C-1: VfxSubgraphTool + fill VfxNodeOps.AddSubgraphRef

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxSubgraphTool.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs`

- [ ] **Step 1: Implement `AddSubgraphRef`**

```csharp
public string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, Vector2 pos)
{
    var parent = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(parentGraphPath);
    var parentGraph = parent.GetResource().GetOrCreateGraph();

    // Determine subgraph kind from extension
    string ext = System.IO.Path.GetExtension(subgraphAssetPath).ToLowerInvariant();
    UnityEditor.VFX.VFXModel refModel;
    switch (ext)
    {
        case ".vfxoperator":
            refModel = SpiralingStudio.VfxMcp.Generated.VfxNodeWrappers.CreateOperator(
                "UnityEditor.VFX.Operator.VFXSubgraphOperator");
            break;
        case ".vfxop":
            refModel = SpiralingStudio.VfxMcp.Generated.VfxNodeWrappers.CreateContext(
                "UnityEditor.VFX.VFXSubgraphContext");
            break;
        default:
            throw new VfxValidationException("validation_error",
                $"Subgraph asset must be .vfxop or .vfxoperator: got {ext}", null);
    }

    SpiralingStudio.VfxMcp.Generated.VfxSubgraphWrappers.BindAsset(refModel, subgraphAssetPath);
    parentGraph.AddChild(refModel);
    refModel.position = pos;

    string guid = AssetDatabase.AssetPathToGUID(parentGraphPath);
    return VfxKernelContainer.Identity.Mint(guid, refModel);
}
```

- [ ] **Step 2: Tool class with 6 actions** (`create`, `add_ref`, `inline`, `extract`, `get_exposed`, `set_override`)

For phase 4, implement `create`, `add_ref`, `get_exposed`, `set_override` fully. `inline` and `extract` can throw `NotImplementedException("v0.3.1")` as explicit deferred features (these are complex and not in the 500-node MVP).

- [ ] **Step 3: Commit**

### Task 4C-2: VfxRecipeTool (scaffold)

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxRecipeTool.cs`

```csharp
// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxRecipeTool.cs
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace SpiralingStudio.VfxMcp.Tools
{
    /// <summary>
    /// Recipe scaffold. No recipes registered in v0.3.0; use vfx_node/vfx_block for
    /// explicit construction. Recipes are deferred to v0.3.1.
    /// </summary>
    [McpForUnityTool("vfx_recipe", AutoRegister = true)]
    public static class VfxRecipeTool
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params.Value<string>("action") ?? "";
            if (action == "list")
                return new JObject
                {
                    ["recipes"] = new JArray(),
                    ["note"] = "Recipes deferred to v0.3.1. Use vfx_node/vfx_block for explicit construction.",
                };
            return new JObject
            {
                ["error"] = new JObject
                {
                    ["code"] = "unknown_action",
                    ["message"] = $"vfx_recipe only supports 'list' in v0.3.0. Got: {action}",
                }
            };
        }
    }
}
```

### Task 4C-3: VfxBatchTool

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxBatchTool.cs`

The batch tool:
1. Parses `ops` array
2. Opens a single `VfxTransaction` with scope `Batch`
3. For each op, parses `tool` + `action` + payload + `as` alias
4. Resolves `@name` (batch-local) and `$name` (pre-existing) refs per the spec
5. Dispatches to the matching per-tool handler (reusing `HandleCommand` directly would re-open transactions; instead a separate internal "apply without transaction" method is needed — add one per tool class during phase 4 lane execution)
6. Commits once at the end

Due to complexity, phase 4 lane C must work closely with lane B to add `internal static object ApplyInTransaction(JObject op, VfxTransactionScope scope)` methods to each tool class. Lane C owns the batch dispatcher; lane B owns adding `ApplyInTransaction` to its three tools; lane A owns adding it to its three.

**Coordination point:** before lane execution, commit a task `4-COORD` that adds `ApplyInTransaction` method stubs (throwing `NotImplementedException`) to all 9 tool classes, so lanes B and C can fill them in parallel without adding new files.

Full batch implementation: ~150 lines. Not expanded here; the lane C subagent writes it following the kernel contracts.

- [ ] **Step 1 (4C-3): Write `VfxBatchTool` + commit**

---

## Phase 4a — Max-effort code review

### Task 4a-1: Spawn code-reviewer subagent

- [ ] **Step 1: Dispatch `superpowers:code-reviewer` subagent with this prompt**

Prompt:
```
Review the VFX Graph MCP v0.3.0 rebuild against:
  - docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md (spec)
  - docs/spec-review-vfx-graph-mcp-redesign.md (external review 1)
  - docs/objective-review-vfx-graph-mcp-redesign.md (external review 2)
  - docs/vfx_graph_mcp_tool_gaps.md (gaps input)
  - CLAUDE.md (project conventions)

Scope: commits since tag v0.2-pre-rebuild on branch vfxgraph-rebuild-v0.3.

Specific things to validate:
1. Every gap in the gaps doc is structurally addressed (not just acknowledged)
2. Every finding in both external reviews is addressed or pushed back with reasoning
3. Generated catalog count matches VFXLibrary.Get*() counts exactly
4. Kernel code does NO runtime reflection on UnityEditor.VFX.*
5. No tool class echoes input or uses verbose response envelopes
6. No identity writes to m.label, m.name, or asset-resident fields
7. The InternalsVisibleTo patch exists at
   Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs
8. The three-part health gate (YAML + compile + console) runs on every commit
9. The 9 tools all follow the same HandleCommand → action switch pattern
10. No placeholder-shaped code (NotImplementedException in wired paths)

Report findings in a new doc at docs/superpowers/specs/2026-04-07-rebuild-phase4-review.md.
Findings marked BLOCKING must be resolved before phase 5 begins.
```

Use effort: max.

- [ ] **Step 2: Resolve blocking findings in follow-up commits**

- [ ] **Step 3: Commit the review doc**

---

## Phase 5 — Override layer fill-in, perf tests, busy tests

### Task 5-1: Populate Quirks.yaml from phase 4 dogfooding

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Quirks.yaml`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/OverridesEmitter.cs`

- [ ] **Step 1: Identify quirks from phase 4 smoke tests**

The phase 4 smoke tests (task 4A-X) will surface name collisions (`Lerp`, etc.), hidden settings, and aliasing needs. Collect them into Quirks.yaml:

```yaml
# Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Quirks.yaml
name_collisions:
  Lerp:
    prefer: UnityEditor.VFX.Operator.Lerp
    also_known_as:
      - UnityEngine.UIElements.Experimental.Lerp  # rejected

defaults:
  UnityEditor.VFX.Operator.VFXInlineOperator:
    m_Type: UnityEngine.Vector3   # gap #15

aliases:
  settings:
    hlslCode: m_HLSLCode          # gap #18
```

- [ ] **Step 2: Extend OverridesEmitter to read YAML and emit lookup tables**

- [ ] **Step 3: Regenerate + test + commit**

### Task 5-2: Populate Hints.yaml

- [ ] **Step 1: Create Hints.yaml with templates for every error code**

```yaml
hints:
  setting_used_as_property:
    template: "'{name}' on {type} is a setting, not a property. Use vfx_node.set_setting."
  name_collision:
    template: "'{name}' is ambiguous. Candidates: {candidates}. Use the qualified name."
  # ... one entry per error code in Section "Stable error codes" of the spec
```

- [ ] **Step 2: Wire hint lookup into VfxResponseShaper.ShapeError**

- [ ] **Step 3: Commit**

### Task 5-3: Performance test suite

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxPerformanceTests.cs`
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Fixtures/FiveHundredNodeGraph.cs`

- [ ] **Step 1: Write a fixture that generates a 500-node `.vfx` asset**

- [ ] **Step 2: Write perf tests asserting p95 targets from spec Performance Budgets section**

- [ ] **Step 3: Commit**

### Task 5-4: Override layer tests

Per spec test category 13: quirks resolve correctly, hints fire on right error codes.

### Task 5-5: Busy gate tests expanded

Trigger synthetic compilation to exercise `VfxBusyException` retry hint path.

### Task 5-6: No-reflection static analyzer test

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNoReflectionTests.cs`

- [ ] **Step 1: Scan kernel files for forbidden patterns**

```csharp
using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNoReflectionTests
    {
        [Test]
        public void Kernel_HasNoRuntimeReflection_OnVFXTypes()
        {
            const string kernelDir = "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel";
            var forbidden = new Regex(@"typeof\(VFX\w+\)\s*\.(GetMethod|GetField|GetProperty|InvokeMember)");
            foreach (var file in Directory.GetFiles(kernelDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                // Exclude VfxStructuralFingerprint.cs from the check because it uses
                // type.FullName string comparison (not runtime reflection)
                if (file.EndsWith("VfxStructuralFingerprint.cs")) continue;
                Assert.IsFalse(forbidden.IsMatch(content),
                    $"{file}: contains forbidden runtime reflection on VFX types. " +
                    "The spec requires all VFX type access go through the generated catalog.");
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

---

## Phase 6 — Production-ready validation

### Task 6-1: Run the full release gate

- [ ] **Step 1: Run all test categories via `mcp__UnityMCP__run_tests`**

Verify all 18 test categories green:
- Codegen determinism
- Generator self-check
- Catalog completeness
- Catalog round-trip
- Coercer round-trip
- Identity
- Token-savings
- YAML verifier
- Compile gate
- Console correlator
- Performance
- Busy gate
- Override layer
- Subgraph lifecycle
- Smoke E2E
- Discard changes
- Batch abort
- No reflection

- [ ] **Step 2: Confirm 13 release-gate criteria**

Manually walk through each criterion from spec "Production-ready release gate" section and check it off.

- [ ] **Step 3: Fix anything failing**

### Task 6-2: End-to-end smoke test

**Files:**
- Create: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxSmokeTests.cs`

Build a complete particle system end-to-end using the 9 tools in sequence. The test exercises every tool at least once.

- [ ] **Step 1: Write the E2E test**

The implementer constructs a minimal thruster VFX:
1. `vfx_asset.create` → new .vfx
2. `vfx_node.add` → Spawn context
3. `vfx_node.add` → Initialize context
4. `vfx_node.add` → Update context
5. `vfx_node.add` → QuadOutput context
6. `vfx_block.add` → SetAttribute (size, color, lifetime)
7. `vfx_node.add` → Constant operators
8. `vfx_node.connect` → wire operators to attribute blocks
9. `vfx_property.add` → expose intensity as float
10. `vfx_property.set_value` → set intensity default
11. `vfx_graph.save`
12. `vfx_graph.compile`
13. `vfx_graph.compilation_status` → assert ok

- [ ] **Step 2: Run + commit**

---

## Phase 7 — Cleanup

### Task 7-1: Delete old files

**Files:**
- Delete: 30 files under `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/` except `VfxConsoleReader.cs` (move to `Kernel/`)
- Delete: 5 old test files under `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/`

- [ ] **Step 1: Move VfxConsoleReader.cs to Kernel/**

Run:
```bash
git mv Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxConsoleReader.cs Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleReader.cs
```

Update the namespace inside the file to `SpiralingStudio.VfxMcp.Kernel`.

- [ ] **Step 2: Adapt for high-water mark semantics**

Add `GetHighWaterMark()` and `GetLinesSince(object mark)` methods per the phase 3 contracts.

- [ ] **Step 3: Delete the old Vfx/ directory**

```bash
git rm -r Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx
```

- [ ] **Step 4: Delete the 5 old test files**

```bash
git rm Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxActionsTests.cs
git rm Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphReflectionCacheTests.cs
git rm Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphResultMapperTests.cs
git rm Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxInputValidationTests.cs
git rm Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxToolContractTests.cs
```

- [ ] **Step 5: Verify no references to deleted files**

Run the full test suite. If any reference lingers, the compiler fails.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Phase 7: delete legacy addon files; move VfxConsoleReader to Kernel/"
```

### Task 7-2: Bump package version + CHANGELOG

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/package.json`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md`

- [ ] **Step 1: Bump version to 0.3.0**

- [ ] **Step 2: Write CHANGELOG entry**

```markdown
## [0.3.0] - 2026-04-07

### Added
- **Complete rebuild.** Production-ready VFX Graph MCP tool suite replacing
  manage_vfx + manage_vfx_graph + inspect_vfx_asset with 9 grouped tools:
  vfx_asset, vfx_graph, vfx_node, vfx_block, vfx_property, vfx_subgraph,
  vfx_recipe (scaffold), vfx_batch, vfx_diag
- Generator-driven typed catalog (walks VFXLibrary.Get*() descriptors;
  zero runtime reflection)
- Sidecar + structural-fingerprint identity (no asset mutation)
- Three-part health gate: YAML diff + compile/error-manager + console correlation
- First-class subgraph support (.vfxop, .vfxoperator)
- Token-savings response shaping (verbose: true|false, compact tokens)
- Performance budgets for 500-node graphs

### Changed
- Soft-forked com.unity.visualeffectgraph via one-line InternalsVisibleTo patch
  at Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs

### Removed
- Old manage_vfx mega-tool and the accidentally-bundled ParticleSystem/
  LineRenderer/TrailRenderer code (covered by MCPForUnity's manage_components)
- manage_vfx_graph, inspect_vfx_asset

### References
- Design spec: docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md
- Input reviews: docs/spec-review-vfx-graph-mcp-redesign.md,
                 docs/objective-review-vfx-graph-mcp-redesign.md
- Gap doc (forward to v0.3.1): docs/vfx_graph_mcp_tool_gaps.md
```

- [ ] **Step 3: Commit**

### Task 7-3: Merge decision

- [ ] **Step 1: Run the full test suite one more time**

- [ ] **Step 2: Verify the release gate (all 13 criteria)**

- [ ] **Step 3: Optionally merge or keep branch as the shipping branch**

Per the spec's rollout section, the rebuild branch becomes the shipping branch. No merge to main is required for personal use.

---

## Appendix A — File structure recap

Final structure after phase 7:

```
Packages/com.spiralingstudio.mcp.vfxgraph/
├── Editor/
│   ├── VfxAssemblyInternals.cs
│   ├── Generation/
│   │   ├── VfxCatalogGenerator.cs
│   │   ├── VfxLibraryWalker.cs
│   │   ├── VfxSlotTreeBuilder.cs
│   │   ├── VfxQuirksLoader.cs
│   │   ├── CatalogIR.cs
│   │   ├── Quirks.yaml
│   │   ├── Hints.yaml
│   │   └── Emitters/
│   │       ├── CatalogEmitter.cs
│   │       ├── CoercersEmitter.cs
│   │       ├── WrappersEmitter.cs
│   │       ├── SubgraphWrappersEmitter.cs
│   │       ├── SchemasEmitter.cs
│   │       └── OverridesEmitter.cs
│   ├── Generated/
│   │   ├── VfxCatalog.g.cs
│   │   ├── VfxCoercers.g.cs
│   │   ├── VfxNodeWrappers.g.cs
│   │   ├── VfxSubgraphWrappers.g.cs
│   │   ├── VfxToolSchemas.g.cs
│   │   ├── VfxOverrides.g.cs
│   │   └── .catalog-version
│   ├── Kernel/
│   │   ├── VfxKernelContracts.cs
│   │   ├── VfxKernelContainer.cs
│   │   ├── VfxIdentity.cs
│   │   ├── VfxIdentitySidecar.cs
│   │   ├── VfxStructuralFingerprint.cs
│   │   ├── VfxNodeOps.cs
│   │   ├── VfxResponseShaper.cs
│   │   ├── VfxTransaction.cs
│   │   ├── VfxYamlVerifier.cs
│   │   ├── VfxCompileGate.cs
│   │   ├── VfxConsoleCorrelator.cs
│   │   ├── VfxBusyGate.cs
│   │   └── VfxConsoleReader.cs
│   └── Tools/
│       ├── VfxAssetTool.cs
│       ├── VfxGraphTool.cs
│       ├── VfxNodeTool.cs
│       ├── VfxBlockTool.cs
│       ├── VfxPropertyTool.cs
│       ├── VfxSubgraphTool.cs
│       ├── VfxRecipeTool.cs
│       ├── VfxBatchTool.cs
│       └── VfxDiagTool.cs
├── Tests/Editor/
│   ├── Generation/…
│   ├── Catalog/…
│   ├── Kernel/…
│   ├── Tools/…
│   └── Fixtures/…
├── CHANGELOG.md
├── package.json
└── README.md

Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs  (patch)
```

## Appendix B — Parallel execution notes

Phases 3 and 4 are designed for subagent-driven parallel execution. Key rules:

1. **Kernel contracts** (`VfxKernelContracts.cs`) are LOCKED before parallel work starts. Any change requires main-agent approval and a new contracts commit.
2. **No lane may modify another lane's files.** Lanes touch their own file set exclusively.
3. **Shared files** (`VfxCatalogGenerator.cs`, `VfxNodeOps.cs`, `VfxKernelContainer.cs`) are owned by one specific lane; other lanes DO NOT modify them. Lane 3A owns `VfxCatalogGenerator.cs` during phase 3. Phase 4 lanes B/C coordinate via task 4-COORD before modifying `VfxNodeOps.cs`.
4. **Integration point**: at the end of each phase, the main agent runs the full test suite. Any cross-lane compile errors are fixed by the main agent, not a lane.
5. **Code review** (phase 4a) runs against the integrated result, not per-lane.
