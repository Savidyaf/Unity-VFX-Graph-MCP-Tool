// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/CatalogEmitterTests.cs
// Phase 2 task 9: failing-first tests for CatalogEmitter.
// Phase 3 task 3A-1 expansion of the walker now instantiates transient
// templates, which causes Unity native code to emit harmless asserts (e.g.
// "Texture3D missing ExtensionOfNativeClass"). Walking once at the suite
// level (and stashing the IR + templates) lets us suppress the asserts in a
// single setup hook and amortise the walker cost across the suite.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class CatalogEmitterTests
    {
        private static CatalogIR s_ir;
        private static List<UnityEditor.VFX.VFXModel> s_templates;

        [OneTimeSetUp]
        public void WalkOnce()
        {
            LogAssert.ignoreFailingMessages = true;
            (s_ir, s_templates) = new VfxLibraryWalker().WalkWithTemplates();
        }

        [OneTimeTearDown]
        public void DestroyTemplates()
        {
            if (s_templates != null)
            {
                foreach (var t in s_templates)
                    if (t != null) Object.DestroyImmediate(t);
                s_templates = null;
            }
            s_ir = null;
            LogAssert.ignoreFailingMessages = false;
        }

        [SetUp]
        public void PerTestIgnore()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [Test]
        public void Emit_ProducesCompilableOutput()
        {
            var emitter = new CatalogEmitter();
            string output = emitter.Emit(s_ir);
            Assert.IsNotEmpty(output);
            StringAssert.Contains("namespace SpiralingStudio.VfxMcp.Generated", output);
            StringAssert.Contains("internal static class VfxCatalog", output);
            StringAssert.Contains("public static readonly string[] Operators", output);
        }

        [Test]
        public void Emit_IsDeterministic()
        {
            var a = new CatalogEmitter().Emit(s_ir);
            var b = new CatalogEmitter().Emit(s_ir);
            Assert.AreEqual(a, b, "Emitter output must be byte-identical across runs.");
        }

        [Test]
        public void Emit_IncludesEveryOperatorFQN()
        {
            string output = new CatalogEmitter().Emit(s_ir);
            foreach (var op in s_ir.Operators)
                StringAssert.Contains(op.TypeFQN, output);
        }

        [Test]
        public void Emit_DeclaresCollisionsTable()
        {
            // Erratum P-M1: deduplicate short names and emit a Collisions table so
            // the override layer (Quirks.yaml) + vfx_diag.list_collisions can surface them.
            string output = new CatalogEmitter().Emit(s_ir);
            StringAssert.Contains("Collisions", output);
            StringAssert.Contains("ShortNameToFqn", output);
        }
    }
}
