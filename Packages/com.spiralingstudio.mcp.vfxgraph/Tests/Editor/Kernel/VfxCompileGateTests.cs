// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxCompileGateTests.cs
//
// Phase 3 Lane C, task 3C-2.
//
// Verifies that VfxCompileGate.Compile drives VFXGraph.CompileAndUpdateAsset
// (with the asset argument required by erratum P-B1) and returns Ok=true on
// a freshly minted empty .vfx fixture.

using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxCompileGateTests
    {
        // Use a sandbox folder so we never collide with other lanes' fixtures.
        private const string FixtureDir  = "Assets/VfxKernelTestFixtures";
        private const string FixturePath = FixtureDir + "/VfxCompileGate_Empty.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxKernelTestFixtures");

            // CreateNew<T> writes a minimal YAML and imports the asset. See
            // Packages/com.unity.visualeffectgraph/Editor/VFXAssetEditorUtility.cs:89-107.
            VisualEffectAssetEditorUtility.CreateNew<VisualEffectAsset>(FixturePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FixturePath) != null)
                AssetDatabase.DeleteAsset(FixturePath);
            // Best-effort folder cleanup; don't fail if other tests left siblings.
            if (AssetDatabase.IsValidFolder(FixtureDir))
            {
                var children = AssetDatabase.FindAssets(string.Empty, new[] { FixtureDir });
                if (children == null || children.Length == 0)
                    AssetDatabase.DeleteAsset(FixtureDir);
            }
        }

        [Test]
        public void Compile_OnEmptyTestAsset_ReturnsOk()
        {
            var gate = new VfxCompileGate();
            var result = gate.Compile(FixturePath);

            Assert.IsNotNull(result, "VfxCompileResult must not be null.");
            Assert.IsTrue(result.Ok,
                $"Empty fixture should compile cleanly. Errors: {DescribeErrors(result)}");
            Assert.IsTrue(result.DurationMs >= 0, "DurationMs must be non-negative.");
        }

        [Test]
        public void Compile_OnMissingAsset_ReturnsErrorEnvelope()
        {
            var gate = new VfxCompileGate();
            var result = gate.Compile("Assets/VfxKernelTestFixtures/__does_not_exist__.vfx");

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Ok, "Missing asset must produce Ok=false.");
            Assert.GreaterOrEqual(result.Errors.Count, 1);
            Assert.AreEqual("asset_not_found", result.Errors[0].ErrorId);
        }

        // TODO(phase 5): A hand-crafted bad-HLSL CustomHLSL fixture would
        // exercise the populated reporter path. Skipped here per task scope.

        private static string DescribeErrors(VfxCompileResult r)
        {
            if (r.Errors == null || r.Errors.Count == 0) return "<none>";
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < r.Errors.Count; i++)
            {
                if (i > 0) parts.Append("; ");
                parts.Append('[').Append(r.Errors[i].Severity).Append("] ")
                     .Append(r.Errors[i].ErrorId).Append(" — ")
                     .Append(r.Errors[i].Description);
            }
            return parts.ToString();
        }
    }
}
