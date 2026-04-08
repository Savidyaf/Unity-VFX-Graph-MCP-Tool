// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxOverrideTests.cs
//
// Phase 5-4: Override layer tests (spec category 13).
// Verifies VfxOverrides.ResolveCollision, ResolveAlias, GetHint, and the
// ShapeError hint wire-in in VfxResponseShaper.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generated;
using SpiralingStudio.VfxMcp.Kernel;

namespace SpiralingStudio.VfxMcp.Catalog.Tests
{
    public class VfxOverrideTests
    {
        // ── ResolveCollision ──────────────────────────────────────────────────

        [Test]
        public void ResolveCollision_Lerp_ReturnsMostLikelyFqn()
        {
            // "Lerp" is the canonical collision example from the spec and plan.
            string fqn = VfxOverrides.ResolveCollision("Lerp");
            Assert.AreEqual("UnityEditor.VFX.Operator.Lerp", fqn);
        }

        [Test]
        public void ResolveCollision_Add_ReturnsOperatorFqn()
        {
            string fqn = VfxOverrides.ResolveCollision("Add");
            Assert.AreEqual("UnityEditor.VFX.Operator.Add", fqn);
        }

        [Test]
        public void ResolveCollision_CaseInsensitive_ReturnsMatch()
        {
            // The collision dictionary uses OrdinalIgnoreCase.
            string fqn = VfxOverrides.ResolveCollision("lerp");
            Assert.AreEqual("UnityEditor.VFX.Operator.Lerp", fqn);
        }

        [Test]
        public void ResolveCollision_NullInput_ReturnsNull()
        {
            Assert.IsNull(VfxOverrides.ResolveCollision(null));
        }

        [Test]
        public void ResolveCollision_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(VfxOverrides.ResolveCollision("NonExistentShortName_xyz"));
        }

        // ── ResolveAlias ──────────────────────────────────────────────────────

        [Test]
        public void ResolveAlias_Type_ReturnsMType()
        {
            // Gap #15: "type" -> "m_Type"
            string raw = VfxOverrides.ResolveAlias("type");
            Assert.AreEqual("m_Type", raw);
        }

        [Test]
        public void ResolveAlias_HlslCode_ReturnsMHLSLCode()
        {
            // Gap #18: "hlslCode" -> "m_HLSLCode"
            string raw = VfxOverrides.ResolveAlias("hlslCode");
            Assert.AreEqual("m_HLSLCode", raw);
        }

        [Test]
        public void ResolveAlias_Expanded_ReturnsMExpanded()
        {
            string raw = VfxOverrides.ResolveAlias("expanded");
            Assert.AreEqual("m_Expanded", raw);
        }

        [Test]
        public void ResolveAlias_NullInput_ReturnsNull()
        {
            Assert.IsNull(VfxOverrides.ResolveAlias(null));
        }

        [Test]
        public void ResolveAlias_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(VfxOverrides.ResolveAlias("nonexistent_alias_xyz"));
        }

        // ── GetHint ───────────────────────────────────────────────────────────

        [Test]
        public void GetHint_SettingUsedAsProperty_ReturnsTemplateWithPlaceholders()
        {
            string hint = VfxOverrides.GetHint("setting_used_as_property");
            Assert.IsNotNull(hint, "GetHint must return a non-null template for known error codes.");
            StringAssert.Contains("{name}", hint,
                "Hint template must contain the {name} placeholder.");
            StringAssert.Contains("{type}", hint,
                "Hint template must contain the {type} placeholder.");
        }

        [Test]
        public void GetHint_NameCollision_ReturnsNonNullTemplate()
        {
            string hint = VfxOverrides.GetHint("name_collision");
            Assert.IsNotNull(hint);
            StringAssert.Contains("{name}", hint);
        }

        [Test]
        public void GetHint_AssetPipelineBusy_ReturnsNonNullTemplate()
        {
            string hint = VfxOverrides.GetHint("asset_pipeline_busy");
            Assert.IsNotNull(hint);
        }

        [Test]
        public void GetHint_IntentDiverged_ReturnsNonNullTemplate()
        {
            string hint = VfxOverrides.GetHint("intent_diverged");
            Assert.IsNotNull(hint);
        }

        [Test]
        public void GetHint_NonExistentCode_ReturnsNull()
        {
            string hint = VfxOverrides.GetHint("nonexistent_code_xyz_phase5");
            Assert.IsNull(hint, "GetHint must return null for unknown error codes.");
        }

        [Test]
        public void GetHint_NullCode_ReturnsNull()
        {
            Assert.IsNull(VfxOverrides.GetHint(null));
        }

        // ── ShapeError hint wire-in ───────────────────────────────────────────

        [Test]
        public void ShapeError_WhenHintIsNull_FallsBackToOverridesHint()
        {
            // Build a VfxErrorEnvelope with Code = "name_collision" and no Hint.
            // VfxResponseShaper must look up VfxOverrides.GetHint("name_collision")
            // and include the template in the shaped output.
            var shaper = new VfxResponseShaper();
            var envelope = new VfxErrorEnvelope
            {
                Code = "name_collision",
                Message = "Short-name 'Lerp' is ambiguous.",
                Hint = null,       // no caller-supplied hint
            };

            var result = shaper.ShapeError(envelope);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            var parsed = JObject.Parse(json);

            string hintInOutput = (string)parsed["error"]?["hint"];
            string expectedTemplate = VfxOverrides.GetHint("name_collision");

            Assert.IsNotNull(expectedTemplate,
                "Precondition: GetHint(\"name_collision\") must return a template.");
            Assert.AreEqual(expectedTemplate, hintInOutput,
                "ShapeError must use the VfxOverrides template when Hint is null.");
        }

        [Test]
        public void ShapeError_WhenHintIsNonEmpty_CallerHintTakesPrecedence()
        {
            // When the caller supplies a non-empty Hint, the override layer
            // must NOT overwrite it.
            var shaper = new VfxResponseShaper();
            var callerHint = "Use the FQN instead.";
            var envelope = new VfxErrorEnvelope
            {
                Code = "name_collision",
                Message = "Short-name 'Lerp' is ambiguous.",
                Hint = callerHint,
            };

            var result = shaper.ShapeError(envelope);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            var parsed = JObject.Parse(json);

            string hintInOutput = (string)parsed["error"]?["hint"];
            Assert.AreEqual(callerHint, hintInOutput,
                "Caller-supplied Hint must take precedence over the VfxOverrides template.");
        }

        [Test]
        public void ShapeError_WhenCodeHasNoHint_HintFieldIsEmpty()
        {
            // An unknown error code has no hint in VfxOverrides — the field
            // must be an empty string (not null) in the wire format.
            var shaper = new VfxResponseShaper();
            var envelope = new VfxErrorEnvelope
            {
                Code = "unknown_code_xyz",
                Message = "Something happened.",
                Hint = null,
            };

            var result = shaper.ShapeError(envelope);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            var parsed = JObject.Parse(json);

            string hintInOutput = (string)parsed["error"]?["hint"];
            Assert.AreEqual("", hintInOutput ?? "",
                "hint field must be empty string when no template is registered.");
        }
    }
}
