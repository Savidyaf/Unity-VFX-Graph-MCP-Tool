// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxPropertySetValueTests.cs
//
// W3-A regression: vfx_property.set_value and vfx_node.get_property both
// returned slot_not_found for name="value" on a VFXParameter because the
// parameter's canonical output slot is named "o" (not "value"). This test
// asserts that a System.Single parameter round-trips a scalar value through
// set_value / get_property cleanly.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxPropertySetValueTests
    {
        private const string FixtureDir = "Assets/VfxPropertySetValueFixtures";
        private const string GraphPath  = FixtureDir + "/PropProbe.vfx";

        [SetUp] public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxPropertySetValueFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [TearDown] public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
            if (AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.DeleteAsset(FixtureDir);
        }

        [Test]
        public void SetValue_SingleParameter_RoundTripsThroughGetProperty()
        {
            // Create the fixture graph.
            var createResult = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create",
                path   = GraphPath,
            }));
            Assert.IsNull(createResult["error"], $"vfx_asset.create failed: {createResult}");

            // Add a System.Single parameter.
            var addResult = (JObject)VfxPropertyTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "System.Single",
                x      = 0f, y = 0f,
            }));
            Assert.IsNull(addResult["error"], $"vfx_property.add failed: {addResult}");
            Assert.IsNotNull(addResult["added"], $"vfx_property.add missing 'added': {addResult}");
            string token = (string)addResult["added"][0]["token"];
            Assert.IsNotNull(token, "vfx_property.add must return a token");

            // set_value with a scalar JSON number — the W3-A bug made this throw slot_not_found.
            var setResult = (JObject)VfxPropertyTool.HandleCommand(JObject.FromObject(new {
                action = "set_value",
                graph  = GraphPath,
                token  = token,
                value  = 2.5f,
            }));
            Assert.IsNull(setResult["error"], $"vfx_property.set_value failed: {setResult}");
            Assert.IsNotNull(setResult["set_value"], $"response missing 'set_value' key: {setResult}");

            // Read the value back via vfx_node.get_property name="value".
            var getResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "get_property",
                graph  = GraphPath,
                token  = token,
                name   = "value",
            }));
            Assert.IsNull(getResult["error"], $"vfx_node.get_property failed: {getResult}");
            Assert.IsNotNull(getResult["value"], $"get_property response missing 'value': {getResult}");

            float persisted = (float)getResult["value"];
            Assert.AreEqual(2.5f, persisted, 0.0001f, "persisted value must round-trip to 2.5");
        }
    }
}
