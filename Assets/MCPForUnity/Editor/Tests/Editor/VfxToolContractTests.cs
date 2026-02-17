// using Newtonsoft.Json.Linq;
// using NUnit.Framework;
//
//
// namespace MCPForUnity.Editor.Tests
// {
//     public class VfxToolContractTests
//     {
//         [Test]
//         public void ManageVfxGraph_MissingAction_ReturnsStructuredValidationError()
//         {
//             var result = JObject.FromObject(ManageVfxGraph.HandleCommand(new JObject()));
//             Assert.That(result["success"]?.ToObject<bool>(), Is.False);
//             Assert.That(result["error_code"]?.ToString(), Is.EqualTo("missing_action"));
//             Assert.That(result["tool_version"]?.ToString(), Is.Not.Null.And.Not.Empty);
//         }
//
//         [Test]
//         public void ManageVfxGraph_UnknownAction_ReturnsUnknownActionOrPipelineGate()
//         {
//             var result = JObject.FromObject(ManageVfxGraph.HandleCommand(new JObject
//             {
//                 ["action"] = "definitely_not_real"
//             }));
//
//             Assert.That(result["success"]?.ToObject<bool>(), Is.False);
//             var code = result["error_code"]?.ToString();
//             Assert.That(code == "unknown_action" || code == "unsupported_pipeline", Is.True);
//         }
//
//         [Test]
//         public void ManageVfxGraph_AliasAction_ResolvesOrFailsWithKnownContract()
//         {
//             var result = JObject.FromObject(ManageVfxGraph.HandleCommand(new JObject
//             {
//                 ["action"] = "graph_add_node"
//             }));
//
//             Assert.That(result["tool_version"]?.ToString(), Is.Not.Null.And.Not.Empty);
//             Assert.That(result["success"]?.ToObject<bool>(), Is.False);
//             var code = result["error_code"]?.ToString();
//             Assert.That(code == "validation_error" || code == "unsupported_pipeline", Is.True);
//         }
//
//         [Test]
//         public void ManageVfx_Ping_ReturnsVersionedContract()
//         {
//             var result = JObject.FromObject(ManageVFX.HandleCommand(new JObject
//             {
//                 ["action"] = "ping"
//             }));
//
//             Assert.That(result["success"]?.ToObject<bool>(), Is.True);
//             Assert.That(result["tool_version"]?.ToString(), Is.Not.Null.And.Not.Empty);
//             Assert.That(result["data"]?["tool"]?.ToString(), Is.EqualTo("manage_vfx"));
//         }
//     }
// }
