// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxStructuralFingerprintTests.cs
//
// Lane 3B task 3B-1: failing-then-green tests for the FNV1a64 fingerprint
// helper. Identity tests that exercise live VFXModel walking live in
// VfxIdentityTests.cs because they need a real .vfx asset.
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxStructuralFingerprintTests
    {
        [Test]
        public void Fnv1a64_Deterministic()
        {
            var a = VfxStructuralFingerprint.Fnv1a64("test input");
            var b = VfxStructuralFingerprint.Fnv1a64("test input");
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Fnv1a64_DifferentInputs_DifferentHashes()
        {
            var a = VfxStructuralFingerprint.Fnv1a64("test input");
            var b = VfxStructuralFingerprint.Fnv1a64("different input");
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Fnv1a64_EmptyString_KnownOffsetBasis()
        {
            // FNV-1a 64-bit offset basis is 14695981039346656037 — feeding the
            // empty string returns the basis unchanged. This locks the constant.
            var a = VfxStructuralFingerprint.Fnv1a64("");
            Assert.AreEqual(14695981039346656037UL, a);
        }

        [Test]
        public void ShortToken_5Hex_LengthAndPrefix()
        {
            var token = VfxStructuralFingerprint.ToShortToken("n_", 0xDEADBEEFCAFEBABEUL);
            Assert.IsTrue(token.StartsWith("n_"), $"Expected token to start with 'n_', got '{token}'");
            Assert.AreEqual(7, token.Length, "Expected 'n_' + 5 hex chars = 7");
        }

        [Test]
        public void ShortToken_5Hex_TopBits()
        {
            // 0xDEADBEEFCAFEBABE shifted right by 44 bits = 0xDEADB.
            // Top 20 bits → 5 hex chars (lowercase hex format).
            var token = VfxStructuralFingerprint.ToShortToken("n_", 0xDEADBEEFCAFEBABEUL);
            Assert.AreEqual("n_deadb", token);
        }

        [Test]
        public void ShortToken_DifferentPrefixes()
        {
            var n = VfxStructuralFingerprint.ToShortToken("n_", 0x1234567890ABCDEFUL);
            var b = VfxStructuralFingerprint.ToShortToken("b_", 0x1234567890ABCDEFUL);
            var ctx = VfxStructuralFingerprint.ToShortToken("ctx_", 0x1234567890ABCDEFUL);
            Assert.IsTrue(n.StartsWith("n_"));
            Assert.IsTrue(b.StartsWith("b_"));
            Assert.IsTrue(ctx.StartsWith("ctx_"));
            // The 5-hex tail must be identical for the same fingerprint.
            Assert.AreEqual(n.Substring(2), b.Substring(2));
            Assert.AreEqual(n.Substring(2), ctx.Substring(4));
        }

        [Test]
        public void ShortToken_PadsLeadingZeros()
        {
            // fingerprint < 2^44 → top-20 bits == 0 → must still be 5 hex chars.
            var token = VfxStructuralFingerprint.ToShortToken("n_", 0x00000000_00000001UL);
            Assert.AreEqual("n_00000", token);
        }
    }
}
