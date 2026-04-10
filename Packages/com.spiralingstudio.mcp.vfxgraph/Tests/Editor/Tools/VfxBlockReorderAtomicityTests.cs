// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxBlockReorderAtomicityTests.cs
//
// C1 regression — vfx_block.reorder must be atomic on failure. Previously an
// out-of-bounds new_index would leave the block detached from its parent
// context (RemoveChild had already run, AddChild threw). The fix in
// VfxBlockTool.ReorderInner wraps the re-insert in try/catch and restores the
// block at its original index if AddChild fails, then re-throws.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxBlockReorderAtomicityTests
    {
        private const string FixtureDir = "Assets/VfxBlockReorderAtomicityFixtures";
        private const string GraphPath  = FixtureDir + "/ReorderProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxBlockReorderAtomicityFixtures");
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
        public void Reorder_OutOfBoundsIndex_IsAtomic_BlockRemainsInGraph()
        {
            // Fixture: graph + Initialize context + one SetAttribute block.
            var create = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = GraphPath,
            }));
            Assert.IsNull(create["error"], $"vfx_asset.create failed: {create}");

            var ctxResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.VFXBasicInitialize",
                x = 0f, y = 0f,
            }));
            Assert.IsNull(ctxResult["error"], $"vfx_node.add (context) failed: {ctxResult}");
            string ctxToken = (string)ctxResult["added"][0]["token"];
            Assert.IsNotNull(ctxToken);

            var blockResult = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action       = "add",
                graph        = GraphPath,
                parent_token = ctxToken,
                type         = "UnityEditor.VFX.Block.SetAttribute",
                index        = -1,
            }));
            Assert.IsNull(blockResult["error"], $"vfx_block.add failed: {blockResult}");
            string blockToken = (string)blockResult["added"][0]["token"];
            Assert.IsNotNull(blockToken);

            // Act: reorder with a wildly out-of-bounds new_index. Parent has 1
            // block so new_index=99 must fail inside AddChild.
            var reorderResult = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action    = "reorder",
                graph     = GraphPath,
                token     = blockToken,
                new_index = 99,
            }));

            // Assert (1): the call surfaces an error envelope (not silent success).
            Assert.IsNotNull(reorderResult, "vfx_block.reorder must return a response");
            Assert.IsNotNull(reorderResult["error"],
                $"expected error envelope for out-of-bounds new_index, got: {reorderResult}");

            // Assert (2): the block is STILL in the graph after the failed reorder.
            var listResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            Assert.IsNull(listResult["error"], $"vfx_node.list failed: {listResult}");
            var nodes = (JArray)listResult["nodes"];
            Assert.IsNotNull(nodes, "list response must contain 'nodes' array");

            bool blockStillPresent = false;
            foreach (JObject n in nodes)
            {
                if ((string)n["token"] == blockToken)
                {
                    blockStillPresent = true;
                    Assert.AreEqual("block", (string)n["category"],
                        "restored node should still be categorised as a block");
                    Assert.AreEqual(ctxToken, (string)n["parent_token"],
                        "restored block must be reattached to its original parent context");
                    break;
                }
            }
            Assert.IsTrue(blockStillPresent,
                $"block token {blockToken} was lost after failed reorder — " +
                "atomicity guard regressed");
        }
    }
}
