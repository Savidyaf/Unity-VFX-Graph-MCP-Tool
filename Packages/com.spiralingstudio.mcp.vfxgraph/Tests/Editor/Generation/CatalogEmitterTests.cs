// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/CatalogEmitterTests.cs
// Phase 2 task 9: failing-first tests for CatalogEmitter.
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class CatalogEmitterTests
    {
        [Test]
        public void Emit_ProducesCompilableOutput()
        {
            var ir = new VfxLibraryWalker().Walk();
            var emitter = new CatalogEmitter();
            string output = emitter.Emit(ir);
            Assert.IsNotEmpty(output);
            StringAssert.Contains("namespace SpiralingStudio.VfxMcp.Generated", output);
            StringAssert.Contains("internal static class VfxCatalog", output);
            StringAssert.Contains("public static readonly string[] Operators", output);
        }

        [Test]
        public void Emit_IsDeterministic()
        {
            var ir = new VfxLibraryWalker().Walk();
            var a = new CatalogEmitter().Emit(ir);
            var b = new CatalogEmitter().Emit(ir);
            Assert.AreEqual(a, b, "Emitter output must be byte-identical across runs.");
        }

        [Test]
        public void Emit_IncludesEveryOperatorFQN()
        {
            var ir = new VfxLibraryWalker().Walk();
            string output = new CatalogEmitter().Emit(ir);
            foreach (var op in ir.Operators)
                StringAssert.Contains(op.TypeFQN, output);
        }

        [Test]
        public void Emit_DeclaresCollisionsTable()
        {
            // Erratum P-M1: deduplicate short names and emit a Collisions table so
            // the override layer (Quirks.yaml) + vfx_diag.list_collisions can surface them.
            var ir = new VfxLibraryWalker().Walk();
            string output = new CatalogEmitter().Emit(ir);
            StringAssert.Contains("Collisions", output);
            StringAssert.Contains("ShortNameToFqn", output);
        }
    }
}
