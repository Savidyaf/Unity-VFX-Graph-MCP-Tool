# VFX MCP Package Upgrade — Execution Plan (Trimmed)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the upgrade portion of `docs/superpowers/specs/2026-04-07-vfx-mcp-upgrade-design.md` so `Packages/com.spiralingstudio.mcp.vfxgraph` compiles and runs against MCPForUnity 9.6.6 on Unity 6.x — with the testing infrastructure expansion **deliberately deferred**. Manual testing replaces the new automated test suite.

**Architecture:** Mechanical refactor only — namespace migration, response-envelope adapter, dependency declaration, dead-code prune, documentation rewrite. The package's behavior is unchanged. Existing tests get the bare minimum updates (using-directive swap and assertion walks for the new envelope shape) so the build stays green; no new tests are added.

**Tech Stack:** C# / Unity 6.x Editor scripting, MCP for Unity 9.6.6, VFX Graph 17.4.0, URP 17.4.0, Newtonsoft.Json 3.2.2, NUnit (existing tests only).

---

## Scope decisions (departures from the source spec)

The source spec's full migration order is 14 steps. This execution plan **drops or modifies** the test-related work per the user's "manual testing preferred" directive:

| Spec step | Disposition here | Why |
|---|---|---|
| 1 (worktree) | **Modified** — work directly in this checkout on a new branch off `main`. No worktree. | User opted out of worktree isolation for this run. |
| 2 (install MCPForUnity 9.6.6) | Kept | Hard prerequisite — package can't compile without it. |
| 3 (namespace sweep) | Kept | Hard prerequisite — type collisions otherwise. |
| 4 (TDD red on envelope tests) | **Dropped** | No red-first ceremony. Existing envelope tests get assertions updated *together with* the production change in a single commit. |
| 5 (envelope adapter migration) | Kept (production side only) | The production change is the substantive upgrade; bundled with test assertion updates. |
| 6 (Group fields) | Kept | One-line edits, important for tool visibility. |
| 7 (dead-code prune) | Kept | Required for the "VFX-Graph-only" goal. |
| 8 (RegistrationSmokeTests.cs) | **Dropped** | New test file; out of scope. |
| 9 (ManageVfxGraphFlowTests.cs) | **Dropped** | New test file; out of scope. |
| 10 (package.json bump) | Kept | Required for UPM resolution. |
| 11 (package README/CHANGELOG) | Kept | Required for the docs to match reality. |
| 12 (project root README) | Kept | Required for the docs to match reality. |
| 13 (final verification grep gate) | Kept | Evidence-before-assertion. |
| 14 (handoff) | Kept | Branch-finishing options at the end. |

The five existing test files (`VfxActionsTests.cs`, `VfxGraphReflectionCacheTests.cs`, `VfxInputValidationTests.cs`, `VfxToolContractTests.cs`, `VfxGraphResultMapperTests.cs`) **stay** and get only mechanical updates — `using` directive swap on all five, plus assertion walks into `.data` on the two envelope ones — so they still compile and pass after the refactor. No assertions about new behavior.

---

## File map

**Branch & dependency files:**
- Modify: `Packages/manifest.json` — add `com.coplaydev.unity-mcp` 9.6.6
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/package.json` — version bump, dep mins, declare MCPForUnity dep

**Production source (namespace migration target — 17 files):**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.asmdef` (`rootNamespace`)
- Modify all 17 `.cs` files under `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/` that survive the prune (see Files-to-keep list below) — namespace declaration change
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxGraph.cs` — namespace + add `Group = "core"`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/InspectVFXAsset.cs` — namespace + add `Group = "vfx"`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxToolContract.cs` — namespace + envelope adapter rewrite (the substantive code change)
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxCommon.cs` — namespace + utility-only header comment
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphCommon.cs` — namespace + utility-only header comment
- (No change: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/VfxAssemblyInternals.cs`)

**Production source (delete — 14 source + 14 metas):**
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVFX.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleCommon.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleControl.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleRead.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleWrite.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineCreate.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineRead.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineWrite.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailControl.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailRead.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailWrite.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphControl.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphRead.cs(.meta)`
- Delete: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphWrite.cs(.meta)`

**Files to keep (production, 17 .cs files):** `ManageVfxGraph.cs`, `InspectVFXAsset.cs`, `VfxGraphActionRouter.cs`, `VfxGraphEdit.cs`, `VfxGraphEditActions.cs`, `VfxGraphAssets.cs`, `VfxAttributeAliases.cs`, `VfxGraphPersistenceService.cs`, `VfxGraphReflectionCache.cs`, `VfxToolContract.cs`, `VfxGraphResultMapper.cs`, `VfxInputValidation.cs`, `VfxPipelineSupport.cs`, `VfxConsoleReader.cs`, `VfxGraphExceptions.cs`, `ManageVfxCommon.cs`, `VfxGraphCommon.cs`.

**Tests (no new files; mechanical updates only):**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.Tests.asmdef` — set `rootNamespace`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxActionsTests.cs` — `using` swap only
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphReflectionCacheTests.cs` — `using` swap only
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxInputValidationTests.cs` — `using` swap only
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxToolContractTests.cs` — `using` swap + assertion walks into `.data`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphResultMapperTests.cs` — `using` swap + assertion walks into `.data`

**Documentation:**
- Rewrite: `Packages/com.spiralingstudio.mcp.vfxgraph/README.md`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md` — promote `[Unreleased]` → `[0.1.1]`, add new `[0.2.0]`
- Modify: `README.md` (project root) — drop stale paths, fix upstream link

---

## Task 1: Branch setup

**Files:**
- Git branch: `vfxgraph-package-v0.2`

- [ ] **Step 1: Verify clean working tree**

Run: `git status`
Expected: `nothing to commit, working tree clean` on branch `v3`. If dirty, stop and ask the user.

- [ ] **Step 2: Fetch and create the new branch off `main`**

Run:
```bash
git fetch origin
git checkout -b vfxgraph-package-v0.2 main
```
Expected: `Switched to a new branch 'vfxgraph-package-v0.2'`. Verify with `git branch --show-current`.

- [ ] **Step 3: Confirm starting point**

Run: `git log -1 --oneline`
Expected: tip commit of `main`. Note this hash for the eventual handoff summary.

---

## Task 2: Install MCPForUnity 9.6.6 prereq

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 1: Add the dependency to `manifest.json`**

Edit `Packages/manifest.json`. Inside the existing `"dependencies"` block (alphabetically sorted), add a new entry:
```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
```
Insert it between `"com.unity.collab-proxy"` and `"com.unity.collections"` to maintain alphabetical order (the current file is alphabetically ordered by key).

- [ ] **Step 2: Have the user open Unity once to resolve UPM**

Tell the user: *"Please open Unity once so UPM resolves the new MCPForUnity dependency and writes `packages-lock.json`. Then come back and tell me when it's done. The console will show CS0101 type-collision errors between our `MCPForUnity.Editor.Tools.Vfx.VfxGraphAssets` / `VfxGraphCommon` and the upstream's same-named types — that's expected and resolved by Task 3."*

Wait for user confirmation before continuing.

- [ ] **Step 3: Verify the resolved version in `packages-lock.json`**

Read `Packages/packages-lock.json`. Find the `com.coplaydev.unity-mcp` entry and confirm `"version": "9.6.6"` (or whatever `main` resolved to). If the resolved version differs from 9.6.6, **stop and report it** to the user — the spec pins to 9.6.6 exactly and any drift needs an explicit decision.

- [ ] **Step 4: Commit**

Run:
```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "$(cat <<'EOF'
chore(vfx): install MCPForUnity 9.6.6 prereq for v0.2 upgrade

Adds com.coplaydev.unity-mcp via git URL so the package's asmdef
references can resolve. Expected console state after this commit is
CS0101 type collisions between our MCPForUnity.Editor.Tools.Vfx
namespace and the upstream's — resolved by the next commit's
namespace sweep.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Namespace migration sweep

This is one big atomic commit that resolves all `MCPForUnity.Editor.Tools.Vfx` collisions by moving every kept production file under `SpiralingStudio.Mcp.VfxGraph.Editor`. Test files keep their existing `SpiralingStudio.Mcp.VfxGraph.Tests.Editor` namespace declaration but get their `using` directive swapped.

**Files:**
- Modify (asmdef): `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.asmdef`
- Modify (asmdef): `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.Tests.asmdef`
- Modify (production .cs, all 17): every file in the **Files to keep** list above
- Modify (test .cs, all 5): every file under `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/`

- [ ] **Step 1: Update production asmdef rootNamespace**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.asmdef`. Change the `rootNamespace` field from `"MCPForUnity.Editor.Tools.Vfx"` to `"SpiralingStudio.Mcp.VfxGraph.Editor"`. The `references` block (`MCPForUnity.Editor`, `MCPForUnity.Runtime`, `Newtonsoft.Json`, `Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.VisualEffectGraph.Runtime`) stays unchanged.

- [ ] **Step 2: Update test asmdef rootNamespace**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.Tests.asmdef`. Change the `rootNamespace` field from `""` to `"SpiralingStudio.Mcp.VfxGraph.Tests.Editor"`. Everything else stays.

- [ ] **Step 3: Sweep namespace declarations across all 17 production files**

For **every** `.cs` file under `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/` that is in the **Files to keep** list (17 files), use the Edit tool to change the line:
```csharp
namespace MCPForUnity.Editor.Tools.Vfx
```
to:
```csharp
namespace SpiralingStudio.Mcp.VfxGraph.Editor
```

Do not touch the deletion-list files yet; they get deleted in Task 6 and any namespace edit is wasted work.

After this step, run a Grep gate:
```
Grep pattern: ^namespace MCPForUnity\.Editor\.Tools\.Vfx
glob:         Packages/com.spiralingstudio.mcp.vfxgraph/**/*.cs
```
Expected matches: only the deletion-list files (14 files). Any kept-list file matching is a bug.

- [ ] **Step 4: Add `using MCPForUnity.Editor.Tools;` to the two attribute consumers**

The `[McpForUnityTool]` attribute previously resolved by namespace nesting (we were inside `MCPForUnity.Editor.Tools.Vfx`, parent namespace `MCPForUnity.Editor.Tools` was visible). After the move, we need to import it explicitly.

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxGraph.cs`. Add the line `using MCPForUnity.Editor.Tools;` immediately after the existing `using MCPForUnity.Editor.Helpers;` line. The current using block is:
```csharp
using System;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
```
After:
```csharp
using System;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
```

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/InspectVFXAsset.cs`. Same change. The current using block is:
```csharp
using System.Linq;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
```
After:
```csharp
using System.Linq;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
```

- [ ] **Step 5: Sweep `using` directive in all 5 test files**

For each of the five files under `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/` (`VfxActionsTests.cs`, `VfxGraphReflectionCacheTests.cs`, `VfxInputValidationTests.cs`, `VfxToolContractTests.cs`, `VfxGraphResultMapperTests.cs`), use the Edit tool to change:
```csharp
using MCPForUnity.Editor.Tools.Vfx;
```
to:
```csharp
using SpiralingStudio.Mcp.VfxGraph.Editor;
```
Test namespace declarations (`namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor`) stay unchanged.

- [ ] **Step 6: Add utility-only header comments to the two retained common files**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxCommon.cs`. Above the existing `using` block at the very top of the file, add:
```csharp
// Utility-only retention. Provides parser helpers (ParseVec2/Vector3/Vector4/
// Color/AnimationCurve/Gradient) consumed by VfxGraphEdit. Despite the historical
// "ManageVfx" prefix, this file no longer carries any manage_vfx-specific logic
// and is part of the manage_vfx_graph utility surface. Consolidation candidate
// for 0.3.0+.
```

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphCommon.cs`. Above the existing `using` block at the very top of the file, add:
```csharp
// Utility-only retention. Provides FindVisualEffect() consumed by
// VfxGraphAssets.AssignAsset(). Kept for the manage_vfx_graph surface.
// Consolidation candidate for 0.3.0+.
```

- [ ] **Step 7: Have the user reload Unity to verify clean compile**

Tell the user: *"Please switch to Unity so it recompiles. The CS0101 type-collision errors should now be gone, and the assembly should build green. The two envelope tests will still pass at this point because `VfxToolContract.Success/Error` still returns the old anonymous-object shape — that gets migrated in the next task."*

Wait for user confirmation that compilation is green.

- [ ] **Step 8: Commit**

Run:
```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.asmdef \
        Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/com.spiralingstudio.mcp.vfxgraph.Editor.Tests.asmdef \
        Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ \
        Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/
git commit -m "$(cat <<'EOF'
refactor(vfx): migrate namespace to SpiralingStudio.Mcp.VfxGraph.Editor

Resolves CS0101 collisions with MCPForUnity 9.6.6's own
MCPForUnity.Editor.Tools.Vfx surface. Kept production files move under
SpiralingStudio.Mcp.VfxGraph.Editor; test files keep their existing
SpiralingStudio.Mcp.VfxGraph.Tests.Editor declaration and only swap
their `using` directive. Asmdef rootNamespaces updated to match.

ManageVfxGraph.cs and InspectVFXAsset.cs add `using MCPForUnity.Editor.Tools;`
since the [McpForUnityTool] attribute is no longer reachable via namespace
nesting from the new location.

ManageVfxCommon.cs and VfxGraphCommon.cs get utility-only header
comments so future readers don't get confused by their historical
naming.

Behavior unchanged. Envelope shape migration is the next commit.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Response envelope adapter migration

This is the substantive code change. `VfxToolContract.Success` and `VfxToolContract.Error` switch from anonymous objects to upstream `SuccessResponse` / `ErrorResponse`, with `tool_version` and `error_code` preserved inside the envelope's `data` field. Tests get their assertions walked one level deeper to match — bundled in the same commit because the user opted out of the TDD red ceremony.

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxToolContract.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxToolContractTests.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphResultMapperTests.cs`

- [ ] **Step 1: Verify `SuccessResponse` / `ErrorResponse` exist and have the expected constructor**

Before editing, confirm the upstream types exist with the constructor shape we expect. Run:
```
Grep pattern: class (Success|Error)Response
path:         Library/PackageCache/
glob:         **/Helpers/*.cs
```
Or, if the upstream package isn't cached at that path, search the resolved location of `com.coplaydev.unity-mcp` from `packages-lock.json`. Read the `SuccessResponse.cs` / `ErrorResponse.cs` files and confirm:
- Both expose a public constructor that takes `(string message, object data)` (or the named-param equivalent).
- Both expose a `success` bool that resolves at the envelope top level when serialized via `JObject.FromObject`.
- Both expose `message` and `data` at the envelope top level.

If any of these assumptions is wrong, **stop and report** — the spec's adapter shape needs revision before continuing.

- [ ] **Step 2: Rewrite `VfxToolContract.Success` and `VfxToolContract.Error`**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxToolContract.cs`. The full file currently looks like (with namespace already migrated by Task 3):
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpiralingStudio.Mcp.VfxGraph.Editor
{
    internal static class VfxToolContract
    {
        internal const string ToolVersion = "2.0.0";

        internal static object Success(string message, object data = null, object details = null)
        {
            return new
            {
                success = true,
                error_code = (string)null,
                message,
                data,
                details,
                tool_version = ToolVersion
            };
        }

        internal static object Error(string errorCode, string message, object details = null, object data = null)
        {
            string code = string.IsNullOrEmpty(errorCode) ? VfxErrorCodes.UnknownError : errorCode;
            return new
            {
                success = false,
                error_code = code,
                error = code,
                message,
                data = data ?? details,
                details,
                hint = message,
                tool_version = ToolVersion
            };
        }
    }
    // ... VfxErrorCodes and VfxActions classes unchanged ...
}
```

Add a `using MCPForUnity.Editor.Helpers;` directive at the top (alongside the existing usings) and replace the two methods with:
```csharp
internal static object Success(string message, object data = null, object details = null)
{
    return new SuccessResponse(
        message,
        new
        {
            tool_version = ToolVersion,
            error_code = (string)null,
            data,
            details,
        }
    );
}

internal static object Error(string errorCode, string message, object details = null, object data = null)
{
    string code = string.IsNullOrEmpty(errorCode) ? VfxErrorCodes.UnknownError : errorCode;
    return new ErrorResponse(
        message,
        new
        {
            tool_version = ToolVersion,
            error_code = code,
            error = code,
            data = data ?? details,
            details,
            hint = message,
        }
    );
}
```

Constructor argument names are positional (`message`, `data`) — adjust to named parameters if Step 1's verification showed the upstream constructor uses different names. The `VfxErrorCodes` and `VfxActions` classes below are unchanged.

- [ ] **Step 3: Update `VfxToolContractTests.cs` assertions**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxToolContractTests.cs`. The current four tests assert `tool_version` / `error_code` at the JSON top level. After the envelope migration, those fields live inside `json["data"]`, and the user-supplied `data` argument lives at `json["data"]["data"]`. Replace the tests with:
```csharp
using SpiralingStudio.Mcp.VfxGraph.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor
{
    public class VfxToolContractTests
    {
        [Test]
        public void Success_ReturnsCorrectShape()
        {
            var result = VfxToolContract.Success("ok", new { foo = 1 });
            var json = JObject.FromObject(result);

            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.AreEqual("ok", json["message"].ToString());
            Assert.IsNull(json["data"]["error_code"].Type == JTokenType.Null ? null : json["data"]["error_code"].ToString());
            Assert.AreEqual(1, json["data"]["data"]["foo"].ToObject<int>());
            Assert.AreEqual(VfxToolContract.ToolVersion, json["data"]["tool_version"].ToString());
        }

        [Test]
        public void Error_ReturnsCorrectShape()
        {
            var result = VfxToolContract.Error("test_error", "something broke");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual("test_error", json["data"]["error_code"].ToString());
            Assert.AreEqual("something broke", json["message"].ToString());
            Assert.AreEqual(VfxToolContract.ToolVersion, json["data"]["tool_version"].ToString());
        }

        [Test]
        public void Error_FallsBackToUnknownErrorWhenCodeEmpty()
        {
            var result = VfxToolContract.Error("", "msg");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.UnknownError, json["data"]["error_code"].ToString());
        }

        [Test]
        public void Error_FallsBackToUnknownErrorWhenCodeNull()
        {
            var result = VfxToolContract.Error(null, "msg");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.UnknownError, json["data"]["error_code"].ToString());
        }
    }
}
```

> If the verification in Task 4 Step 1 shows that `SuccessResponse` / `ErrorResponse` serialize differently (e.g. `success`/`message` are nested rather than at top level), adjust the assertion paths to match the actual serialization.

- [ ] **Step 4: Update `VfxGraphResultMapperTests.cs` assertions**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphResultMapperTests.cs`. The mapper delegates to `VfxToolContract.Success/Error` so its outputs are now envelopes too. Replace with:
```csharp
using SpiralingStudio.Mcp.VfxGraph.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor
{
    public class VfxGraphResultMapperTests
    {
        [Test]
        public void Wrap_NullResult_ReturnsError()
        {
            var result = VfxGraphResultMapper.Wrap(null, "test_action");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual(VfxErrorCodes.UnknownError, json["data"]["error_code"].ToString());
            StringAssert.Contains("test_action", json["message"].ToString());
        }

        [Test]
        public void Wrap_SuccessAnonymousObject_ReturnsContractSuccess()
        {
            var raw = new { success = true, message = "done", data = new { id = 42 } };
            var result = VfxGraphResultMapper.Wrap(raw, "add_node");
            var json = JObject.FromObject(result);

            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.AreEqual("done", json["message"].ToString());
            Assert.IsNotNull(json["data"]["tool_version"]);
        }

        [Test]
        public void Wrap_FailureAnonymousObject_ReturnsContractError()
        {
            var raw = new { success = false, message = "Node not found" };
            var result = VfxGraphResultMapper.Wrap(raw, "remove_node");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual(VfxErrorCodes.NotFound, json["data"]["error_code"].ToString());
        }

        [Test]
        public void Wrap_FailureWithExplicitErrorCode_PreservesCode()
        {
            var raw = new { success = false, error_code = VfxErrorCodes.ValidationError, message = "bad input" };
            var result = VfxGraphResultMapper.Wrap(raw, "set_node_property");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.ValidationError, json["data"]["error_code"].ToString());
        }

        [Test]
        public void Wrap_AssetNotFoundRequiresBothKeywords()
        {
            var raw = new { success = false, message = "Created asset successfully" };
            var result = VfxGraphResultMapper.Wrap(raw, "create_asset");
            var json = JObject.FromObject(result);

            Assert.AreNotEqual(VfxErrorCodes.AssetNotFound, json["data"]["error_code"].ToString());
        }
    }
}
```

- [ ] **Step 5: Have the user run the test runner**

Tell the user: *"Please open Unity's Test Runner (Window > General > Test Runner), select EditMode, and run the `com.spiralingstudio.mcp.vfxgraph.Editor.Tests` assembly. All ~17 tests should pass. If any fail, paste me the failures and we'll diagnose before continuing."*

Wait for user confirmation that all existing tests pass green.

- [ ] **Step 6: Commit**

Run:
```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxToolContract.cs \
        Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxToolContractTests.cs \
        Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/VfxGraphResultMapperTests.cs
git commit -m "$(cat <<'EOF'
feat(vfx)!: adopt SuccessResponse/ErrorResponse envelopes

VfxToolContract.Success / VfxToolContract.Error now return upstream
MCPForUnity.Editor.Helpers.SuccessResponse / ErrorResponse with
tool_version and error_code preserved inside the envelope's `data`
field. VfxGraphResultMapper.Wrap is unchanged because it already
delegates to VfxToolContract in every return path.

BREAKING CHANGE: consumers reading response.tool_version /
response.error_code at the top level must now read
response.data.tool_version / response.data.error_code. The CHANGELOG
0.2.0 entry calls this out.

Existing envelope tests updated in the same commit so the package
stays green; no new tests added (manual testing covers the new
surface for this run).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Add `Group` fields to entrypoints

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxGraph.cs`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/InspectVFXAsset.cs`

- [ ] **Step 1: Add `Group = "core"` to `ManageVfxGraph`**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxGraph.cs`. Change:
```csharp
[McpForUnityTool("manage_vfx_graph", AutoRegister = true)]
```
to:
```csharp
[McpForUnityTool("manage_vfx_graph", AutoRegister = true, Group = "core")]
```

- [ ] **Step 2: Add `Group = "vfx"` to `InspectVFXAsset`**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/InspectVFXAsset.cs`. Change:
```csharp
[McpForUnityTool("inspect_vfx_asset", AutoRegister = true)]
```
to:
```csharp
[McpForUnityTool("inspect_vfx_asset", AutoRegister = true, Group = "vfx")]
```

- [ ] **Step 3: Have the user reload Unity to verify the `Group` property exists**

Tell the user: *"Please switch to Unity so it recompiles. If MCPForUnity 9.6.6 doesn't expose a `Group` property on `McpForUnityToolAttribute`, this commit will fail with a clear `'McpForUnityToolAttribute' does not contain a definition for 'Group'` error — that means we need to bump the upstream pin or revisit the design. Otherwise, expect a clean compile."*

Wait for user confirmation.

- [ ] **Step 4: Commit**

Run:
```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVfxGraph.cs \
        Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/InspectVFXAsset.cs
git commit -m "$(cat <<'EOF'
feat(vfx): set [McpForUnityTool] Group fields

manage_vfx_graph is core (always visible), inspect_vfx_asset is vfx
(opt-in via the manage_tools meta-tool). Matches the upstream
9.6.6 grouping convention.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Dead-code prune

Delete the 14 source files + 14 metas confirmed unreachable from the kept set.

**Files:** see deletion list in the File Map.

- [ ] **Step 1: Delete the 14 source files and their metas**

Run a single `git rm` to remove them all in one go (so Unity sees the deletion atomically rather than partial):
```bash
git rm \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVFX.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ManageVFX.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleCommon.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleCommon.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleControl.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleControl.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleRead.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleRead.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleWrite.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/ParticleWrite.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineCreate.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineCreate.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineRead.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineRead.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineWrite.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/LineWrite.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailControl.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailControl.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailRead.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailRead.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailWrite.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/TrailWrite.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphControl.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphControl.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphRead.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphRead.cs.meta \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphWrite.cs \
  Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/VfxGraphWrite.cs.meta
```

- [ ] **Step 2: Have the user reload Unity to verify clean compile**

Tell the user: *"Please switch to Unity. The deleted files were already in their own namespace island after Task 3, so nothing in the kept set should reference them. If a CS0246 'type not found' fires, paste me the error and we'll restore the missing file — but the dead-code map says this won't happen."*

Wait for user confirmation.

- [ ] **Step 3: Commit**

Run:
```bash
git commit -m "$(cat <<'EOF'
refactor(vfx)!: prune legacy manage_vfx surface (14 files)

Deletes the legacy ParticleSystem/LineRenderer/TrailRenderer/VFX
Graph R-W-C source files that were unreachable from the kept set.
The package is now VFX-Graph-only with no overlap with the upstream's
built-in manage_vfx tool.

BREAKING CHANGE: the manage_vfx MCP tool is no longer registered by
this package. Downstream consumers calling manage_vfx should switch
to the upstream's built-in equivalent or to manage_vfx_graph.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Update `package.json`

**Files:**
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/package.json`

- [ ] **Step 1: Bump version and dependency mins, declare MCPForUnity prereq**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/package.json`. The current file is:
```json
{
  "name": "com.spiralingstudio.mcp.vfxgraph",
  "version": "0.1.0",
  "displayName": "Spiraling Studio MCP VFX Graph Tools",
  "description": "URP-first VFX Graph custom tools for MCP for Unity.",
  "unity": "2021.3",
  "author": {
    "name": "Spiraling Studio"
  },
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.render-pipelines.universal": "17.0.0",
    "com.unity.visualeffectgraph": "17.0.0"
  },
  "keywords": [
    "mcp",
    "unity",
    "vfx",
    "urp"
  ]
}
```

Replace with:
```json
{
  "name": "com.spiralingstudio.mcp.vfxgraph",
  "version": "0.2.0",
  "displayName": "Spiraling Studio MCP VFX Graph Tools",
  "description": "URP-first VFX Graph custom tools for MCP for Unity.",
  "unity": "2021.3",
  "author": {
    "name": "Spiraling Studio"
  },
  "dependencies": {
    "com.coplaydev.unity-mcp": "9.6.6",
    "com.unity.nuget.newtonsoft-json": "3.2.2",
    "com.unity.render-pipelines.universal": "17.4.0",
    "com.unity.visualeffectgraph": "17.4.0"
  },
  "keywords": [
    "mcp",
    "unity",
    "vfx",
    "urp"
  ]
}
```

> The `unity` field is intentionally **not** changed by this task — the spec defers it to the user post-handoff. After commit, remind the user that they should bump it to their Unity 6.x version if they want UPM to enforce the editor minimum.

- [ ] **Step 2: Commit**

Run:
```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/package.json
git commit -m "$(cat <<'EOF'
chore(vfx): bump package to 0.2.0 with new dep mins

- version: 0.1.0 -> 0.2.0
- declare com.coplaydev.unity-mcp 9.6.6 as explicit prereq
- com.unity.visualeffectgraph: 17.0.0 -> 17.4.0
- com.unity.render-pipelines.universal: 17.0.0 -> 17.4.0
- com.unity.nuget.newtonsoft-json: 3.2.1 -> 3.2.2

Mins pinned to packages-lock.json's resolved versions, which is
what the package actually compiles against. The `unity` field is
left at 2021.3 for the user to bump to their 6.x value separately.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Rewrite package README and update CHANGELOG

**Files:**
- Rewrite: `Packages/com.spiralingstudio.mcp.vfxgraph/README.md`
- Modify: `Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md`

- [ ] **Step 1: Rewrite the package README**

Replace `Packages/com.spiralingstudio.mcp.vfxgraph/README.md` with the following content. Key changes vs current:
- Drop the "Source of truth and compatibility" section entirely (mirror/sync/CI workflow no longer exist).
- Drop the line about `.github/workflows/unity-editmode-tests.yml` from quality standards.
- Add a "Tool registrations" section.
- Add a "Compatibility" section.
- Add a "Response envelope" section.
- Rewrite "Installation and setup" with concrete steps including the MCPForUnity git URL.
- Drop references to deleted `VfxGraphRead`/`Write`/`Control` from the architecture section.
- Add a one-line link to the gaps doc under "Known constraints".

```markdown
# Spiraling Studio MCP VFX Graph Tools

`com.spiralingstudio.mcp.vfxgraph` is a URP-first VFX Graph extension package for MCP for Unity. It registers two custom MCP tools — `manage_vfx_graph` and `inspect_vfx_asset` — and is intentionally scoped to VFX Graph only (no overlap with the upstream's built-in `manage_vfx` ParticleSystem/LineRenderer/TrailRenderer surface).

## Purpose

This package enables AI agents to build and edit Unity VFX Graph assets through MCP actions, including graph topology, properties, blocks, GPU-event links, GraphicsBuffer pipelines, ECS integration, and runtime assignment workflows.

## Tool registrations

| Tool | Group | Description |
|---|---|---|
| `manage_vfx_graph` | `core` | Always visible. Primary entrypoint for all VFX Graph editing actions (add/remove nodes, set properties, add blocks, configure outputs, batch ops, recipe builds, save/compile). |
| `inspect_vfx_asset` | `vfx` | Opt-in via the `manage_tools` meta-tool. Returns a full diagnostic dump (graph info + properties + connections + compilation status + attributes) for any VFX asset. |

## Compatibility

| Dependency | Minimum |
|---|---|
| Unity Editor | 6.x (set in `package.json` `unity` field by the project owner) |
| `com.coplaydev.unity-mcp` | `9.6.6` |
| `com.unity.visualeffectgraph` | `17.4.0` |
| `com.unity.render-pipelines.universal` | `17.4.0` |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` |

## Installation and setup

1. Install MCPForUnity prereq via Unity Package Manager > Add package from git URL:
   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```
2. Install this package — either embedded under `Packages/com.spiralingstudio.mcp.vfxgraph` (current layout in this repo), or via your own git URL once published.
3. Restart Unity and reconnect your MCP client. The `manage_vfx_graph` tool appears in the tool list immediately. To enable `inspect_vfx_asset`, ask your client to call the upstream `manage_tools` meta-tool with the `vfx` group enabled.

## Architecture

- `ManageVfxGraph` is the primary MCP entrypoint (`manage_vfx_graph`).
- `VfxGraphActionRouter` maps action names directly to `VfxGraphEdit` methods.
- `VfxGraphEdit` (partial) is the core implementation for all graph operations.
- `VfxGraphEditActions` extends `VfxGraphEdit` with attribute blocks, batch execution, context settings, output configuration, compilation, custom attributes, bounds, block management, buffer pipelines, and recipe-based creation.
- `VfxAttributeAliases` maps friendly block names (e.g. SetPosition, Turbulence) to internal VFX types and auto-configures settings.
- `VfxGraphPersistenceService` centralizes asset persistence, invalidation, and deferred batch saving.
- `VfxGraphReflectionCache` caches all type resolution and method lookups, including VFXType-attributed struct scanning.
- `VfxToolContract` and `VfxGraphResultMapper` normalize response shapes onto the upstream `SuccessResponse` / `ErrorResponse` envelopes.
- `VfxInputValidation` handles input parameter validation.
- `VfxGraphAssets` manages VFX asset lifecycle (create, assign, list).
- `ManageVfxCommon` and `VfxGraphCommon` are utility-only retentions despite their historical naming — see header comments in those files.

### Response envelope

All responses are upstream `MCPForUnity.Editor.Helpers.SuccessResponse` or `ErrorResponse` instances, with the package's `tool_version` and `error_code` fields preserved inside the envelope's `data` payload:

```jsonc
// SuccessResponse
{
  "success": true,
  "message": "<human readable>",
  "data": {
    "tool_version": "2.0.0",
    "error_code": null,
    "data": { /* user payload */ },
    "details": { /* optional context */ }
  }
}

// ErrorResponse
{
  "success": false,
  "message": "<human readable>",
  "data": {
    "tool_version": "2.0.0",
    "error_code": "validation_error",
    "error": "validation_error",
    "data": { /* optional context */ },
    "details": { /* optional context */ },
    "hint": "<actionable hint>"
  }
}
```

> Consumers reading `response.tool_version` or `response.error_code` at the top level (the 0.1.0 shape) must update to `response.data.tool_version` / `response.data.error_code`.

## Supported actions (`manage_vfx_graph`)

### Introspection

- `get_graph_info` — full graph topology with data connections, block settings, parameter names
- `get_connections` — slot-level data connections
- `list_node_types` — available VFX node types
- `list_block_types` — available VFX block types
- `list_properties` — blackboard properties
- `list_attributes` — built-in + custom attributes with types, defaults, aliases
- `get_node_settings` — settings for a specific node
- `get_compilation_status` — shader compilation errors
- `save_graph`

### Node and slot operations

- `add_node` — with enriched error suggestions for unknown types
- `remove_node`
- `move_node`
- `duplicate_node`
- `connect_nodes`
- `disconnect_nodes`
- `set_node_property`
- `set_node_setting` — with available settings in error responses

### Context and block operations

- `link_contexts`
- `link_gpu_event` — with pre-link reimport for flow slot materialization
- `add_block` — auto-resolves aliases (SetPosition, Turbulence, etc.)
- `add_attribute_block` — dedicated action for SetAttribute with attribute/composition/source/random/channels
- `remove_block`
- `reorder_block` — change block execution order within a context
- `set_block_activation` — enable/disable blocks
- `set_context_settings` — Update context toggles, Spawn loop/delay, any context setting
- `configure_output` — output type, blend mode, orientation, settings
- `set_bounds` — Initialize context AABox bounds
- `set_space`
- `set_capacity`

### Blackboard and code operations

- `add_property`
- `remove_property`
- `set_property_value`
- `set_hlsl_code`

### Custom attributes

- `add_custom_attribute` — add graph-level custom attribute with name, type, description
- `remove_custom_attribute`

### Buffer and ECS integration

- `create_buffer_helper` — low-level GraphicsBuffer helper
- `setup_buffer_pipeline` — composite: creates buffer property + SampleBuffer + configures type + generates VFXType struct code
- `compile_graph` — trigger shader compilation

### Batch and recipes

- `batch` — execute multiple operations in a single call with deferred save and symbolic references ($spawn, $init, etc.)
- `create_from_recipe` — create common graph patterns from templates:
  - `ecs_buffer_particles` — Spawn→Init→Update→Output with GraphicsBuffer property and SampleBuffer
  - `simple_spawn_particles` — basic particle system with lifetime, velocity, color
  - `gpu_event_chain` — parent→child particle system via GPU events
  - `particle_strip_trail` — particle strip trail rendering

### Asset lifecycle operations

- `create_asset`
- `list_assets`
- `list_templates`
- `assign_asset`

### Console

- `read_vfx_console`

## Quality and maintainability standards

- Deterministic contract responses with explicit `error_code` values (now inside the envelope's `data` payload).
- Action routing isolates MCP schema from implementation details.
- Reflection methods use caching to reduce lookup overhead.
- Partial class architecture separates core graph operations from extended actions.
- Attribute alias system is data-driven and easily extensible.
- Batch API defers persistence for performance with symbolic reference resolution.

## Pipeline support

- Guaranteed support: URP.
- Non-URP pipelines return structured `unsupported_pipeline` errors.

## Known constraints

- Unity VFX internals are reflection-driven and can change across Unity versions.
- GPU event flow slot indexing may vary by graph structure; diagnostics include attempted indices.
- Custom attribute API availability depends on VFX Graph version (17+).
- A catalog of known bugs and missing capabilities is tracked in [`docs/vfx_graph_mcp_tool_gaps.md`](../../docs/vfx_graph_mcp_tool_gaps.md). The 0.3.0 release will address them.

## Troubleshooting

- If actions fail with `unsupported_pipeline`, switch project render pipeline to URP.
- If tool results look stale, run `save_graph` and retry introspection actions.
- Use `inspect_vfx_asset` for a full diagnostic dump of any VFX asset.
- Use `list_attributes` to discover available attributes and their SetBlock aliases.
```

- [ ] **Step 2: Update the package CHANGELOG**

Edit `Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md`. The current file has a `[Unreleased]` block (which actually documents the 0.1.x dev cycle's work) and a `[0.1.0]` block. Promote `[Unreleased]` to `[0.1.1] - 2026-04-07` and add a new `[0.2.0] - 2026-04-07` entry above it.

Replace the file content with:
```markdown
# Changelog

## [0.2.0] - 2026-04-07

### Breaking
- Migrated namespace from `MCPForUnity.Editor.Tools.Vfx` to
  `SpiralingStudio.Mcp.VfxGraph.Editor` to avoid collision with the new
  MCPForUnity package's own `MCPForUnity.Editor.Tools.Vfx` surface. Consumers
  must update `using` directives.
- Response envelope now uses `MCPForUnity.Editor.Helpers.SuccessResponse` /
  `ErrorResponse`. The `tool_version` and `error_code` fields are preserved
  but now live inside the envelope's `data` field rather than at the top level.
- **Deregistered the `manage_vfx` MCP tool entirely.** Downstream consumers
  that previously called `manage_vfx` (default Unity ParticleSystem,
  LineRenderer, and TrailRenderer support) will no longer find it in the
  tool list. This is intentional: the package is VFX Graph only and no
  longer overlaps with the upstream's built-in `manage_vfx` tool.

### Removed
- Deleted the 14 source files that implemented the `manage_vfx` surface:
  `ManageVFX.cs`, `Particle{Common,Control,Read,Write}.cs`,
  `Line{Create,Read,Write}.cs`, `Trail{Control,Read,Write}.cs`,
  `VfxGraph{Control,Read,Write}.cs`.

### Added
- Declared `com.coplaydev.unity-mcp` 9.6.6 as an explicit UPM dependency
  so installation surfaces a clean missing-prereq error instead of late
  assembly-resolution failures.
- `[McpForUnityTool]` Group field set: `manage_vfx_graph` is `Group = "core"`
  (always visible), `inspect_vfx_asset` is `Group = "vfx"` (opt-in via the
  `manage_tools` meta-tool).

### Changed
- Bumped minimum dependency versions to match the validated project:
  VFX Graph 17.4.0, URP 17.4.0, Newtonsoft.Json 3.2.2.
- README rewritten: removed stale mirror/sync/CI sections, added concrete
  install instructions including the MCPForUnity git-URL prerequisite,
  added Tool Registrations and Compatibility sections, documented the
  new response envelope shape.
- Existing envelope unit tests (`VfxToolContractTests`, `VfxGraphResultMapperTests`)
  updated to walk into the new envelope `data` payload. No new automated
  tests were added in this release; manual testing covers the upgrade.

### Roadmap
- 0.3.0 will address the bugs and missing capabilities catalogued in
  `docs/vfx_graph_mcp_tool_gaps.md` (silent connection drops in
  `connect_nodes` and batch operations, JArray→Vector conversion in
  `set_property_value`, `Lerp`/type-name disambiguation, block ID stability,
  `KillIf`-style helper block, opinionated GPU-event recipes, etc.).

## [0.1.1] - 2026-04-07

- Promoted `Packages/<package-name>/Editor/Tools/Vfx` as canonical source for VFX MCP tooling.
- Added `scripts/sync_vfx_tools.py` to keep `Assets/MCPForUnity/Editor/Tools/Vfx` as a compatibility mirror.
- Added CI workflow `.github/workflows/vfx-sync-check.yml` to detect package-assets drift.
- Centralized all assembly/type resolution through `VfxGraphReflectionCache` with safe `ReflectionTypeLoadException` handling.
- Cached `InvalidationCause` enum and `Invalidate` method lookups in persistence service.
- Fixed `GuessErrorCode` heuristic that misclassified "asset" messages as `asset_not_found`.
- Added `error_code` to all error returns in `VfxGraphEdit` for deterministic error classification.
- Added top-level exception handler in `ManageVfxGraph.HandleCommand`.
- Collapsed redundant `*Operations` and `*Service` wrapper layers; router now calls `VfxGraphEdit` directly.
- Extracted `TryLoadGraph`, `TryLoadNodeById`, `PersistGraph` helpers to reduce duplication.
- Replaced inline `SetDirty`/`SaveAssets` with centralized `PersistGraph`.
- Added `Debug.LogWarning` to correctness-affecting catch blocks.
- Extracted `FindVfxSettingField` and `ConvertSettingValue` from `SetNodeSetting`.
- Added Python setup step to CI sync-check workflow.
- Added orphan file detection and cleanup to sync script.
- Deduplicated CI test workflow (single filtered VFX test run).
- Converted `GraphActions` to `HashSet` for O(1) lookups; added `IsKnownAction`.
- Fixed null-caching in `GetEditorVfxType` (no longer caches failed lookups).
- Added unit tests for `VfxToolContract`, `VfxGraphResultMapper`, and expanded `VfxInputValidation`/`VfxActions` tests.
- Updated README architecture section to reflect simplified structure.

## [0.1.0] - 2026-02-17

- Added URP-first productionization baseline for VFX MCP tools.
- Added structured response contract with `tool_version` and `error_code`.
- Added `manage_vfx_graph` action routing module boundaries.
- Added validation helpers for required fields and asset paths.
- Added URP compatibility guard with explicit non-URP error messaging.
- Added docs and CI/test scaffolding for release hardening.
```

- [ ] **Step 3: Commit**

Run:
```bash
git add Packages/com.spiralingstudio.mcp.vfxgraph/README.md \
        Packages/com.spiralingstudio.mcp.vfxgraph/CHANGELOG.md
git commit -m "$(cat <<'EOF'
docs(vfx): rewrite package README and CHANGELOG for 0.2.0

README:
- Drop stale mirror/sync/CI sections (those scripts no longer exist)
- Add Tool registrations table (manage_vfx_graph core, inspect_vfx_asset vfx)
- Add Compatibility table with the new dep mins
- Add Response envelope subsection documenting the SuccessResponse /
  ErrorResponse shape and where tool_version / error_code now live
- Rewrite Installation and setup with concrete git-URL steps
- Drop deleted VfxGraphRead/Write/Control from architecture
- Add gaps-doc link under Known constraints

CHANGELOG:
- Promote [Unreleased] to [0.1.1] (the 0.1.x dev cycle's work)
- Add new [0.2.0] block describing the breaking namespace move,
  envelope migration, manage_vfx deregistration, and dep bumps

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Cleanup project root README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace stale paths and dead links**

Edit the project root `README.md`. Drop the `Assets/MCPForUnity/Editor/Tools/Vfx` and `Assets/MCPForUnity/Editor/Tests/Editor` paths from the "Key paths" section (those directories no longer exist). Replace the dead `beta`-branch CUSTOM_TOOLS link with the `main`-branch URL. Add a short note that the package is self-contained.

Replace the file content with:
```markdown
# Unity MPC Tool to support VFX Graph changes in URP

Warning : This is a Work In Progress tool with basic functionality tested. All the code here is written by AI tools in a very short time. It will be update as time goes on.
Update : I decided to scrap this approach to edit the YMAL file directly. This tool will be used to train the AI to build the YMAL editor


## What is included

- `manage_vfx_graph` and `inspect_vfx_asset` custom tool handlers (VFX Graph only — the package no longer registers `manage_vfx`).
- Reflection-backed VFX Graph editing support.
- Structured tool responses on the upstream `SuccessResponse` / `ErrorResponse` envelopes, with `tool_version` and `error_code` preserved inside `data`.
- URP compatibility gating for graph actions.
- UPM package self-contained at `Packages/com.spiralingstudio.mcp.vfxgraph`.

## Key paths

- Package: `Packages/com.spiralingstudio.mcp.vfxgraph`
- Tests (EditMode): `Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor`

## Upstream references

- [unity-mcp repository](https://github.com/CoplayDev/unity-mcp)
- [Custom tool authoring guide](https://github.com/CoplayDev/unity-mcp/blob/main/docs/reference/CUSTOM_TOOLS.md)
```

- [ ] **Step 2: Commit**

Run:
```bash
git add README.md
git commit -m "$(cat <<'EOF'
docs: refresh project root README for v0.2 layout

- Drop stale Assets/MCPForUnity/* paths (no longer exist)
- Note that manage_vfx is no longer registered (VFX Graph only now)
- Document the new envelope shape briefly
- Replace dead beta-branch CUSTOM_TOOLS link with main-branch URL

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Final verification

This is the verification-before-completion checkpoint. Evidence-before-assertions: every claim in the handoff message has to be backed by a command output.

**Files:** verification only — no changes.

- [ ] **Step 1: Grep gate — zero `MCPForUnity.Editor.Tools.Vfx` references in the package**

Run:
```
Grep pattern: MCPForUnity\.Editor\.Tools\.Vfx
path:         Packages/com.spiralingstudio.mcp.vfxgraph/
```
Expected: **zero matches**. Any match is a bug — find and fix before continuing.

- [ ] **Step 2: Grep gate — only allowed `MCPForUnity.` references**

Run:
```
Grep pattern: MCPForUnity\.
path:         Packages/com.spiralingstudio.mcp.vfxgraph/
output_mode:  content
```

Expected matches and only these:
- `using MCPForUnity.Editor.Helpers;` in `VfxToolContract.cs` (and any other file that touches `SuccessResponse` / `ErrorResponse` — historically `ManageVfxGraph.cs` and `InspectVFXAsset.cs` already had this)
- `using MCPForUnity.Editor.Tools;` in `ManageVfxGraph.cs` and `InspectVFXAsset.cs`
- `MCPForUnity.Editor` and `MCPForUnity.Runtime` in the `Editor.asmdef` references block

Anything else (e.g. a leftover `MCPForUnity.Editor.Tools.Vfx.SomeType` reference inside a `using static` directive or qualified type name) is a bug.

- [ ] **Step 3: Grep gate — no orphan `.cs.meta` files for deleted sources**

Run:
```
Glob pattern: Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/Vfx/*.cs.meta
```
For every result, verify the corresponding `.cs` file exists. The 14 deletion-list metas should be gone.

- [ ] **Step 4: Confirm `package.json` is well-formed JSON and version is 0.2.0**

Read `Packages/com.spiralingstudio.mcp.vfxgraph/package.json`. Verify:
- `version` == `"0.2.0"`
- `dependencies."com.coplaydev.unity-mcp"` == `"9.6.6"`
- `dependencies."com.unity.visualeffectgraph"` == `"17.4.0"`
- `dependencies."com.unity.render-pipelines.universal"` == `"17.4.0"`
- `dependencies."com.unity.nuget.newtonsoft-json"` == `"3.2.2"`

- [ ] **Step 5: Have the user run a manual smoke pass**

Tell the user:

> "The mechanical upgrade is done. Please do a manual smoke pass before I write up the handoff:
> 1. Switch to Unity. Confirm the console is clean (no errors / warnings related to this package).
> 2. Open Window > General > Test Runner > EditMode and run the `com.spiralingstudio.mcp.vfxgraph.Editor.Tests` assembly. All tests should pass.
> 3. Reconnect your MCP client. Confirm `manage_vfx_graph` shows up in the tool list immediately. Optionally enable the `vfx` group via `manage_tools` and confirm `inspect_vfx_asset` becomes visible.
> 4. Run a representative `manage_vfx_graph` call (e.g. `create_from_recipe simple_spawn_particles`) against a throwaway path under `Assets/Tests/Tmp/` and verify it returns a `SuccessResponse` envelope with the new `data.tool_version` / `data.error_code` shape.
>
> Let me know once each of those is green, or paste me anything that fails."

Wait for user confirmation. **If anything fails**, diagnose using the systematic-debugging skill before declaring complete.

- [ ] **Step 6: Show git log summary**

Run:
```bash
git log --oneline main..HEAD
```
Expected: 9 commits matching the task structure (one per Task 2 through Task 9, plus the branch creation in Task 1 has no commit). Confirm the branch is at the head you expect.

---

## Task 11: Finishing the development branch

- [ ] **Step 1: Invoke the finishing-a-development-branch skill**

Per the spec's step 14, present the user with structured options for the finished `vfxgraph-package-v0.2` branch (merge to `main`, open a PR, push to remote without merging, etc.). The skill walks through the choices and the user picks.

Use the Skill tool: `superpowers:finishing-a-development-branch`.

---

## Self-review notes

**Spec coverage:**
- G1 (compile green) — Task 3 (namespace) + Task 5 (Group) verified by user reload + Task 10 grep gates ✓
- G2 (only two tools, manage_vfx removed) — Task 5 sets Groups, Task 6 prunes the surface ✓
- G3 (envelope adapter) — Task 4 ✓
- G4 (UPM dep declared) — Task 2 (manifest.json) + Task 7 (package.json) ✓
- G5 (test suite passes) — **Modified**: existing tests only, no new tests. Task 4 step 5 has the user run them. The spec's new flow tests and registration smoke tests are explicitly out of scope per user direction.
- G6 (docs accurate) — Task 8 + Task 9 ✓
- G7 (worktree) — **Modified**: branch in current checkout instead of worktree, per user direction.

**Placeholder scan:** searched for TBD/TODO/"implement later"/"add appropriate" — none. The two intentional placeholders (the `unity` field and the CHANGELOG date) are explicitly flagged in their respective steps.

**Type consistency:** `SuccessResponse` and `ErrorResponse` constructor shape is taken from the spec's adapter snippet. Task 4 step 1 has an explicit verification gate against the actual upstream source — if the constructor signature doesn't match, the implementer stops and reports.

**Manual test gates:** because automated tests are scoped down, there are five "have the user reload Unity / run tests / smoke test" pause points (Tasks 2, 3, 4, 5, 6, 10). These are non-negotiable — every commit that touches code waits on a green Unity reload before moving to the next.
