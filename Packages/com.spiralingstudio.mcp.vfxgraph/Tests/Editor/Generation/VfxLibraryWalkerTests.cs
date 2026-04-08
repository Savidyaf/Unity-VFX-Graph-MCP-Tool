// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
// Phase 2 task 7: failing-first test for the operator-only walker baseline.
// Phase 3 task 3A-1 extends the suite for settings + slot bindings capture.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxLibraryWalkerTests
    {
        // Walking the entire VFXLibrary instantiates transient templates for
        // every operator/block/context. Some of those models cause Unity native
        // code to emit asserts (e.g. "'UnityEngine.Texture3D' is missing the
        // class attribute 'ExtensionOfNativeClass'!") that the test runner
        // promotes to failures via LogAssert. Walking once in [OneTimeSetUp]
        // and stashing the IR/templates lets us swallow those assertions in a
        // single place, and also halves the cost of the suite.
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
        public void Walk_Operators_MatchesVFXLibraryCount()
        {
            var ir = s_ir;
            // After task 8 the walker splits VFXSubgraphOperator out of ir.Operators,
            // so we add the subgraph count back in to compare with the raw VFXLibrary count.
            int raw = VFXLibrary.GetOperators().Count();
            Assert.AreEqual(raw, ir.Operators.Count + ir.SubgraphOperators.Count,
                "ir.Operators + ir.SubgraphOperators should equal VFXLibrary.GetOperators().");
        }

        [Test]
        public void Walk_Operators_EachHasFQN()
        {
            var ir = s_ir;
            foreach (var op in ir.Operators)
            {
                Assert.IsNotEmpty(op.TypeFQN, $"Operator with short name {op.ShortName} has no FQN.");
                Assert.IsNotEmpty(op.ShortName, $"Operator with FQN {op.TypeFQN} has no short name.");
            }
        }

        [Test]
        public void Walk_KnownOperator_LerpIsPresent()
        {
            var ir = s_ir;
            var lerp = ir.Operators.FirstOrDefault(o => o.TypeFQN == "UnityEditor.VFX.Operator.Lerp");
            Assert.NotNull(lerp, "Lerp operator must be in the catalog (it was gap #9 in the original tool).");
        }

        // ─────────── Phase 2 task 8 — extended walker coverage ───────────

        [Test]
        public void Walk_Blocks_MatchesVFXLibraryCount()
        {
            var ir = s_ir;
            // The walker pulls VFXSubgraphBlock out of ir.Blocks, so we add the
            // subgraph-block count back in to compare with the raw VFXLibrary count.
            int raw = VFXLibrary.GetBlocks().Count();
            Assert.AreEqual(raw, ir.Blocks.Count + ir.SubgraphBlocks.Count,
                "ir.Blocks + ir.SubgraphBlocks should equal VFXLibrary.GetBlocks().");
        }

        [Test]
        public void Walk_Contexts_MatchesVFXLibraryCount()
        {
            var ir = s_ir;
            int raw = VFXLibrary.GetContexts().Count();
            Assert.AreEqual(raw, ir.Contexts.Count + ir.SubgraphContexts.Count,
                "ir.Contexts + ir.SubgraphContexts should equal VFXLibrary.GetContexts().");
        }

        [Test]
        public void Walk_Parameters_MatchesVFXLibraryCount()
        {
            var ir = s_ir;
            Assert.AreEqual(VFXLibrary.GetParameters().Count(), ir.Parameters.Count);
        }

        [Test]
        public void Walk_Operators_ExcludesSubgraphOperator()
        {
            var ir = s_ir;
            // After the typeof()-based split, VFXSubgraphOperator must NOT
            // appear in ir.Operators (it lives in ir.SubgraphOperators instead).
            Assert.IsFalse(ir.Operators.Any(o => o.ModelType == typeof(UnityEditor.VFX.VFXSubgraphOperator)),
                "VFXSubgraphOperator should be split out into ir.SubgraphOperators.");
        }

        [Test]
        public void Walk_Subgraphs_IncludesSubgraphOperator()
        {
            var ir = s_ir;
            var sub = ir.SubgraphOperators.FirstOrDefault(
                o => o.ModelType == typeof(UnityEditor.VFX.VFXSubgraphOperator));
            Assert.NotNull(sub, "VFXSubgraphOperator must appear in ir.SubgraphOperators.");
        }

        [Test]
        public void Walk_Subgraphs_IncludesSubgraphBlock()
        {
            var ir = s_ir;
            var sub = ir.SubgraphBlocks.FirstOrDefault(
                b => b.ModelType == typeof(UnityEditor.VFX.VFXSubgraphBlock));
            Assert.NotNull(sub, "VFXSubgraphBlock must appear in ir.SubgraphBlocks.");
        }

        // ─────────── Phase 3 task 3A-1 — settings + slot bindings ───────────

        [Test]
        public void Walk_Operator_CapturesSettings()
        {
            // VFXInlineOperator carries an m_Type setting (a SerializableType wrapper).
            // ERRATUM P-B2: walker must unwrap SerializableType via implicit operator.
            // ERRATUM P-H2: raw field name "m_Type" must be stored on SettingDescriptor.Name.
            var ir = s_ir;
            var inline = ir.Operators.FirstOrDefault(o => o.TypeFQN.EndsWith(".VFXInlineOperator"));
            Assert.NotNull(inline, "VFXInlineOperator must exist (gap #15).");
            Assert.IsNotEmpty(inline.Settings,
                "VFXInlineOperator must have at least one setting (m_Type).");
            Assert.IsTrue(inline.Settings.Any(s => s.Name == "m_Type"),
                "m_Type setting must be captured with its raw field name (erratum P-H2).");
        }

        [Test]
        public void Walk_Operator_CapturesInputSlots()
        {
            // The Add operator has at least one input slot in VFX Graph 17.4.
            var ir = s_ir;
            var add = ir.Operators.FirstOrDefault(o => o.TypeFQN == "UnityEditor.VFX.Operator.Add");
            Assert.NotNull(add, "Add operator must be in the catalog.");
            Assert.IsNotEmpty(add.InputSlots,
                "Add must have at least one captured input slot.");
        }
    }
}
