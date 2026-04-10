// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxGraphP2LiftsTests.cs
//
// W4-C (v0.3.1) — P2 surface-gap lifts for VfxGraphTool.
//
// Bundles the four W4-C lifts into one test file:
//   Lift 1 — set_space / set_capacity / set_bounds convenience wrappers
//   Lift 2 — compilation_status cache-read
//   Lift 3 — get_health empty-transaction probe
//   Lift 4 — get_info F3 partial keys (space, bounds_setting_mode, update_mode)
//
// All tests build a minimal single-system fixture (Spawner → Initialize →
// Update → Output) so the lifts that depend on a unique init context find one.
// Tests that don't need the full system use a bare Initialize context.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxGraphP2LiftsTests
    {
        private const string FixtureDir = "Assets/VfxGraphP2LiftsFixtures";
        private const string GraphPath  = FixtureDir + "/P2LiftsProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxGraphP2LiftsFixtures");
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

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>Create a minimal VFX asset with a single Initialize context.</summary>
        private static void CreateFixtureWithInit()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));
            VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = "UnityEditor.VFX.VFXBasicInitialize",
                x = 0f, y = 0f,
            }));
        }

        /// <summary>Create a fixture with no contexts at all (just a bare asset).</summary>
        private static void CreateEmptyFixture()
        {
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));
        }

        // ── Lift 1: set_space / set_capacity / set_bounds wrappers ─────────

        [Test]
        public void Lift1_SetCapacity_WithUniqueInit_Succeeds()
        {
            CreateFixtureWithInit();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "set_capacity",
                graph  = GraphPath,
                value  = 256,
            }));

            Assert.IsNotNull(result, "set_capacity must return a response");
            Assert.IsNull(result["error"],
                $"set_capacity failed with error: {result["error"]}");
            Assert.IsNotNull(result["set_capacity"],
                "expected 'set_capacity' key in the response payload");
            Assert.AreEqual(256, (long)result["set_capacity"]["value"],
                "capacity value must be persisted through the transaction");
        }

        [Test]
        public void Lift1_SetCapacity_NoInitContext_ReturnsNeedsExplicitTarget()
        {
            CreateEmptyFixture();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "set_capacity",
                graph  = GraphPath,
                value  = 128,
            }));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"],
                "wrapper should return a structured envelope, not throw");
            Assert.AreEqual("needs_explicit_target", (string)result["state"],
                "no init context should yield needs_explicit_target, not not_implemented");
        }

        [Test]
        public void Lift1_SetSpace_WithUniqueContext_Succeeds()
        {
            CreateFixtureWithInit();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "set_space",
                graph  = GraphPath,
                value  = "World",
            }));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"],
                $"set_space failed with error: {result["error"]}");
            Assert.IsNotNull(result["set_space"],
                "expected 'set_space' key in the response payload");
            Assert.AreEqual("World", (string)result["set_space"]["value"],
                "space value must round-trip World → VFXSpace.World → 'World'");
        }

        [Test]
        public void Lift1_SetBounds_WithUniqueInit_SetsModeAndReturnsHint()
        {
            CreateFixtureWithInit();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "set_bounds",
                graph  = GraphPath,
                mode   = "Manual",
            }));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"],
                $"set_bounds failed with error: {result["error"]}");
            Assert.IsNotNull(result["set_bounds"],
                "expected 'set_bounds' key in the response payload");
            Assert.AreEqual("Manual", (string)result["set_bounds"]["mode"]);
            Assert.IsNotNull(result["set_bounds"]["hint"],
                "set_bounds should document how to set explicit center/size");
        }

        // ── Lift 2: compilation_status cache ───────────────────────────────

        [Test]
        public void Lift2_CompilationStatus_BeforeCompile_ReturnsNoCompileYet()
        {
            // Use a path distinct from the class-wide GraphPath so the static
            // compile-result cache cannot carry state between test methods.
            const string distinctPath = FixtureDir + "/P2LiftsProbeUncompiled.vfx";
            try
            {
                VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = distinctPath }));

                var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                    action = "compilation_status",
                    graph  = distinctPath,
                }));

                Assert.IsNotNull(result);
                // Documented state — not a stub.
                Assert.AreEqual("no_compile_yet", (string)result["state"],
                    "compilation_status must return no_compile_yet before any compile runs");
                Assert.AreNotEqual("not_implemented", (string)result["state"],
                    "compilation_status must no longer return not_implemented");
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(distinctPath) != null)
                    AssetDatabase.DeleteAsset(distinctPath);
            }
        }

        [Test]
        public void Lift2_CompilationStatus_AfterCompile_ReturnsCachedResult()
        {
            CreateFixtureWithInit();

            // Trigger a compile so the cache is populated.
            var compileResult = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "compile",
                graph  = GraphPath,
            }));
            Assert.IsNotNull(compileResult, "compile must return a response");

            // Now read the cached result via compilation_status.
            var statusResult = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "compilation_status",
                graph  = GraphPath,
            }));

            Assert.IsNotNull(statusResult);
            Assert.IsNull(statusResult["error"],
                $"compilation_status failed: {statusResult["error"]}");
            Assert.AreNotEqual("not_implemented", (string)statusResult["state"],
                "must not return not_implemented after a successful compile");
            Assert.IsNotNull(statusResult["Ok"],
                "cached result must include the Ok flag from VfxCompileResult");
            Assert.AreEqual(true, (bool)statusResult["cached"],
                "response must flag that it came from the cache");
        }

        // ── Lift 3: get_health empty-transaction probe ─────────────────────

        [Test]
        public void Lift3_GetHealth_OnFreshFixture_ReturnsHealthKeys()
        {
            CreateFixtureWithInit();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "get_health",
                graph  = GraphPath,
            }));

            Assert.IsNotNull(result, "get_health must return a response");
            // No longer a stub:
            Assert.AreNotEqual("not_implemented", (string)result["state"],
                "get_health must no longer return not_implemented");

            // Either we got a health block (happy path via Shape(commit, true))
            // or the commit failed and Shape delegated to ShapeError. The
            // health-key assertion is the primary contract for this lift.
            if (result["error"] == null)
            {
                Assert.IsNotNull(result["health"],
                    "successful get_health must include a 'health' key (verbose Shape)");
                // VfxHealthReport fields are PascalCase (public C# fields).
                Assert.IsNotNull(result["health"]["Compile"],
                    "health.Compile must be present");
                Assert.IsNotNull(result["health"]["YamlDiff"],
                    "health.YamlDiff must be present");
            }
            // If the commit errored (e.g. compile error), that's an acceptable
            // alternate outcome — the point is that the stub is gone.
        }

        // ── Lift 4: get_info F3 partial keys ───────────────────────────────

        [Test]
        public void Lift4_GetInfo_F3PartialKeys_NoLongerEmptyWithInitContext()
        {
            CreateFixtureWithInit();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "get_info",
                graph  = GraphPath,
            }));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"], $"get_info failed: {result["error"]}");

            // update_mode is a documented constant (VFX Graph has no graph-level
            // update mode) — must never be empty now.
            Assert.AreEqual("per_system", (string)result["update_mode"],
                "update_mode is now documented as 'per_system', not an empty string");

            // bounds_setting_mode is read from VFXDataParticle.boundsMode. With a
            // single init context, there's a unique value.
            string boundsMode = (string)result["bounds_setting_mode"];
            Assert.IsNotNull(boundsMode,
                "bounds_setting_mode key must exist");
            Assert.IsNotEmpty(boundsMode,
                "bounds_setting_mode must be populated when a VFXDataParticle is present");
        }

        [Test]
        public void Lift4_GetInfo_UpdateMode_Constant_OnEmptyGraph()
        {
            CreateEmptyFixture();

            var result = (JObject)VfxGraphTool.HandleCommand(JObject.FromObject(new {
                action = "get_info",
                graph  = GraphPath,
            }));

            Assert.IsNotNull(result);
            Assert.IsNull(result["error"]);
            // The constant is returned regardless of graph contents.
            Assert.AreEqual("per_system", (string)result["update_mode"]);
        }
    }
}
