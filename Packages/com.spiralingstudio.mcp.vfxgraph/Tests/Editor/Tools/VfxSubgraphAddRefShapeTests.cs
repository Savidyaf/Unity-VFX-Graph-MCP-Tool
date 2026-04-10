// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxSubgraphAddRefShapeTests.cs
//
// W2-D Bug 2 + Bug 3 — vfx_subgraph envelope shape parity.
//
// Bug 2: AddRefInner previously returned `added` as a JObject, while every
//        other Add*Inner returns `added` as a single-element JArray. The fix
//        wraps the added entry in a JArray so batch consumers / diff folders
//        see a consistent type across all "add" mutations.
//
// Bug 3: GetExposed previously returned a non-canonical `{exposed:[], note:...}`
//        envelope. The fix promotes it to the canonical not_implemented shape
//        used by other deferred read tools (state + hint + empty exposed array
//        for shape compatibility).

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxSubgraphAddRefShapeTests
    {
        private const string FixtureDir       = "Assets/VfxSubgraphShapeFixtures";
        private const string MainGraphPath    = FixtureDir + "/SubgraphShapeProbe.vfx";
        private const string SubgraphAssetPath = FixtureDir + "/SubgraphShapeMath.vfxoperator";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxSubgraphShapeFixtures");
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
        public void AddRef_Returns_AddedAsJArray_NotJObject()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = MainGraphPath,
            }));
            VfxSubgraphTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = SubgraphAssetPath,
            }));

            var response = (JObject)VfxSubgraphTool.HandleCommand(JObject.FromObject(new {
                action   = "add_ref",
                graph    = MainGraphPath,
                subgraph = SubgraphAssetPath,
                x = 0f, y = 0f,
            }));

            Assert.IsNull(response["error"], $"add_ref returned error: {response}");
            var added = response["added"];
            Assert.IsNotNull(added, "add_ref response must include 'added' key");
            Assert.AreEqual(JTokenType.Array, added.Type,
                "add_ref 'added' value must be a JArray to match every other Add*Inner shape");
            Assert.IsInstanceOf<JArray>(added);
            Assert.GreaterOrEqual(((JArray)added).Count, 1,
                "add_ref 'added' array must contain at least one entry");
            Assert.IsNotNull(added[0]?["token"], "first entry must contain 'token'");
            Assert.IsNotNull(added[0]?["subgraph"], "first entry must contain 'subgraph'");
        }

        [Test]
        public void GetExposed_Returns_CanonicalNotImplementedEnvelope()
        {
            var response = (JObject)VfxSubgraphTool.HandleCommand(JObject.FromObject(new {
                action = "get_exposed",
                graph  = MainGraphPath,
            }));

            Assert.IsNull(response["error"], $"get_exposed returned error: {response}");
            Assert.AreEqual("not_implemented", (string)response["state"],
                "get_exposed must use canonical state=\"not_implemented\" envelope");
            string hint = (string)response["hint"];
            Assert.IsFalse(string.IsNullOrEmpty(hint),
                "get_exposed must include a non-empty hint pointing operators at the recommended workflow");
            Assert.IsNotNull(response["exposed"], "get_exposed must keep 'exposed' key for shape compatibility");
            Assert.AreEqual(JTokenType.Array, response["exposed"].Type,
                "'exposed' must remain an array");
        }
    }
}
