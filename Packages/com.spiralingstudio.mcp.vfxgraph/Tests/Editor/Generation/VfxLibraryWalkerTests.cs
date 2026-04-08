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
            int expected = VFXLibrary.GetOperators().Count();
            Assert.AreEqual(expected, ir.Operators.Count,
                "Walker should produce exactly one NodeDescriptor per VFXLibrary operator.");
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
    }
}
