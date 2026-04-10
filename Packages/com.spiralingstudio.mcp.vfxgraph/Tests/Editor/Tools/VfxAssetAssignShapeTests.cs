// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxAssetAssignShapeTests.cs
//
// W2-D Bug 1 — vfx_asset.assign must route through ShapeMutation so verbose
// responses include `health` (and warnings/diffs are foldable). Prior to the
// fix, Assign returned the raw inner payload on success and bypassed the
// canonical mutation envelope.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxAssetAssignShapeTests
    {
        private const string FixtureDir = "Assets/VfxAssignShapeFixtures";
        private const string GraphPath  = FixtureDir + "/AssignProbe.vfx";
        private const string GoName     = "VfxAssignShapeProbeGO";

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxAssignShapeFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);

            // Clean any leftover instance from a previous failed run.
            var existing = GameObject.Find(GoName);
            if (existing != null) Object.DestroyImmediate(existing);

            _go = new GameObject(GoName);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            var stray = GameObject.Find(GoName);
            if (stray != null) Object.DestroyImmediate(stray);

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
        public void Assign_Verbose_GoesThroughShapeMutation_IncludesHealth()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action = "create", path = GraphPath,
            }));

            var response = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new {
                action     = "assign",
                path       = GraphPath,
                gameObject = GoName,
                verbose    = true,
            }));

            Assert.IsNull(response["error"], $"assign returned error: {response}");
            Assert.IsNotNull(response["assigned"], "assign response must still fold the inner 'assigned' key");
            Assert.IsNotNull(response["health"],
                "verbose vfx_asset.assign must include 'health' — proves it routed through ShapeMutation rather than returning the raw payload");
        }
    }
}
