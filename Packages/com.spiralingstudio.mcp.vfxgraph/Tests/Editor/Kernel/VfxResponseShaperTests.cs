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
    }
}
