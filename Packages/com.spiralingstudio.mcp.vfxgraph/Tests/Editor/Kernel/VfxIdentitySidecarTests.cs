// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxIdentitySidecarTests.cs
//
// Lane 3B task 3B-2: round-trip + missing-token + v1->v2 migration tests for
// VfxIdentitySidecar. Each test uses a unique temp file path (NOT
// Library/VfxMcpIdentity.json) so that parallel-running test workers cannot
// collide on shared state.
using System;
using System.IO;
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxIdentitySidecarTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(
                Path.GetTempPath(),
                "VfxMcpIdentityTest_" + Guid.NewGuid().ToString("n") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempPath != null && File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); }
                catch { /* best effort */ }
            }
        }

        [Test]
        public void RoundTrip_AddThenGetThenFlushThenReload()
        {
            var sidecar = new VfxIdentitySidecar(_tempPath);
            sidecar.Put("guid1", "n_abc12", 0xDEADBEEFUL,
                "UnityEditor.VFX.VFXInlineOperator", 0xCAFEBABEUL);
            sidecar.Flush();

            var reloaded = new VfxIdentitySidecar(_tempPath);
            Assert.AreEqual(0xDEADBEEFUL, reloaded.Get("guid1", "n_abc12"));
            Assert.AreEqual("UnityEditor.VFX.VFXInlineOperator",
                reloaded.GetTypeFqn("guid1", "n_abc12"));
            Assert.AreEqual(0xCAFEBABEUL, reloaded.GetParentFingerprint("guid1", "n_abc12"));
        }

        [Test]
        public void Get_MissingToken_ReturnsZero()
        {
            var sidecar = new VfxIdentitySidecar(_tempPath);
            Assert.AreEqual(0UL, sidecar.Get("missing", "missing"));
            Assert.IsNull(sidecar.GetTypeFqn("missing", "missing"));
            Assert.AreEqual(0UL, sidecar.GetParentFingerprint("missing", "missing"));
        }

        [Test]
        public void Put_OverwritesExisting()
        {
            var sidecar = new VfxIdentitySidecar(_tempPath);
            sidecar.Put("g", "t", 0x111UL, "T1", 0x222UL);
            sidecar.Put("g", "t", 0x333UL, "T2", 0x444UL);
            Assert.AreEqual(0x333UL, sidecar.Get("g", "t"));
            Assert.AreEqual("T2", sidecar.GetTypeFqn("g", "t"));
            Assert.AreEqual(0x444UL, sidecar.GetParentFingerprint("g", "t"));
        }

        [Test]
        public void Flush_NoOpIfNotDirty()
        {
            // Reading without writing should not create the file.
            var sidecar = new VfxIdentitySidecar(_tempPath);
            sidecar.Flush();
            Assert.IsFalse(File.Exists(_tempPath),
                "Flush() must not write the sidecar file when nothing was Put().");
        }

        [Test]
        public void Load_V1Legacy_FlatFormat_Migrates()
        {
            // Hand-craft a v1 file: the legacy schema was Token -> hex string,
            // wrapped in Dictionary<guid, Dictionary<token, hex>>. The v1 file
            // has no top-level "version"/"assets" envelope.
            File.WriteAllText(_tempPath,
                "{ \"old-guid\": { \"n_legacy\": \"deadbeef\" } }");

            var sidecar = new VfxIdentitySidecar(_tempPath);

            // Strict-match path still works (we have a fingerprint).
            Assert.AreEqual(0xDEADBEEFUL, sidecar.Get("old-guid", "n_legacy"));

            // Per erratum P-H3: v1 entries lack type/parentFp; loose recovery
            // for these entries throws node_lost. The sidecar must report null
            // for type and 0 for parent fingerprint.
            Assert.IsNull(sidecar.GetTypeFqn("old-guid", "n_legacy"));
            Assert.AreEqual(0UL, sidecar.GetParentFingerprint("old-guid", "n_legacy"));
        }

        [Test]
        public void Load_V1Legacy_PutThenFlush_WritesV2Schema()
        {
            File.WriteAllText(_tempPath,
                "{ \"g\": { \"n_old\": \"abcdef0123456789\" } }");

            var sidecar = new VfxIdentitySidecar(_tempPath);
            sidecar.Put("g", "n_new", 0x1234UL, "T", 0x5678UL);
            sidecar.Flush();

            string raw = File.ReadAllText(_tempPath);
            Assert.IsTrue(raw.Contains("\"version\""),
                "Flushed sidecar must use the v2 envelope (`version` key).");
            Assert.IsTrue(raw.Contains("\"assets\""),
                "Flushed sidecar must use the v2 envelope (`assets` key).");

            var reloaded = new VfxIdentitySidecar(_tempPath);
            // The migrated v1 entry survives the round-trip.
            Assert.AreEqual(0xABCDEF0123456789UL, reloaded.Get("g", "n_old"));
            // The new v2 entry round-trips fully.
            Assert.AreEqual(0x1234UL, reloaded.Get("g", "n_new"));
            Assert.AreEqual("T", reloaded.GetTypeFqn("g", "n_new"));
            Assert.AreEqual(0x5678UL, reloaded.GetParentFingerprint("g", "n_new"));
        }

        [Test]
        public void Put_NullParentFingerprint_StoredAsZero()
        {
            // Top-level nodes (no parent context) pass parentFingerprint = 0.
            // The sidecar must round-trip 0 as 0 without confusing it with "missing".
            var sidecar = new VfxIdentitySidecar(_tempPath);
            sidecar.Put("g", "n_top", 0xAAAAUL, "TopType", 0UL);
            sidecar.Flush();
            var reloaded = new VfxIdentitySidecar(_tempPath);
            Assert.AreEqual(0xAAAAUL, reloaded.Get("g", "n_top"));
            Assert.AreEqual("TopType", reloaded.GetTypeFqn("g", "n_top"));
            Assert.AreEqual(0UL, reloaded.GetParentFingerprint("g", "n_top"));
        }
    }
}
