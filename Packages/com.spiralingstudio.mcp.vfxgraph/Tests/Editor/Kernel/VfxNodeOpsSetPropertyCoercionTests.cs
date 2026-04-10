// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNodeOpsSetPropertyCoercionTests.cs
//
// W5-A regression guard — VfxNodeOps.SetProperty must route JObject/JArray
// payloads through VfxCoercerDispatch before writing to VFXSlot.value. Prior
// to the W5-A fix, forwarding the raw Newtonsoft.Json object straight to
// VFXSerializedObject.Set threw "Cannot assign an object of type
// Newtonsoft.Json.Linq.JObject to VFXSerializedObject of type
// UnityEngine.Vector3".
//
// Uses UnityEditor.VFX.Operator.CrossProduct because it exposes two fixed
// Vector3 input slots ("a" and "b") by default — no variadic/spaceable
// rewriting that could mask the coercer path.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNodeOpsSetPropertyCoercionTests
    {
        private const string FixtureDir = "Assets/VfxSetPropertyCoercionFixtures";
        private const string GraphPath  = FixtureDir + "/CoercionProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxSetPropertyCoercionFixtures");
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

        private static string AddCrossProduct()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = GraphPath,
            }));

            var addResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.Operator.CrossProduct",
                x = 0f, y = 0f,
            }));
            Assert.IsNull(addResult["error"],
                $"vfx_node.add (CrossProduct) failed: {addResult}");
            return (string)addResult["added"][0]["token"];
        }

        [Test]
        public void SetProperty_Vector3FromJObject_PersistsAsVector3()
        {
            string token = AddCrossProduct();

            // CrossProduct input slot "a" defaults to Vector3.right. Before the
            // W5-A fix, passing a JObject here threw an InvalidCastException in
            // VFXSerializedObject. After the fix, VfxCoercerDispatch converts the
            // JObject to a Vector3 before the slot assignment.
            var payload = new JObject { ["x"] = 1f, ["y"] = 2f, ["z"] = 3f };
            VfxKernelContainer.NodeOps.SetProperty(GraphPath, token, "a", payload);

            object roundTripped =
                VfxKernelContainer.NodeOps.GetProperty(GraphPath, token, "a");
            Assert.IsInstanceOf<Vector3>(roundTripped,
                $"SetProperty should persist a strongly-typed Vector3, got {roundTripped?.GetType().Name ?? "null"}");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), (Vector3)roundTripped);
        }

        [Test]
        public void SetProperty_Vector3FromJArray_PersistsAsVector3()
        {
            string token = AddCrossProduct();

            // Same regression, JArray shape: [x, y, z] must coerce to Vector3.
            var payload = new JArray(4f, 5f, 6f);
            VfxKernelContainer.NodeOps.SetProperty(GraphPath, token, "b", payload);

            object roundTripped =
                VfxKernelContainer.NodeOps.GetProperty(GraphPath, token, "b");
            Assert.IsInstanceOf<Vector3>(roundTripped,
                $"SetProperty should persist a strongly-typed Vector3, got {roundTripped?.GetType().Name ?? "null"}");
            Assert.AreEqual(new Vector3(4f, 5f, 6f), (Vector3)roundTripped);
        }
    }
}
