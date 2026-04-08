// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxCatalogGeneratorTests.cs
// Phase 2 task 11: end-to-end determinism test for the catalog generator.
// Regenerating twice must produce byte-identical output.
using System.IO;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxCatalogGeneratorTests
    {
        [Test]
        public void Regeneration_IsByteIdentical()
        {
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
