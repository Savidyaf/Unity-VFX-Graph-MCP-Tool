// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxNodeListTests.cs

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxNodeListTests
    {
        private const string FixtureDir  = "Assets/VfxNodeListFixtures";
        private const string GraphPath   = FixtureDir + "/ListProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxNodeListFixtures");
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
        public void List_AfterAdds_ReturnsAllNodesWithStableTokens()
        {
            // Create a graph
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

            // Add a SetAttribute block under the context
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

            // Add an Add (operator)
            var opResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.Operator.Add",
                x = 100f, y = 50f,
            }));
            Assert.IsNull(opResult["error"], $"vfx_node.add (operator) failed: {opResult}");
            string opToken = (string)opResult["added"][0]["token"];
            Assert.IsNotNull(opToken);

            // List nodes
            var listResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            Assert.IsNull(listResult["error"], $"vfx_node.list failed: {listResult}");
            var nodes = (JArray)listResult["nodes"];
            Assert.IsNotNull(nodes, "list response must contain 'nodes' array");
            Assert.GreaterOrEqual(nodes.Count, 3, $"expected ≥3 nodes, got {nodes.Count}");

            // All three created tokens must appear
            bool foundCtx   = false, foundBlock = false, foundOp = false;
            string foundBlockParent = null;
            int foundBlockIndex = -2;
            foreach (JObject n in nodes)
            {
                string tok = (string)n["token"];
                if (tok == ctxToken)
                {
                    foundCtx = true;
                    Assert.AreEqual("context", (string)n["category"]);
                }
                if (tok == blockToken)
                {
                    foundBlock = true;
                    Assert.AreEqual("block", (string)n["category"]);
                    foundBlockParent = (string)n["parent_token"];
                    foundBlockIndex  = (int)n["block_index"];
                }
                if (tok == opToken)
                {
                    foundOp = true;
                    Assert.AreEqual("operator", (string)n["category"]);
                }
            }
            Assert.IsTrue(foundCtx,   $"context token {ctxToken} not in list");
            Assert.IsTrue(foundBlock, $"block token {blockToken} not in list");
            Assert.IsTrue(foundOp,    $"operator token {opToken} not in list");
            Assert.AreEqual(ctxToken, foundBlockParent, "block parent_token must match context token");
            Assert.GreaterOrEqual(foundBlockIndex, 0, "block_index must be ≥0");

            // List again — tokens must be identical (deterministic recovery)
            var listResult2 = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            var nodes2 = (JArray)listResult2["nodes"];
            Assert.AreEqual(nodes.Count, nodes2.Count);
            // Build token sets and compare
            var s1 = new System.Collections.Generic.HashSet<string>();
            var s2 = new System.Collections.Generic.HashSet<string>();
            foreach (JObject n in nodes)  s1.Add((string)n["token"]);
            foreach (JObject n in nodes2) s2.Add((string)n["token"]);
            Assert.IsTrue(s1.SetEquals(s2), "tokens must be stable across consecutive list calls");
        }

        [Test]
        public void GraphGetInfo_ExposesGraphMetadata()
        {
            var create = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = GraphPath,
            }));
            Assert.IsNull(create["error"]);

            var info = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "get_info", graph = GraphPath,
            }));
            Assert.IsNull(info["error"], $"get_info failed: {info}");

            // F3 expansion: these keys must exist
            Assert.IsNotNull(info["graph_path"],          "graph_path key missing");
            Assert.IsNotNull(info["child_count"],         "child_count key missing");
            Assert.IsNotNull(info["space"],               "space key missing");
            Assert.IsNotNull(info["system_count"],        "system_count key missing");
            Assert.IsNotNull(info["bounds_setting_mode"], "bounds_setting_mode key missing");
            Assert.IsNotNull(info["update_mode"],         "update_mode key missing");
            // system_names is an array, may be empty for a fresh graph
            Assert.IsNotNull(info["system_names"]);
            Assert.IsTrue(info["system_names"] is JArray);
        }
    }
}
