// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Catalog/VfxCatalogCompletenessTests.cs
// Phase 3 task 3A-8: assert that the generated catalog covers every type that
// VFXLibrary surfaces, minus the subgraph carve-outs the walker performs.
//
// ERRATUM P-L2: derive expected counts from walker.SubgraphOperators.Count etc.,
// not a hardcoded -1. If VFXLibrary ever ships zero or multiple subgraph ops the
// hardcoded version would silently pass or fail.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Catalog.Tests
{
    public class VfxCatalogCompletenessTests
    {
        // Walking the catalog instantiates models that can emit Unity native
        // log assertions; suppress them at the suite level.
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
        public void GeneratedCatalog_Operators_MatchesVFXLibraryCount()
        {
            int expected = VFXLibrary.GetOperators().Count() - s_ir.SubgraphOperators.Count;
            Assert.AreEqual(expected, Generated.VfxCatalog.Operators.Length,
                "Generated catalog operator count must equal VFXLibrary.GetOperators() minus the carved-out subgraph operators.");
        }

        [Test]
        public void GeneratedCatalog_Blocks_MatchesVFXLibraryCount()
        {
            int expected = VFXLibrary.GetBlocks().Count() - s_ir.SubgraphBlocks.Count;
            Assert.AreEqual(expected, Generated.VfxCatalog.Blocks.Length,
                "Generated catalog block count must equal VFXLibrary.GetBlocks() minus the carved-out subgraph blocks.");
        }

        [Test]
        public void GeneratedCatalog_Contexts_MatchesVFXLibraryCount()
        {
            int expected = VFXLibrary.GetContexts().Count() - s_ir.SubgraphContexts.Count;
            Assert.AreEqual(expected, Generated.VfxCatalog.Contexts.Length,
                "Generated catalog context count must equal VFXLibrary.GetContexts() minus the carved-out subgraph contexts.");
        }

        [Test]
        public void GeneratedCatalog_Parameters_MatchesVFXLibraryCount()
        {
            Assert.AreEqual(VFXLibrary.GetParameters().Count(), Generated.VfxCatalog.Parameters.Length,
                "Generated catalog parameter count must equal VFXLibrary.GetParameters().");
        }

        [Test]
        public void GeneratedCatalog_SubgraphOperators_NonEmpty()
        {
            Assert.GreaterOrEqual(Generated.VfxCatalog.SubgraphOperators.Length, 1,
                "VFXSubgraphOperator must appear in the generated subgraph operator list.");
        }
    }
}
