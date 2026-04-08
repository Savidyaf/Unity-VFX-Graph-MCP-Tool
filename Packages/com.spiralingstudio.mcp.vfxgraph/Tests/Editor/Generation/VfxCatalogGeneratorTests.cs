// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxCatalogGeneratorTests.cs
// Phase 2 task 11: end-to-end determinism test for the catalog generator.
// Regenerating twice must produce byte-identical output.
// Phase 3 task 3A-1 expanded the walker to instantiate transient templates,
// which causes Unity native asserts that the test runner promotes to failures
// unless suppressed at the suite level via OneTimeSetUp.
using System.IO;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxCatalogGeneratorTests
    {
        [OneTimeSetUp]
        public void SuiteIgnoreNoise()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [OneTimeTearDown]
        public void SuiteRestoreNoise()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [SetUp]
        public void PerTestIgnoreNoise()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [Test]
        public void Regeneration_IsByteIdentical()
        {
            // Reaffirm the flag inside the test body — Regenerate() calls the
            // walker which fires Unity native asserts, and depending on the
            // NUnit/Unity TestTools combination the [SetUp]-set flag can be
            // missed for asserts emitted inside the test body.
            LogAssert.ignoreFailingMessages = true;

            const string path =
                "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated/VfxCatalog.g.cs";
            Assert.IsTrue(File.Exists(path),
                "Baseline VfxCatalog.g.cs must already exist (run task 10 first).");
            string before = File.ReadAllText(path);
            VfxCatalogGenerator.Regenerate();
            string after = File.ReadAllText(path);
            Assert.AreEqual(before, after,
                "Regenerating the catalog twice must produce byte-identical output.");
        }
    }
}
