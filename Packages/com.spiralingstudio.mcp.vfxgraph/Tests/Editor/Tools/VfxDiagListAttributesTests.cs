// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxDiagListAttributesTests.cs
//
// v0.3.2 F10 catalog lift (Cluster W3-C) — verifies vfx_diag.list_attributes
// now returns the populated built-in attribute catalog instead of the
// not_implemented stub shipped in v0.3.1.
//
// Assertions follow the same pagination envelope as list_node_types:
//   { attributes: [...], page: int, total: int, has_next: bool }

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxDiagListAttributesTests
    {
        [Test]
        public void ListAttributes_ReturnsPopulatedPagedCatalog()
        {
            var response = (JObject)VfxDiagTool.HandleCommand(JObject.FromObject(new {
                action = "list_attributes",
                page   = 0,
            }));

            Assert.IsNotNull(response, "response must not be null");
            Assert.IsNull(response["error"],
                $"list_attributes returned an error envelope: {response["error"]}");

            // Envelope shape
            var attributes = response["attributes"] as JArray;
            Assert.IsNotNull(attributes, "response must include 'attributes' array");
            Assert.IsNotNull(response["page"], "response must include 'page'");
            Assert.IsNotNull(response["total"], "response must include 'total'");
            Assert.IsNotNull(response["has_next"], "response must include 'has_next'");

            // F10 lift assertion: not the old not_implemented stub
            Assert.IsNull(response["state"],
                "list_attributes should no longer carry the not_implemented 'state' key");
            Assert.Greater(attributes.Count, 0,
                "attributes[] must be non-empty after F10 catalog lift");
            Assert.Greater(response["total"].Value<int>(), 0,
                "total must be > 0 after F10 catalog lift");

            // Canonical built-ins must be present — these are spec'd by
            // VFXAttributesManager.s_BuiltInAttributes in Unity 6000.4.
            bool hasPosition = false;
            bool hasVelocity = false;
            foreach (var token in attributes)
            {
                string name = token.Value<string>();
                if (name == "position") hasPosition = true;
                if (name == "velocity") hasVelocity = true;
            }
            Assert.IsTrue(hasPosition, "built-in 'position' attribute must be present");
            Assert.IsTrue(hasVelocity, "built-in 'velocity' attribute must be present");
        }
    }
}
