// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxBlockSetAttributeTests.cs
//
// W2-B regression — vfx_block.set_attribute must drive the SetAttribute block's
// `attribute` [VFXSetting] field (not look up the attribute name as if it were
// an input slot). The previous dispatch called NodeOps.SetProperty with the
// attribute name verbatim, so callers passing attribute_name="lifetime" hit
// slot_not_found because there is no slot literally called "lifetime" — the
// value slot for the lifetime attribute is "_Lifetime" and only exists once
// the `attribute` setting has been set.
//
// This test creates a SetAttribute block, calls vfx_block.set_attribute with
// attribute_name="lifetime", and verifies the response is success and the
// `attribute` setting was actually persisted on the block. Lifetime is chosen
// because its slot type is float, avoiding spaceable Vector coercion noise
// that is unrelated to the W2-B fix.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxBlockSetAttributeTests
    {
        private const string FixtureDir = "Assets/VfxBlockSetAttributeFixtures";
        private const string GraphPath  = FixtureDir + "/SetAttrProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxBlockSetAttributeFixtures");
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
        public void SetAttribute_LifetimeOnSetAttributeBlock_PersistsSettingAndReturnsSuccess()
        {
            // Fixture: graph + Initialize context + SetAttribute block.
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

            // Act: set the lifetime attribute via vfx_block.set_attribute.
            // Lifetime is a float — no spaceable Vector coercion involved, which
            // keeps the test focused on the W2-B dispatch fix.
            var setResult = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action         = "set_attribute",
                graph          = GraphPath,
                token          = blockToken,
                attribute_name = "lifetime",
                value          = 4.5f,
            }));

            // Assert (1): response is a success envelope with the set_attribute payload.
            Assert.IsNotNull(setResult, "vfx_block.set_attribute must return a response");
            Assert.IsNull(setResult["error"],
                $"vfx_block.set_attribute returned an error envelope: {setResult}");
            Assert.IsNotNull(setResult["set_attribute"],
                $"response missing 'set_attribute' key: {setResult}");
            Assert.AreEqual("lifetime", (string)setResult["set_attribute"]["attribute_name"]);
            Assert.AreEqual("_Lifetime", (string)setResult["set_attribute"]["slot_name"],
                "slot_name should reflect GenerateLocalAttributeName algorithm");

            // Assert (2): the block's `attribute` setting was actually persisted.
            var getResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "get_setting",
                graph  = GraphPath,
                token  = blockToken,
                name   = "attribute",
            }));
            Assert.IsNull(getResult["error"], $"vfx_node.get_setting failed: {getResult}");
            Assert.AreEqual("lifetime", (string)getResult["value"],
                $"block's 'attribute' setting did not persist; get_setting returned: {getResult}");
        }

        [Test]
        public void ListAttributes_ReturnsCanonicalNotImplementedEnvelope()
        {
            var result = (JObject)VfxBlockTool.HandleCommand(JObject.FromObject(new {
                action = "list_attributes",
            }));

            Assert.IsNotNull(result, "vfx_block.list_attributes must return a response");
            Assert.IsNull(result["error"], $"unexpected error: {result}");
            Assert.AreEqual("not_implemented", (string)result["state"],
                $"expected canonical state='not_implemented' stub, got: {result}");
            Assert.IsNotNull(result["hint"], $"stub envelope missing 'hint': {result}");
            Assert.IsNull(result["note"],
                $"legacy non-canonical 'note' field still present: {result}");
        }
    }
}
