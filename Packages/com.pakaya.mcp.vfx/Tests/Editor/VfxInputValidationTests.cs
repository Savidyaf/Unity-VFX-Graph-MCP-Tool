using MCPForUnity.Editor.Tools.Vfx;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Pakaya.Mcp.Vfx.Tests.Editor
{
    public class VfxInputValidationTests
    {
        [Test]
        public void TryValidateAssetPath_NormalizesRelativePathAndExtension()
        {
            bool ok = VfxInputValidation.TryValidateAssetPath(
                "VFX/MyGraph",
                ".vfx",
                out string normalized,
                out object error);

            Assert.IsTrue(ok);
            Assert.IsNull(error);
            Assert.AreEqual("Assets/VFX/MyGraph.vfx", normalized);
        }

        [Test]
        public void TryValidateAssetPath_RejectsPathTraversal()
        {
            bool ok = VfxInputValidation.TryValidateAssetPath(
                "Assets/../../Secrets/Bad",
                ".vfx",
                out _,
                out object error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
            var json = JObject.FromObject(error);
            Assert.AreEqual(false, json["success"]?.ToObject<bool>());
        }

        [Test]
        public void TryValidateAssetPath_AcceptsPackagesPath()
        {
            bool ok = VfxInputValidation.TryValidateAssetPath(
                "Packages/com.test/MyGraph.vfx",
                ".vfx",
                out string normalized,
                out _);

            Assert.IsTrue(ok);
            Assert.AreEqual("Packages/com.test/MyGraph.vfx", normalized);
        }

        [Test]
        public void TryValidateAssetPath_RejectsEmptyPath()
        {
            bool ok = VfxInputValidation.TryValidateAssetPath(
                "",
                ".vfx",
                out _,
                out object error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryGetRequiredString_ReturnsValue()
        {
            var p = new JObject { ["action"] = "add_node" };
            bool ok = VfxInputValidation.TryGetRequiredString(p, "action", out string value, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual("add_node", value);
        }

        [Test]
        public void TryGetRequiredString_FailsOnMissing()
        {
            var p = new JObject();
            bool ok = VfxInputValidation.TryGetRequiredString(p, "action", out _, out object error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryGetRequiredString_FailsOnEmptyString()
        {
            var p = new JObject { ["action"] = "" };
            bool ok = VfxInputValidation.TryGetRequiredString(p, "action", out _, out object error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryGetRequiredString_FailsOnNullParams()
        {
            bool ok = VfxInputValidation.TryGetRequiredString(null, "action", out _, out object error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }
    }
}
