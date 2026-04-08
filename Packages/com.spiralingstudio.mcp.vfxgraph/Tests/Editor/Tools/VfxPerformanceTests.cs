// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxPerformanceTests.cs
//
// Phase 5-3: Performance budget tests.
//
// Uses the FiveHundredNodeGraph fixture (built once per run) to exercise the
// spec's performance budget table. Tests run 5-10 iterations, take the median,
// and assert below ~2x the spec p95 target to account for cold caches and CI
// variance.
//
// Filter with [Category("Performance")] to skip in fast-test runs.
//
// Performance budgets (spec table, p95 targets → test threshold):
//   Single-call read (get_info, 500 nodes)  < 150 ms → test: 300 ms
//   Single-call read (scoped filter)        <  50 ms → test: 100 ms
//   Identity token resolve                  <   5 ms → test:  15 ms
//   Identity recovery walk                  <  20 ms → test:  60 ms
//   YAML structural diff (500 nodes)        < 100 ms → test: 250 ms
//   Console correlator snapshot             <  20 ms → test:  60 ms
//   Token-savings shaping (per-call)        <  10 ms → test:  30 ms total for 100 calls

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using SpiralingStudio.VfxMcp.Tests.Fixtures;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    [Category("Performance")]
    public class VfxPerformanceTests
    {
        // ── fixture lifecycle ─────────────────────────────────────────────────

        private string _fixturePath;

        [OneTimeSetUp]
        public void BuildFixture()
        {
            _fixturePath = FiveHundredNodeGraph.Create();
            Assert.IsNotNull(_fixturePath);
        }

        [OneTimeTearDown]
        public void DestroyFixture()
        {
            FiveHundredNodeGraph.Destroy();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="action"/> <paramref name="iterations"/> times,
        /// returns the median elapsed ms.
        /// </summary>
        private static double MedianMs(Action action, int iterations = 7)
        {
            var samples = new List<double>(iterations);
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            samples.Sort();
            return samples[samples.Count / 2];
        }

        private VFXGraph LoadGraph()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(_fixturePath);
            Assert.IsNotNull(asset, "Perf fixture asset not found at " + _fixturePath);
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            Assert.IsNotNull(graph, "VFXGraph could not be loaded from perf fixture.");
            return graph;
        }

        private static IEnumerable<VFXModel> WalkAllModels(VFXModel root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.children)
                foreach (var desc in WalkAllModels(child))
                    yield return desc;
        }

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        [Category("Performance")]
        public void Perf_SingleCallRead_GetGraphInfo_500Nodes_UnderBudget()
        {
            // Walk all graph children and collect type names (simulating get_info).
            // Spec p95 < 150 ms; test threshold: 300 ms.
            const double BudgetMs = 300.0;

            var graph = LoadGraph();

            double medianMs = MedianMs(() =>
            {
                var names = new List<string>();
                foreach (var child in graph.children)
                    names.Add(child.GetType().FullName);
                Assert.Greater(names.Count, 0);
            }, iterations: 7);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"Single-call read (get_info, 500 nodes) {medianMs:F1}ms > {BudgetMs}ms budget.");
        }

        [Test]
        [Category("Performance")]
        public void Perf_SingleCallRead_ScopedFilter_UnderBudget()
        {
            // Filter graph children by a type substring (scoped filter scenario).
            // Spec p95 < 50 ms; test threshold: 100 ms.
            const double BudgetMs = 100.0;

            var graph = LoadGraph();
            const string filterNs = "UnityEditor.VFX.Operator";

            double medianMs = MedianMs(() =>
            {
                var matches = new List<VFXModel>();
                foreach (var child in graph.children)
                    if (child.GetType().FullName?.StartsWith(filterNs) == true)
                        matches.Add(child);
                Assert.IsNotNull(matches);
            }, iterations: 10);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"Single-call read (scoped filter) {medianMs:F1}ms > {BudgetMs}ms budget.");
        }

        [Test]
        [Category("Performance")]
        public void Perf_IdentityTokenResolve_UnderBudget()
        {
            // Mint a token for the first node, then resolve it repeatedly.
            // Spec p95 < 5 ms; test threshold: 15 ms.
            const double BudgetMs = 15.0;

            var graph = LoadGraph();
            string guid = AssetDatabase.AssetPathToGUID(_fixturePath);

            VFXModel firstNode = null;
            foreach (var child in graph.children) { firstNode = child; break; }
            Assume.That(firstNode, Is.Not.Null, "Fixture has no children.");

            // Use a temp sidecar path so we don't pollute any real sidecar.
            string tempSidecar = System.IO.Path.GetTempFileName();
            var identity = new VfxIdentity(new VfxIdentitySidecar(tempSidecar));
            string token = identity.Mint(guid, firstNode);

            try
            {
                double medianMs = MedianMs(() =>
                {
                    var resolved = identity.Resolve(guid, token);
                    Assert.IsNotNull(resolved);
                }, iterations: 10);

                Assert.LessOrEqual(medianMs, BudgetMs,
                    $"Identity token resolve {medianMs:F1}ms > {BudgetMs}ms budget.");
            }
            finally
            {
                if (File.Exists(tempSidecar)) File.Delete(tempSidecar);
            }
        }

        [Test]
        [Category("Performance")]
        public void Perf_IdentityRecoveryWalk_UnderBudget()
        {
            // Walk all models (simulates the recovery-path candidate search).
            // Spec p95 < 20 ms; test threshold: 60 ms.
            const double BudgetMs = 60.0;

            var graph = LoadGraph();

            double medianMs = MedianMs(() =>
            {
                int count = 0;
                foreach (var _ in WalkAllModels(graph)) count++;
                Assert.Greater(count, 0);
            }, iterations: 7);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"Identity recovery walk {medianMs:F1}ms > {BudgetMs}ms budget.");
        }

        [Test]
        [Category("Performance")]
        public void Perf_YamlStructuralDiff_500Nodes_UnderBudget()
        {
            // Run ScanMonoBehaviours on the on-disk YAML of the fixture.
            // Spec p95 < 100 ms; test threshold: 250 ms.
            const double BudgetMs = 250.0;

            AssetDatabase.SaveAssets();

            string yaml = string.Empty;
            if (File.Exists(_fixturePath))
                yaml = File.ReadAllText(_fixturePath);

            Assume.That(yaml.Length, Is.GreaterThan(0),
                "Fixture YAML is empty — flush/save may have failed.");

            double medianMs = MedianMs(() =>
            {
                var blocks = VfxYamlVerifier.ScanMonoBehaviours(yaml);
                Assert.IsNotNull(blocks);
            }, iterations: 7);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"YAML structural diff {medianMs:F1}ms > {BudgetMs}ms budget.");
        }

        [Test]
        [Category("Performance")]
        public void Perf_ConsoleCorrelatorSnapshot_UnderBudget()
        {
            // Calling SnapshotBefore() + CorrelateAfter() with an empty intent.
            // This exercises the console correlator's overhead path.
            // Spec p95 < 20 ms; test threshold: 60 ms.
            const double BudgetMs = 60.0;

            var correlator = new VfxConsoleCorrelator();
            var emptyIntent = new VfxIntentSnapshot { Ops = new List<VfxIntentOp>() };

            double medianMs = MedianMs(() =>
            {
                object snapshot = correlator.SnapshotBefore();
                var correlation = correlator.CorrelateAfter(snapshot, emptyIntent);
                Assert.IsNotNull(correlation);
            }, iterations: 10);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"Console correlator snapshot+correlate {medianMs:F1}ms > {BudgetMs}ms budget.");
        }

        [Test]
        [Category("Performance")]
        public void Perf_TokenSavingsShaping_UnderBudget()
        {
            // Shape error envelopes 100 times. Total must stay under 30 ms
            // (i.e., well below 10ms per-call spec target scaled).
            // Spec p95 per-call < 10 ms; 100-call test threshold: 30 ms total.
            const double BudgetMs = 30.0;

            var shaper = new VfxResponseShaper();
            var envelope = new VfxErrorEnvelope
            {
                Code = "name_collision",
                Message = "'{name}' is ambiguous.",
                // No Hint — exercises the VfxOverrides.GetHint() fallback path.
            };

            double medianMs = MedianMs(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var result = shaper.ShapeError(envelope);
                    Assert.IsNotNull(result);
                }
            }, iterations: 5);

            Assert.LessOrEqual(medianMs, BudgetMs,
                $"Token-savings shaping (100 calls) {medianMs:F1}ms > {BudgetMs}ms budget.");
        }
    }
}
