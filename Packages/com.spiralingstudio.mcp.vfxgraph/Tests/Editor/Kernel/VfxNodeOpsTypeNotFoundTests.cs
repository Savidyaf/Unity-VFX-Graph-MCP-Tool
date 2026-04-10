// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNodeOpsTypeNotFoundTests.cs
//
// W4-A regression: vfx_node.add must surface error.code="type_not_found"
// (not "vfx_exception") when the supplied type FQN is not present in the
// VFX catalog. The structured details payload must include the offending
// type_fqn and a category hint so callers can distinguish contract errors
// from genuinely unexpected internal failures.
//
// Baseline before this fix: VfxNodeWrappers.CreateOperator threw a plain
// System.ArgumentException that fell through VfxNodeTool's catch-all arm
// and became { "error": { "code": "vfx_exception", ... } }.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNodeOpsTypeNotFoundTests
    {
        private const string FixtureDir = "Assets/VfxNodeOpsTypeNotFoundFixtures";
        private const string GraphPath  = FixtureDir + "/TypeNotFoundProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxNodeOpsTypeNotFoundFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);

            var createResult = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new
            {
                action = "create",
                path   = GraphPath,
            }));
            Assert.IsNull(createResult["error"],
                $"fixture setup: vfx_asset.create failed: {createResult}");
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

        private static JObject AddWithType(string typeFqn)
        {
            return (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new
            {
                action = "add",
                graph  = GraphPath,
                type   = typeFqn,
                x      = 0f,
                y      = 0f,
            }));
        }

        private static void AssertTypeNotFound(JObject result, string expectedTypeFqn)
        {
            Assert.IsNotNull(result["error"],
                $"vfx_node.add should have returned an error envelope, got: {result}");

            var err = result["error"];
            Assert.AreEqual("type_not_found", (string)err["code"],
                $"Expected error.code=\"type_not_found\" but got {err["code"]}. Full envelope: {result}");

            var details = err["details"];
            Assert.IsNotNull(details,
                $"error.details must be present for type_not_found; full envelope: {result}");
            Assert.AreEqual(expectedTypeFqn, (string)details["type_fqn"],
                $"error.details.type_fqn mismatch; full envelope: {result}");
            Assert.IsNotNull(details["category"],
                $"error.details.category must be populated; full envelope: {result}");
        }

        [Test]
        public void Add_BogusFqn_ReturnsTypeNotFoundError()
        {
            const string bogus = "bogus.does.not.exist";
            var result = AddWithType(bogus);
            AssertTypeNotFound(result, bogus);
        }

        [Test]
        public void Add_InvalidOperatorFqn_ReturnsTypeNotFoundError()
        {
            // "UnityEditor.VFX.Operator.Position" is a plausible-looking
            // FQN-fragment but is not present in VfxCatalog.Operators.
            // Prior to W4-A this crashed into the vfx_exception catch-all.
            const string invalidOp = "UnityEditor.VFX.Operator.Position";
            var result = AddWithType(invalidOp);
            AssertTypeNotFound(result, invalidOp);
        }
    }
}
