// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNodeOpsCoercerTests.cs
//
// Phase v0.3.1 F7 — regression guard for VfxCoercerDispatch.
//
// This test exercises the SetSetting path through the typed coercer dispatch
// helper (VfxCoercerDispatch.Coerce) instead of the old Convert.ChangeType
// fallback. The 'capacity' setting on VFXBasicInitialize is a uint field —
// CoerceToUInt in VfxCoercers.g.cs is the coercer that actually runs here.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNodeOpsCoercerTests
    {
        private const string FixtureDir = "Assets/VfxCoercerFixtures";
        private const string GraphPath  = FixtureDir + "/CoercerProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxCoercerFixtures");
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
        public void SetSetting_UintCapacity_RoundTripsThroughCoercer()
        {
            // Create graph and add an Initialize context — its 'capacity' field is a uint setting,
            // which is a known gap for Convert.ChangeType when called with non-uint inputs.
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));
            var addResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.VFXBasicInitialize",
                x = 0f, y = 0f,
            }));
            string ctxToken = (string)addResult["added"][0]["token"];
            Assert.IsNotNull(ctxToken);

            var setResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "set_setting",
                graph  = GraphPath,
                token  = ctxToken,
                name   = "capacity",
                value  = 4096,
            }));
            Assert.IsNull(setResult["error"], $"set_setting (capacity) failed: {setResult}");

            var getResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "get_setting",
                graph  = GraphPath,
                token  = ctxToken,
                name   = "capacity",
            }));
            Assert.IsNull(getResult["error"]);
            Assert.AreEqual(4096, (int)getResult["value"]);
        }
    }
}
