// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Generation/VfxSlotTreeBuilderTests.cs
// Phase 3 task 3A-2: drives VfxSlotTreeBuilder against the live walker
// templates per erratum P-B3 (no C# field reflection — descend live VFXSlot.children).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Generation;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpiralingStudio.VfxMcp.Generation.Tests
{
    public class VfxSlotTreeBuilderTests
    {
        // Walking the catalog instantiates models that emit Unity native
        // asserts (Texture3D ExtensionOfNativeClass). Walk once at the suite
        // level so we only suppress those once.
        private static CatalogIR s_ir;
        private static List<UnityEditor.VFX.VFXModel> s_templates;

        [OneTimeSetUp]
        public void WalkOnce()
        {
            LogAssert.ignoreFailingMessages = true;
            (s_ir, s_templates) = new VfxLibraryWalker().WalkWithTemplates();
            new VfxSlotTreeBuilder().BuildFromTemplates(s_ir, s_templates);
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
        public void Build_Vector3_HasThreeChildren()
        {
            // Vector3 must always be present in the catalog because dozens of
            // operators expose Vector3-typed slots. The decomposition is
            // produced by VFX Graph itself via VFXSlot.CreateSub.
            var vec = s_ir.SlotTypes.FirstOrDefault(t => t.TypeFQN == "UnityEngine.Vector3");
            Assert.NotNull(vec, "Vector3 slot type must be present after walking the catalog.");
            Assert.IsTrue(vec.IsCompound, "Vector3 must be tagged compound.");
            Assert.AreEqual(3, vec.Children.Count, "Vector3 must decompose into 3 children.");
            Assert.IsTrue(vec.Children.Any(c => c.Name == "x"), "Vector3 must have an 'x' child.");
            Assert.IsTrue(vec.Children.Any(c => c.Name == "y"), "Vector3 must have a 'y' child.");
            Assert.IsTrue(vec.Children.Any(c => c.Name == "z"), "Vector3 must have a 'z' child.");
            Assert.IsTrue(vec.Children.All(c => c.ChildTypeFQN == "System.Single"),
                "Vector3 children should all be float (System.Single).");
        }

        [Test]
        public void Build_Float_IsLeaf()
        {
            // float must be a leaf — most operators expose float-typed slots.
            var f = s_ir.SlotTypes.FirstOrDefault(t => t.TypeFQN == "System.Single");
            Assert.NotNull(f, "float slot type must be present after walking the catalog.");
            Assert.IsFalse(f.IsCompound, "float must be a leaf, not compound.");
            Assert.AreEqual(0, f.Children.Count);
        }

        [Test]
        public void Build_Color_HasFourChildren()
        {
            // UnityEngine.Color decomposes into r/g/b/a — used by Set Color and many other ops.
            var color = s_ir.SlotTypes.FirstOrDefault(t => t.TypeFQN == "UnityEngine.Color");
            Assert.NotNull(color, "Color slot type must be present after walking the catalog.");
            Assert.IsTrue(color.IsCompound, "Color must be tagged compound.");
            Assert.AreEqual(4, color.Children.Count, "Color must decompose into r,g,b,a.");
            Assert.IsTrue(color.Children.Any(c => c.Name == "r"));
            Assert.IsTrue(color.Children.Any(c => c.Name == "g"));
            Assert.IsTrue(color.Children.Any(c => c.Name == "b"));
            Assert.IsTrue(color.Children.Any(c => c.Name == "a"));
        }
    }
}
