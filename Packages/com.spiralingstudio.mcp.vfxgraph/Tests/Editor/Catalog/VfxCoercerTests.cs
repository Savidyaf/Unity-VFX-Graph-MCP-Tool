// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCoercerTests.cs
// Phase 3 task 3A-3: round-trip tests for the generated VfxCoercers.g.cs.
// The coercers turn JToken → strongly typed slot values that NodeOps can hand
// to VFXSlot.value. Float, Vector3 (array + object), and Color are exercised
// because they're the most common slot value types in user-authored graphs.
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Generated;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Catalog.Tests
{
    public class VfxCoercerTests
    {
        [SetUp]
        public void IgnoreNoise()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [Test]
        public void Float_RoundTrip()
        {
            var input = new JValue(1.5f);
            var coerced = VfxCoercers.CoerceToFloat(input);
            Assert.AreEqual(1.5f, coerced);
        }

        [Test]
        public void Float_FromInteger_Coerces()
        {
            var input = new JValue(2);
            var coerced = VfxCoercers.CoerceToFloat(input);
            Assert.AreEqual(2f, coerced);
        }

        [Test]
        public void Vector3_FromJsonArray_RoundTrip()
        {
            var input = JArray.Parse("[1.0, 2.0, 3.0]");
            Vector3 coerced = VfxCoercers.CoerceToVector3(input);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), coerced);
        }

        [Test]
        public void Vector3_FromJsonObject_RoundTrip()
        {
            var input = JObject.Parse("{\"x\":1,\"y\":2,\"z\":3}");
            Vector3 coerced = VfxCoercers.CoerceToVector3(input);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), coerced);
        }

        [Test]
        public void Color_FromJsonArray_RoundTrip()
        {
            var input = JArray.Parse("[1.0, 0.5, 0.25, 1.0]");
            Color coerced = VfxCoercers.CoerceToColor(input);
            Assert.AreEqual(1f, coerced.r);
            Assert.AreEqual(0.5f, coerced.g);
            Assert.AreEqual(0.25f, coerced.b);
            Assert.AreEqual(1f, coerced.a);
        }

        [Test]
        public void Color_FromJsonObject_RoundTrip()
        {
            var input = JObject.Parse("{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":1.0}");
            Color coerced = VfxCoercers.CoerceToColor(input);
            Assert.AreEqual(1f, coerced.r);
            Assert.AreEqual(0.5f, coerced.g);
            Assert.AreEqual(0.25f, coerced.b);
            Assert.AreEqual(1f, coerced.a);
        }
    }
}
