using MCPForUnity.Editor.Tools.Vfx;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor
{
    public class VfxToolContractTests
    {
        [Test]
        public void Success_ReturnsCorrectShape()
        {
            var result = VfxToolContract.Success("ok", new { foo = 1 });
            var json = JObject.FromObject(result);

            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.IsNull(json["error_code"].ToObject<string>());
            Assert.AreEqual("ok", json["message"].ToString());
            Assert.AreEqual(1, json["data"]["foo"].ToObject<int>());
            Assert.AreEqual(VfxToolContract.ToolVersion, json["tool_version"].ToString());
        }

        [Test]
        public void Error_ReturnsCorrectShape()
        {
            var result = VfxToolContract.Error("test_error", "something broke");
            var json = JObject.FromObject(result);

            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual("test_error", json["error_code"].ToString());
            Assert.AreEqual("something broke", json["message"].ToString());
            Assert.AreEqual(VfxToolContract.ToolVersion, json["tool_version"].ToString());
        }

        [Test]
        public void Error_FallsBackToUnknownErrorWhenCodeEmpty()
        {
            var result = VfxToolContract.Error("", "msg");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.UnknownError, json["error_code"].ToString());
        }

        [Test]
        public void Error_FallsBackToUnknownErrorWhenCodeNull()
        {
            var result = VfxToolContract.Error(null, "msg");
            var json = JObject.FromObject(result);

            Assert.AreEqual(VfxErrorCodes.UnknownError, json["error_code"].ToString());
        }
    }
}
