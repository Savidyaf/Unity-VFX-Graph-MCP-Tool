// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxDiagListSettingsTests.cs
//
// v0.3.2 F10 catalog lift (Cluster W3-C) — verifies vfx_diag.list_settings
// now returns per-type [VFXSetting] field lists instead of the
// not_implemented stub shipped in v0.3.1.
//
// Each paginated entry is { type_fqn: string, settings: string[] }.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxDiagListSettingsTests
    {
        [Test]
        public void ListSettings_FirstPage_HasWellFormedEnvelope()
        {
            var response = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "list_settings",
                page   = 0,
            }));

            Assert.IsNotNull(response, "response must not be null");
            Assert.IsNull(response["error"],
                $"list_settings returned an error envelope: {response["error"]}");

            // Envelope shape — same pagination keys as list_node_types.
            var settings = response["settings"] as JArray;
            Assert.IsNotNull(settings, "response must include 'settings' array");
            Assert.IsNotNull(response["page"], "response must include 'page'");
            Assert.IsNotNull(response["total"], "response must include 'total'");
            Assert.IsNotNull(response["has_next"], "response must include 'has_next'");

            // F10 lift assertion: not the old not_implemented stub.
            Assert.IsNull(response["state"],
                "list_settings should no longer carry the not_implemented 'state' key");
            Assert.Greater(settings.Count, 0,
                "settings[] must be non-empty after F10 catalog lift");
            Assert.Greater(response["total"].Value<int>(), 0,
                "total must be > 0 after F10 catalog lift");

            // Each entry on page 0 must have type_fqn + non-empty settings[].
            foreach (var entry in settings)
            {
                var entryObj = entry as JObject;
                Assert.IsNotNull(entryObj, "each settings entry must be a JObject");

                string typeFqn = entryObj["type_fqn"]?.Value<string>();
                Assert.IsFalse(string.IsNullOrEmpty(typeFqn),
                    "each entry must include a non-empty 'type_fqn'");

                var fieldArr = entryObj["settings"] as JArray;
                Assert.IsNotNull(fieldArr, $"entry {typeFqn} missing 'settings' array");
                Assert.Greater(fieldArr.Count, 0,
                    $"entry {typeFqn} must have at least one setting field (empty types are filtered)");
            }
        }

        [Test]
        public void ListSettings_Paginated_IncludesOperatorTypes()
        {
            // Walk every page (cap at 40 pages = 2000 entries — more than
            // enough for VFX Graph 17) until we find at least one entry
            // whose type_fqn starts with UnityEditor.VFX.Operator.*. Alpha
            // ordering puts Block.* ahead of Operator.*, so the operator
            // entries land on later pages.
            bool sawOperator = false;
            for (int page = 0; page < 40 && !sawOperator; page++)
            {
                var response = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                    action = "list_settings",
                    page   = page,
                }));
                Assert.IsNull(response["error"],
                    $"list_settings page {page} returned error: {response["error"]}");

                var settings = response["settings"] as JArray;
                if (settings == null || settings.Count == 0) break;

                foreach (var entry in settings)
                {
                    string typeFqn = (entry as JObject)?["type_fqn"]?.Value<string>();
                    if (typeFqn != null && typeFqn.StartsWith("UnityEditor.VFX.Operator."))
                    {
                        sawOperator = true;
                        break;
                    }
                }

                bool hasNext = response["has_next"]?.Value<bool>() ?? false;
                if (!hasNext) break;
            }

            Assert.IsTrue(sawOperator,
                "at least one UnityEditor.VFX.Operator.* entry must appear in the settings catalog");
        }
    }
}
