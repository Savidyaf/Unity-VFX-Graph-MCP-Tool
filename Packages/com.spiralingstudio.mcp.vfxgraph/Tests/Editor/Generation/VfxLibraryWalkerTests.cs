// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxLibraryWalkerTests.cs
// Phase 2 task 7: failing-first test for the operator-only walker baseline.
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxLibraryWalkerTests
    {
        [Test]
        public void Walk_Operators_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            // After task 8 the walker splits VFXSubgraphOperator out of ir.Operators,
            // so we add the subgraph count back in to compare with the raw VFXLibrary count.
            int raw = VFXLibrary.GetOperators().Count();
            Assert.AreEqual(raw, ir.Operators.Count + ir.SubgraphOperators.Count,
                "ir.Operators + ir.SubgraphOperators should equal VFXLibrary.GetOperators().");
        }

        [Test]
        public void Walk_Operators_EachHasFQN()
        {
            var ir = new VfxLibraryWalker().Walk();
            foreach (var op in ir.Operators)
            {
                Assert.IsNotEmpty(op.TypeFQN, $"Operator with short name {op.ShortName} has no FQN.");
                Assert.IsNotEmpty(op.ShortName, $"Operator with FQN {op.TypeFQN} has no short name.");
            }
        }

        [Test]
        public void Walk_KnownOperator_LerpIsPresent()
        {
            var ir = new VfxLibraryWalker().Walk();
            var lerp = ir.Operators.FirstOrDefault(o => o.TypeFQN == "UnityEditor.VFX.Operator.Lerp");
            Assert.NotNull(lerp, "Lerp operator must be in the catalog (it was gap #9 in the original tool).");
        }

        // ─────────── Phase 2 task 8 — extended walker coverage ───────────

        [Test]
        public void Walk_Blocks_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            // The walker pulls VFXSubgraphBlock out of ir.Blocks, so we add the
            // subgraph-block count back in to compare with the raw VFXLibrary count.
            int raw = VFXLibrary.GetBlocks().Count();
            Assert.AreEqual(raw, ir.Blocks.Count + ir.SubgraphBlocks.Count,
                "ir.Blocks + ir.SubgraphBlocks should equal VFXLibrary.GetBlocks().");
        }

        [Test]
        public void Walk_Contexts_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            int raw = VFXLibrary.GetContexts().Count();
            Assert.AreEqual(raw, ir.Contexts.Count + ir.SubgraphContexts.Count,
                "ir.Contexts + ir.SubgraphContexts should equal VFXLibrary.GetContexts().");
        }

        [Test]
        public void Walk_Parameters_MatchesVFXLibraryCount()
        {
            var ir = new VfxLibraryWalker().Walk();
            Assert.AreEqual(VFXLibrary.GetParameters().Count(), ir.Parameters.Count);
        }

        [Test]
        public void Walk_Operators_ExcludesSubgraphOperator()
        {
            var ir = new VfxLibraryWalker().Walk();
            // After the typeof()-based split, VFXSubgraphOperator must NOT
            // appear in ir.Operators (it lives in ir.SubgraphOperators instead).
            Assert.IsFalse(ir.Operators.Any(o => o.ModelType == typeof(UnityEditor.VFX.VFXSubgraphOperator)),
                "VFXSubgraphOperator should be split out into ir.SubgraphOperators.");
        }

        [Test]
        public void Walk_Subgraphs_IncludesSubgraphOperator()
        {
            var ir = new VfxLibraryWalker().Walk();
            var sub = ir.SubgraphOperators.FirstOrDefault(
                o => o.ModelType == typeof(UnityEditor.VFX.VFXSubgraphOperator));
            Assert.NotNull(sub, "VFXSubgraphOperator must appear in ir.SubgraphOperators.");
        }

        [Test]
        public void Walk_Subgraphs_IncludesSubgraphBlock()
        {
            var ir = new VfxLibraryWalker().Walk();
            var sub = ir.SubgraphBlocks.FirstOrDefault(
                b => b.ModelType == typeof(UnityEditor.VFX.VFXSubgraphBlock));
            Assert.NotNull(sub, "VFXSubgraphBlock must appear in ir.SubgraphBlocks.");
        }
    }
}
