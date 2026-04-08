// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxResponseShaperTests.cs
//
// Phase 3D-1 — TDD tests for the VfxResponseShaper.
// Verifies that:
//   - Terse mode strictly omits the health report key (release-gate criterion).
//   - Verbose mode includes the health report.
//   - Error envelopes round-trip code/message/hint/retry_after_hint_ms.
//   - !commit.Ok delegates to ShapeError.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxResponseShaperTests
    {
        [Test]
        public void Shape_Terse_OmitsHealthReport()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok = true,
                Health = new VfxHealthReport { Compile = "ok", YamlDiff = "clean", Console = "ok", CompileMs = 12 }
            };

            var output = shaper.Shape(commit, verbose: false);
            var json = JsonConvert.SerializeObject(output);

            Assert.IsFalse(json.Contains("health"),
                "Terse mode must omit the health report (release-gate criterion).");
        }

        [Test]
        public void Shape_Verbose_IncludesHealthReport()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok = true,
                Health = new VfxHealthReport { Compile = "ok", YamlDiff = "clean", Console = "ok", CompileMs = 12 }
            };

            var output = shaper.Shape(commit, verbose: true);
            var json = JsonConvert.SerializeObject(output);

            Assert.IsTrue(json.Contains("health"),
                "Verbose mode must include the health report.");
        }

        [Test]
        public void ShapeError_BuildsErrorEnvelope()
        {
            var shaper = new VfxResponseShaper();
            var error = new VfxErrorEnvelope
            {
                Code = "busy",
                Message = "Editor is compiling.",
                Hint = "Retry after the editor finishes compilation.",
                RetryAfterHintMs = 250,
            };

            var output = shaper.ShapeError(error);
            var json = JsonConvert.SerializeObject(output);
            var parsed = JObject.Parse(json);

            Assert.IsNotNull(parsed["error"], "Output must contain an 'error' object.");
            Assert.AreEqual("busy", (string)parsed["error"]["code"]);
            Assert.AreEqual("Editor is compiling.", (string)parsed["error"]["message"]);
            Assert.AreEqual("Retry after the editor finishes compilation.", (string)parsed["error"]["hint"]);
            Assert.AreEqual(250, (int)parsed["error"]["retry_after_hint_ms"]);
        }

        [Test]
        public void Shape_NotOk_ReturnsErrorEnvelope()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok = false,
                Error = new VfxErrorEnvelope
                {
                    Code = "intent_diverged",
                    Message = "1 intent ops did not appear in the saved YAML.",
                    Hint = "Re-run the operation; the underlying graph may have been edited.",
                }
            };

            var output = shaper.Shape(commit, verbose: false);
            var json = JsonConvert.SerializeObject(output);
            var parsed = JObject.Parse(json);

            Assert.IsNotNull(parsed["error"],
                "When commit.Ok is false, Shape must delegate to ShapeError and return an error envelope.");
            Assert.AreEqual("intent_diverged", (string)parsed["error"]["code"]);
            Assert.AreEqual("1 intent ops did not appear in the saved YAML.", (string)parsed["error"]["message"]);
        }

        [Test]
        public void ShapeMutation_Verbose_FoldsPayloadAndIncludesHealth()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok     = true,
                Health = new VfxHealthReport { YamlDiff = "clean", Compile = "ok", Console = "ok", CompileMs = 12, YamlBytes = 999 },
            };
            var payload = new JObject { ["added"] = new JArray(new JObject { ["token"] = "tok-1" }) };
            var shaped = (JObject)shaper.ShapeMutation(commit, verbose: true, payload);

            Assert.IsNotNull(shaped["added"], "verbose ShapeMutation must fold payload");
            Assert.IsNotNull(shaped["health"], "verbose ShapeMutation must include health");
            // VfxHealthReport fields are public C# fields; Newtonsoft's default
            // contract keeps them as PascalCase when JObject.FromObject serializes.
            Assert.AreEqual("clean", (string)shaped["health"]["YamlDiff"]);
        }

        [Test]
        public void ShapeMutation_Terse_FoldsPayloadAndOmitsHealth()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok     = true,
                Health = new VfxHealthReport { YamlDiff = "clean", Compile = "ok", Console = "ok" },
            };
            var payload = new JObject { ["added"] = new JArray(new JObject { ["token"] = "tok-1" }) };
            var shaped = (JObject)shaper.ShapeMutation(commit, verbose: false, payload);

            Assert.IsNotNull(shaped["added"], "terse ShapeMutation must fold payload");
            Assert.IsNull(shaped["health"], "terse ShapeMutation must NOT include health (token-savings rule)");
        }

        [Test]
        public void ShapeMutation_Errored_DelegatesToShapeError()
        {
            var shaper = new VfxResponseShaper();
            var commit = new VfxCommitResult
            {
                Ok    = false,
                Error = new VfxErrorEnvelope { Code = "test_error", Message = "boom" },
            };
            var shaped = (JObject)shaper.ShapeMutation(commit, verbose: true, new JObject());

            Assert.IsNotNull(shaped["error"]);
            Assert.AreEqual("test_error", (string)shaped["error"]["code"]);
        }
    }
}
