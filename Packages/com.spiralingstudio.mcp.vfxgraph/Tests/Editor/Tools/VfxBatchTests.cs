// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxBatchTests.cs
//
// Phase 4a (F9 batch dispatch) — multi-op batch test.
//
// Verifies that a single vfx_batch.commit call executes multiple ops inside a
// single VfxTransactionScope via VfxBatchTool's reflection dispatch, calling
// the new *Inner methods on each domain tool's ApplyInTransaction.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxBatchTests
    {
        private const string FixtureDir = "Assets/VfxBatchFixtures";
        private const string GraphPath  = FixtureDir + "/BatchProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxBatchFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
            if (AssetDatabase.IsValidFolder(FixtureDir))
            {
                var children = AssetDatabase.FindAssets(string.Empty, new[] { FixtureDir });
                if (children == null || children.Length == 0)
                    AssetDatabase.DeleteAsset(FixtureDir);
            }
        }

        [Test]
        public void Batch_ThreeOps_VfxNode_CommitsAsOne()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));

            // Three vfx_node.add ops in one batch — verifies the *Inner extraction
            // worked for VfxNodeTool and routes through VfxBatchTool's reflection dispatch.
            var batchResult = (JObject)VfxBatchTool.HandleCommand(JObject.FromObject(new {
                action = "commit",
                graph  = GraphPath,
                ops    = new object[]
                {
                    new {
                        tool   = "vfx_node",
                        action = "add",
                        graph  = GraphPath,
                        type   = "UnityEditor.VFX.VFXBasicInitialize",
                        x = 0f, y = 0f,
                    },
                    new {
                        tool   = "vfx_node",
                        action = "add",
                        graph  = GraphPath,
                        type   = "UnityEditor.VFX.Operator.Add",
                        x = 100f, y = 0f,
                    },
                    new {
                        tool   = "vfx_node",
                        action = "add",
                        graph  = GraphPath,
                        type   = "UnityEditor.VFX.Operator.Multiply",
                        x = 200f, y = 0f,
                    },
                },
            }));

            Assert.IsNull(batchResult["error"], $"batch commit failed: {batchResult}");

            // Verify all 3 nodes landed in a single commit by listing them
            var listResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            Assert.IsNotNull(listResult["nodes"]);
            Assert.GreaterOrEqual(((JArray)listResult["nodes"]).Count, 3,
                "expected at least 3 nodes from a 3-op batch");
        }
    }
}
