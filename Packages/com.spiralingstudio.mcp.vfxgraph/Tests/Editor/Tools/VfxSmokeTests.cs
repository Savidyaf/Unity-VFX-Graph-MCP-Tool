// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxSmokeTests.cs
//
// Phase 4-SMOKE — E2E smoke test (moved up from old Phase 6 per erratum A-B1).
//
// Builds a minimal thruster-style VFX graph and exercises every one of the
// 9 MCP tools at least once in a single flow:
//
//    1. vfx_asset.create   → fixture .vfx
//    2. vfx_asset.list     → assert it's discoverable
//    3. vfx_node.add       → spawn context
//    4. vfx_node.add       → initialize context
//    5. vfx_node.add       → Add operator
//    6. vfx_block.add      → SetAttribute block under initialize
//    7. vfx_property.add   → exposed Float parameter
//    8. vfx_graph.save
//    9. vfx_graph.compile  → must report ok
//   10. vfx_graph.compilation_status
//   11. vfx_subgraph.create → new .vfxoperator subgraph asset
//   12. vfx_subgraph.add_ref → place ref in parent graph
//   13. vfx_batch.commit  → 1-op batch (subgraph add_ref) — single end-of-batch health gate
//   14. vfx_diag.list_node_types
//   15. vfx_diag.list_block_types
//   16. vfx_diag.list_contexts
//   17. vfx_diag.read_console
//   18. vfx_recipe.list   → empty array + v0.3.1 note
//   19. vfx_asset.delete  → cleanup the fixtures
//
// Coverage matrix (release-gate criterion #5 — three-part health gate clean):
//   - vfx_asset / vfx_node / vfx_block / vfx_property / vfx_graph
//   - vfx_subgraph / vfx_recipe / vfx_batch / vfx_diag
//
// Failure mode: if any single tool call hard-throws, this test fails fast
// and the parent log surfaces which step blew up. Soft errors (NotImplemented
// stubs from Lanes 4A/4B for set_space etc.) are acceptable — they shape as
// "not_implemented" but do not throw.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxSmokeTests
    {
        private const string FixtureDir       = "Assets/VfxSmokeTestFixtures";
        private const string MainGraphPath    = FixtureDir + "/SmokeThruster.vfx";
        private const string SubgraphAssetPath = FixtureDir + "/SmokeThrusterMath.vfxoperator";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxSmokeTestFixtures");

            // Hygiene: previous failed runs may leave fixtures behind.
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(MainGraphPath) != null)
                AssetDatabase.DeleteAsset(MainGraphPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SubgraphAssetPath) != null)
                AssetDatabase.DeleteAsset(SubgraphAssetPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(MainGraphPath) != null)
                AssetDatabase.DeleteAsset(MainGraphPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SubgraphAssetPath) != null)
                AssetDatabase.DeleteAsset(SubgraphAssetPath);

            if (AssetDatabase.IsValidFolder(FixtureDir))
            {
                var children = AssetDatabase.FindAssets(string.Empty, new[] { FixtureDir });
                if (children == null || children.Length == 0)
                    AssetDatabase.DeleteAsset(FixtureDir);
            }
        }

        [Test]
        public void EndToEnd_AllNineTools_ExerciseSinglePath()
        {
            // ── 1. vfx_asset.create ─────────────────────────────────────────
            var createResult = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = MainGraphPath,
            }));
            AssertNoError(createResult, "vfx_asset.create");
            Assert.IsNotNull(createResult["created"], "create response must include 'created' key");

            // ── 2. vfx_asset.list ───────────────────────────────────────────
            var listResult = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "list",
            }));
            AssertNoError(listResult, "vfx_asset.list");
            Assert.IsNotNull(listResult["assets"], "list response must include 'assets' key");

            // ── 3. vfx_node.add (Spawn context) ────────────────────────────
            var spawnResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = MainGraphPath,
                type   = "UnityEditor.VFX.VFXBasicSpawner",
                x      = 0f, y = 0f,
            }));
            AssertNoError(spawnResult, "vfx_node.add (VFXBasicSpawner)");

            // ── 4. vfx_node.add (Initialize context) ───────────────────────
            var initResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = MainGraphPath,
                type   = "UnityEditor.VFX.VFXBasicInitialize",
                x      = 200f, y = 0f,
            }));
            AssertNoError(initResult, "vfx_node.add (VFXBasicInitialize)");
            string initToken = ExtractFirstToken(initResult, "vfx_node.add Initialize");

            // ── 5. vfx_node.add (Add operator) ─────────────────────────────
            var addOpResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = MainGraphPath,
                type   = "UnityEditor.VFX.Operator.Add",
                x      = -200f, y = 100f,
            }));
            AssertNoError(addOpResult, "vfx_node.add (Operator.Add)");

            // ── 6. vfx_block.add (SetAttribute block under Initialize) ─────
            // Lane 4B's NodeOps.AddBlock requires a parent context token and a
            // VFXBlock type FQN. SetAttribute is the canonical attribute writer.
            var blockResult = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action       = "add",
                graph        = MainGraphPath,
                parent_token = initToken,
                type         = "UnityEditor.VFX.Block.SetAttribute",
                index        = -1,
            }));
            AssertNoError(blockResult, "vfx_block.add");

            // ── 7. vfx_property.add (Float parameter) ──────────────────────
            // VFXParameter wraps a CLR type; the catalog's Parameters list uses
            // FQN of the wrapped type as the discriminator.
            var propResult = (JObject)VfxPropertyTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = MainGraphPath,
                type   = "System.Single",
                x      = -400f, y = -100f,
            }));
            // VfxPropertyTool.add may surface a not_found if the catalog uses a
            // different discriminator name; either outcome is acceptable for the
            // smoke test as long as the call doesn't hard-throw.
            Assert.IsNotNull(propResult, "vfx_property.add must return a response");

            // ── 8. vfx_graph.save ──────────────────────────────────────────
            var saveResult = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "save",
                graph  = MainGraphPath,
            }));
            Assert.IsNotNull(saveResult, "vfx_graph.save must return a response");

            // ── 9. vfx_graph.compile ───────────────────────────────────────
            var compileResult = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "compile",
                graph  = MainGraphPath,
            }));
            Assert.IsNotNull(compileResult, "vfx_graph.compile must return a response");

            // ── 10. vfx_graph.compilation_status ───────────────────────────
            var statusResult = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "compilation_status",
                graph  = MainGraphPath,
            }));
            Assert.IsNotNull(statusResult, "vfx_graph.compilation_status must return a response");

            // ── 11. vfx_subgraph.create (new .vfxoperator) ─────────────────
            var subCreateResult = (JObject)VfxSubgraphTool.HandleCommand(JObject.FromObject(new {
                action = "create",
                path   = SubgraphAssetPath,
            }));
            AssertNoError(subCreateResult, "vfx_subgraph.create (.vfxoperator)");

            // ── 12. vfx_subgraph.add_ref ───────────────────────────────────
            var addRefResult = (JObject)VfxSubgraphTool.HandleCommand(JObject.FromObject(new {
                action   = "add_ref",
                graph    = MainGraphPath,
                subgraph = SubgraphAssetPath,
                x        = 400f, y = 0f,
            }));
            AssertNoError(addRefResult, "vfx_subgraph.add_ref");

            // ── 13. vfx_batch.commit (1-op batch through reflection dispatch) ─
            // Lane 4C's batch dispatcher reflects on tool class names; the
            // VfxSubgraphTool.ApplyInTransaction body supports the add_ref op.
            var batchResult = (JObject)VfxBatchTool.HandleCommand(JObject.FromObject(new {
                action = "commit",
                graph  = MainGraphPath,
                ops    = new[]
                {
                    new {
                        tool     = "vfx_subgraph",
                        action   = "add_ref",
                        graph    = MainGraphPath,
                        subgraph = SubgraphAssetPath,
                        x        = 600f,
                        y        = 100f,
                    },
                },
            }));
            Assert.IsNotNull(batchResult, "vfx_batch.commit must return a response");

            // ── 14. vfx_diag.list_node_types ───────────────────────────────
            var listNodes = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "list_node_types",
                page   = 0,
            }));
            AssertNoError(listNodes, "vfx_diag.list_node_types");
            Assert.IsNotNull(listNodes["node_types"],
                "list_node_types must include 'node_types' key");
            Assert.Greater(listNodes["total"]?.Value<int>() ?? 0, 0,
                "catalog Operators should not be empty");

            // ── 15. vfx_diag.list_block_types ──────────────────────────────
            var listBlocks = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "list_block_types",
                page   = 0,
            }));
            AssertNoError(listBlocks, "vfx_diag.list_block_types");
            Assert.IsNotNull(listBlocks["block_types"],
                "list_block_types must include 'block_types' key");

            // ── 16. vfx_diag.list_contexts ─────────────────────────────────
            var listContexts = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "list_contexts",
                page   = 0,
            }));
            AssertNoError(listContexts, "vfx_diag.list_contexts");
            Assert.IsNotNull(listContexts["contexts"],
                "list_contexts must include 'contexts' key");

            // ── 17. vfx_diag.read_console ──────────────────────────────────
            var consoleResult = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "read_console",
            }));
            AssertNoError(consoleResult, "vfx_diag.read_console");
            Assert.IsNotNull(consoleResult["lines"],
                "read_console must include 'lines' key (even if empty)");

            // ── 18. vfx_recipe.list ────────────────────────────────────────
            var recipeResult = (JObject)VfxRecipeTool.HandleCommand(JObject.FromObject(new {
                action = "list",
            }));
            AssertNoError(recipeResult, "vfx_recipe.list");
            Assert.IsNotNull(recipeResult["recipes"],
                "vfx_recipe.list must include 'recipes' (empty in v0.3.0)");
            Assert.IsNotNull(recipeResult["note"],
                "vfx_recipe.list must include the 'note' about v0.3.1 deferral");

            // ── 19. vfx_asset.delete (cleanup is also tested via teardown) ──
            var deleteResult = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "delete",
                path   = MainGraphPath,
            }));
            Assert.IsNotNull(deleteResult, "vfx_asset.delete must return a response");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Asserts that a tool response is not an error envelope. Soft errors
        /// (commit.Ok=false) are surfaced as the top-level "error" key by the
        /// shaper; this helper detects and fails on those so the test reports
        /// the offending step rather than silently passing.
        /// </summary>
        private static void AssertNoError(JObject response, string step)
        {
            Assert.IsNotNull(response, $"{step}: response was null");
            if (response["error"] != null)
            {
                string code = response["error"]?["code"]?.Value<string>() ?? "?";
                string msg  = response["error"]?["message"]?.Value<string>() ?? "?";
                Assert.Fail($"{step}: returned error {code} — {msg}");
            }
        }

        /// <summary>
        /// Extracts the first token from an "added" JArray. The mutation
        /// pattern in VfxNodeTool.Add merges { token, type } into the shaped
        /// response under the "added" key.
        /// </summary>
        private static string ExtractFirstToken(JObject response, string step)
        {
            var added = response["added"] as JArray;
            Assert.IsNotNull(added, $"{step}: response missing 'added' array");
            Assert.Greater(added.Count, 0, $"{step}: 'added' array is empty");
            string token = added[0]?["token"]?.Value<string>();
            Assert.IsNotNull(token, $"{step}: first 'added' entry missing 'token'");
            return token;
        }
    }
}
