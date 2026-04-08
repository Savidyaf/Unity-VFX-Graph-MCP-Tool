// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxNodeMoveTests.cs

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxNodeMoveTests
    {
        private const string FixtureDir = "Assets/VfxMoveFixtures";
        private const string GraphPath  = FixtureDir + "/MoveProbe.vfx";

        [SetUp] public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxMoveFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [TearDown] public void TearDown()
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
        public void Move_UpdatesPositionAndListReflectsIt()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));

            var addResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.Operator.Add",
                x = 0f, y = 0f,
            }));
            string token = (string)addResult["added"][0]["token"];
            Assert.IsNotNull(token);

            var moveResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "move",
                graph  = GraphPath,
                token  = token,
                x = 100f, y = 200f,
            }));
            Assert.IsNull(moveResult["error"], $"move failed: {moveResult}");
            Assert.IsNotNull(moveResult["moved"], "move response must contain 'moved' key");

            // Verify the new position via list
            var listResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "list", graph = GraphPath,
            }));
            var nodes = (JArray)listResult["nodes"];
            JObject moved = null;
            foreach (JObject n in nodes)
                if ((string)n["token"] == token) { moved = n; break; }

            Assert.IsNotNull(moved, $"token {token} missing from list after move");
            Assert.AreEqual(100f, (float)moved["x"], 0.01f);
            Assert.AreEqual(200f, (float)moved["y"], 0.01f);
        }
    }
}
