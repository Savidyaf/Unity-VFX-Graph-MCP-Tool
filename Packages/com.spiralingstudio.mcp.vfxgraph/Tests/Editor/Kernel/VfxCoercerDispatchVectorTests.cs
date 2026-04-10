// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxCoercerDispatchVectorTests.cs
//
// W4-B (v0.3.1 cluster C-NEW1) — regression guard for VfxCoercerDispatch
// handling of Vector2/Vector3/Vector4 and Color inputs coming in as JObject,
// JArray, or (for Color) hex-string literals.
//
// Surfaced by W2-B while exercising set_attribute: kernel-side code was
// forwarding raw JObject/JArray payloads into VFXSerializedObject.Set, which
// threw with "Cannot assign an object of type Newtonsoft.Json.Linq.JObject to
// VFXSerializedObject of type UnityEditor.VFX.Vector". These tests lock the
// expected coercion path so a JSON {"x":1,"y":2,"z":3} OR [1,2,3] value
// reliably becomes a strongly-typed Unity Vector the downstream slot setter
// can accept.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxCoercerDispatchVectorTests
    {
        // ────────────────────────────────────────────── Vector3 ──────────────────────────────────────────────

        [Test]
        public void Coerce_Vector3_FromJObject_xyz()
        {
            var input = new JObject { ["x"] = 1f, ["y"] = 2f, ["z"] = 3f };
            var result = VfxCoercerDispatch.Coerce(input, typeof(Vector3));
            Assert.IsInstanceOf<Vector3>(result);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), (Vector3)result);
        }

        [Test]
        public void Coerce_Vector3_FromJArray_3floats()
        {
            var input = new JArray(1f, 2f, 3f);
            var result = VfxCoercerDispatch.Coerce(input, typeof(Vector3));
            Assert.IsInstanceOf<Vector3>(result);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), (Vector3)result);
        }

        [Test]
        public void Coerce_Vector3_FromJObject_MissingZ_DefaultsToZero()
        {
            var input = new JObject { ["x"] = 4f, ["y"] = 5f };
            var result = (Vector3)VfxCoercerDispatch.Coerce(input, typeof(Vector3));
            Assert.AreEqual(new Vector3(4f, 5f, 0f), result);
        }

        [Test]
        public void Coerce_Vector3_PassesThrough_WhenAlreadyVector3()
        {
            var input = new Vector3(7f, 8f, 9f);
            var result = VfxCoercerDispatch.Coerce(input, typeof(Vector3));
            Assert.AreEqual(input, (Vector3)result);
        }

        // ────────────────────────────────────────────── Vector2 ──────────────────────────────────────────────

        [Test]
        public void Coerce_Vector2_FromJArray_2floats()
        {
            var input = new JArray(1f, 2f);
            var result = VfxCoercerDispatch.Coerce(input, typeof(Vector2));
            Assert.IsInstanceOf<Vector2>(result);
            Assert.AreEqual(new Vector2(1f, 2f), (Vector2)result);
        }

        [Test]
        public void Coerce_Vector2_FromJObject_xy()
        {
            var input = new JObject { ["x"] = 1.5f, ["y"] = 2.5f };
            var result = (Vector2)VfxCoercerDispatch.Coerce(input, typeof(Vector2));
            Assert.AreEqual(new Vector2(1.5f, 2.5f), result);
        }

        // ────────────────────────────────────────────── Vector4 ──────────────────────────────────────────────

        [Test]
        public void Coerce_Vector4_FromJArray_4floats()
        {
            var input = new JArray(1f, 2f, 3f, 4f);
            var result = (Vector4)VfxCoercerDispatch.Coerce(input, typeof(Vector4));
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 4f), result);
        }

        [Test]
        public void Coerce_Vector4_FromJObject_xyzw()
        {
            var input = new JObject { ["x"] = 1f, ["y"] = 2f, ["z"] = 3f, ["w"] = 4f };
            var result = (Vector4)VfxCoercerDispatch.Coerce(input, typeof(Vector4));
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 4f), result);
        }

        // ────────────────────────────────────────────── Color ──────────────────────────────────────────────

        [Test]
        public void Coerce_Color_FromJObject_rgba()
        {
            var input = new JObject { ["r"] = 0.5f, ["g"] = 0.5f, ["b"] = 0.5f, ["a"] = 1f };
            var result = VfxCoercerDispatch.Coerce(input, typeof(Color));
            Assert.IsInstanceOf<Color>(result);
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), (Color)result);
        }

        [Test]
        public void Coerce_Color_FromJArray_rgba()
        {
            var input = new JArray(0.25f, 0.5f, 0.75f, 1f);
            var result = (Color)VfxCoercerDispatch.Coerce(input, typeof(Color));
            Assert.AreEqual(new Color(0.25f, 0.5f, 0.75f, 1f), result);
        }

        [Test]
        public void Coerce_Color_FromHexString_RRGGBB()
        {
            // #FF8040  → r=255/255, g=128/255, b=64/255, a=1
            var result = (Color)VfxCoercerDispatch.Coerce("#FF8040", typeof(Color));
            Assert.AreEqual(1f,          result.r, 1e-5f);
            Assert.AreEqual(128f / 255f, result.g, 1e-5f);
            Assert.AreEqual(64f  / 255f, result.b, 1e-5f);
            Assert.AreEqual(1f,          result.a, 1e-5f);
        }

        [Test]
        public void Coerce_Color_FromHexString_RRGGBBAA()
        {
            var result = (Color)VfxCoercerDispatch.Coerce("#FF000080", typeof(Color));
            Assert.AreEqual(1f,           result.r, 1e-5f);
            Assert.AreEqual(0f,           result.g, 1e-5f);
            Assert.AreEqual(0f,           result.b, 1e-5f);
            Assert.AreEqual(128f / 255f,  result.a, 1e-5f);
        }
    }
}
