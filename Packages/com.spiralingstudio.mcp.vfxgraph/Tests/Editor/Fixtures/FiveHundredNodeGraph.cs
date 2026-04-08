// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Fixtures/FiveHundredNodeGraph.cs
//
// Phase 5-3: Test fixture helper that creates a .vfx asset with ~500 operator
// nodes. Used by VfxPerformanceTests to exercise perf-budget assertions.
//
// Strategy: walk VFXLibrary.GetOperators() to collect all available types,
// then cycle through them until we reach 500 total nodes. Positioning is a
// simple grid so Unity doesn't need to do layout work.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generated;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tests.Fixtures
{
    /// <summary>
    /// Static helper that creates and tears down a 500-node VFX Graph asset.
    /// Call <see cref="Create"/> in a [OneTimeSetUp] and <see cref="Destroy"/> in
    /// [OneTimeTearDown]. Asset is placed under Assets/VfxPerfTestFixtures/.
    /// </summary>
    internal static class FiveHundredNodeGraph
    {
        public const string FixtureDir  = "Assets/VfxPerfTestFixtures";
        public const string FixturePath = FixtureDir + "/PerfGraph500.vfx";

        public const int TargetNodeCount = 500;

        /// <summary>
        /// Creates the fixture asset and populates it with ~500 operator nodes.
        /// Returns the asset path. Idempotent: if the asset already exists it is
        /// deleted and recreated so the test always starts from a known state.
        /// </summary>
        public static string Create()
        {
            // Ensure fixture folder.
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxPerfTestFixtures");

            // Remove stale asset.
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FixturePath) != null)
                AssetDatabase.DeleteAsset(FixturePath);

            // Create a new empty .vfx asset.
            VisualEffectAssetEditorUtility.CreateNew<VisualEffectAsset>(FixturePath);
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FixturePath);
            Assert.IsNotNull(asset, "Could not create perf fixture asset at " + FixturePath);

            // Load the VFXGraph via the soft-fork bridge (VfxMcpKernelHelpers is
            // in UnityEditor.VFX namespace — accessible via InternalsVisibleTo).
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            Assert.IsNotNull(graph, "LoadGraphFromAsset returned null for perf fixture.");

            // Collect available operator FQNs from the live library.
            var descs = VFXLibrary.GetOperators().ToList();
            Assert.IsTrue(descs.Count > 0,
                "VFXLibrary.GetOperators() returned 0 items — InternalsVisibleTo patch may be missing.");

            var types = descs.Select(d => d.modelType.FullName).Where(n => n != null).ToList();

            // Cycle through types until we have TargetNodeCount nodes.
            int col = 0, row = 0;
            for (int i = 0; i < TargetNodeCount; i++)
            {
                string typeFqn = types[i % types.Count];
                try
                {
                    var op = VfxNodeWrappers.CreateOperator(typeFqn);
                    if (op == null) continue;
                    op.position = new UnityEngine.Vector2(col * 220, row * 150);
                    graph.AddChild(op);
                }
                catch
                {
                    // Some operator types may fail construction with certain
                    // Unity versions; skip and continue cycling.
                }

                col++;
                if (col >= 25) { col = 0; row++; }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return FixturePath;
        }

        /// <summary>Deletes the fixture asset and folder (best-effort).</summary>
        public static void Destroy()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FixturePath) != null)
                AssetDatabase.DeleteAsset(FixturePath);

            if (AssetDatabase.IsValidFolder(FixtureDir))
            {
                var remaining = AssetDatabase.FindAssets(string.Empty, new[] { FixtureDir });
                if (remaining == null || remaining.Length == 0)
                    AssetDatabase.DeleteAsset(FixtureDir);
            }
        }
    }
}
