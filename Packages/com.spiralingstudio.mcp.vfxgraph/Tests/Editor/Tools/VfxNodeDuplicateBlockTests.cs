// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxNodeDuplicateBlockTests.cs
//
// W2-C regression: vfx_node.duplicate must dispatch VFXBlock models through
// IVfxNodeOps.AddBlock (not AddOperator). Prior code threw vfx_exception
// because blocks have no top-level Add* path; they require a parent context.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxNodeDuplicateBlockTests
    {
        private const string FixtureDir = "Assets/VfxNodeDuplicateBlockFixtures";
        private const string GraphPath  = FixtureDir + "/DupBlockProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxNodeDuplicateBlockFixtures");
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
        public void Duplicate_OnBlockToken_CreatesSiblingUnderSameContext()
        {
            // Create graph
            var create = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = GraphPath,
            }));
            Assert.IsNull(create["error"], $"vfx_asset.create failed: {create}");

            // Add an Initialize context
            var ctxResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.VFXBasicInitialize",
                x = 0f, y = 0f,
            }));
            Assert.IsNull(ctxResult["error"], $"vfx_node.add (context) failed: {ctxResult}");
            string ctxToken = (string)ctxResult["added"][0]["token"];
            Assert.IsNotNull(ctxToken);

            // Add a SetAttribute block at index 0 under the context
            var blockResult = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action       = "add",
                graph        = GraphPath,
                parent_token = ctxToken,
                type         = "UnityEditor.VFX.Block.SetAttribute",
                index        = 0,
            }));
            Assert.IsNull(blockResult["error"], $"vfx_block.add failed: {blockResult}");
            string blockToken = (string)blockResult["added"][0]["token"];
            Assert.IsNotNull(blockToken);

            // Duplicate the block via vfx_node.duplicate
            var dupResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "duplicate",
                graph  = GraphPath,
                token  = blockToken,
            }));
            Assert.IsNull(dupResult["error"], $"vfx_node.duplicate (block) failed: {dupResult}");
            Assert.IsNotNull(dupResult["duplicated"], "response missing 'duplicated' key");

            string newBlockToken = (string)dupResult["duplicated"]["token"];
            string sourceToken   = (string)dupResult["duplicated"]["source"];
            Assert.IsNotNull(newBlockToken, "duplicated.token must be present");
            Assert.AreEqual(blockToken, sourceToken, "duplicated.source must match the input token");
            Assert.AreNotEqual(blockToken, newBlockToken, "duplicate must mint a new token distinct from source");

            // List nodes — must show TWO SetAttribute blocks under the same parent context
            var listResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            Assert.IsNull(listResult["error"], $"vfx_node.list failed: {listResult}");
            var nodes = (JArray)listResult["nodes"];
            Assert.IsNotNull(nodes, "list response must contain 'nodes' array");

            int blockCountUnderCtx = 0;
            bool foundOriginal = false, foundDuplicate = false;
            foreach (JObject n in nodes)
            {
                string cat = (string)n["category"];
                if (cat != "block") continue;
                string parent = (string)n["parent_token"];
                if (parent != ctxToken) continue;
                blockCountUnderCtx++;
                string tok = (string)n["token"];
                if (tok == blockToken) foundOriginal = true;
                if (tok == newBlockToken) foundDuplicate = true;
            }

            Assert.AreEqual(2, blockCountUnderCtx,
                $"expected exactly 2 blocks under context {ctxToken}, got {blockCountUnderCtx}");
            Assert.IsTrue(foundOriginal,  $"original block token {blockToken} missing from list");
            Assert.IsTrue(foundDuplicate, $"duplicated block token {newBlockToken} missing from list");
        }
    }
}
