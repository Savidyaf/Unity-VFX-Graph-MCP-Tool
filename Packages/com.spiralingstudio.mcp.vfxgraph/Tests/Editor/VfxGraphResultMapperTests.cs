using MCPForUnity.Editor.Tools.Vfx;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor
{
    public class VfxGraphResultMapperTests
    {
        [Test]
        public void Wrap_NullResult_ReturnsError()
        {
            var result = VfxGraphResultMapper.Wrap(null, "test_action");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual(VfxErrorCodes.UnknownError, json["error_code"].ToString());
            StringAssert.Contains("test_action", json["message"].ToString());
        }

        [Test]
        public void Wrap_SuccessAnonymousObject_ReturnsContractSuccess()
        {
            var raw = new { success = true, message = "done", data = new { id = 42 } };
            var result = VfxGraphResultMapper.Wrap(raw, "add_node");
            var json = JObject.FromObject(result);

            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.AreEqual("done", json["message"].ToString());
            Assert.IsNotNull(json["tool_version"]);
        }

        [Test]
        public void Wrap_FailureAnonymousObject_ReturnsContractError()
        {
            var raw = new { success = false, message = "Node not found" };
            var result = VfxGraphResultMapper.Wrap(raw, "remove_node");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual(VfxErrorCodes.NotFound, json["error_code"].ToString());
        }

        [Test]
        public void Wrap_FailureWithExplicitErrorCode_PreservesCode()
        {
            var raw = new { success = false, error_code = VfxErrorCodes.ValidationError, message = "bad input" };
            var result = VfxGraphResultMapper.Wrap(raw, "set_node_property");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.ValidationError, json["error_code"].ToString());
        }

        [Test]
        public void Wrap_AssetNotFoundRequiresBothKeywords()
        {
            var raw = new { success = false, message = "Created asset successfully" };
            var result = VfxGraphResultMapper.Wrap(raw, "create_asset");
            var json = JObject.FromObject(result);

            Assert.AreNotEqual(VfxErrorCodes.AssetNotFound, json["error_code"].ToString());
        }
    }
}
