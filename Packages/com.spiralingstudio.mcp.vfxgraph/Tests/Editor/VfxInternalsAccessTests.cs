using System.Linq;
using NUnit.Framework;
using UnityEditor.VFX;

namespace MCPForUnity.Editor.Tools.Vfx.Tests
{
    /// <summary>
    /// Phase 1 task 5 smoke test: proves the InternalsVisibleTo patch at
    ///   Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs
    /// is in place and the addon's editor assembly can reach VFXLibrary.
    ///
    /// If any of these tests fail with TypeInitializationException,
    /// MethodAccessException, or a count of 0, the patch is missing or
    /// the addon asmdef is missing its Unity.VisualEffectGraph.Editor reference.
    /// </summary>
    public class VfxInternalsAccessTests
    {
        [Test]
        public void VFXLibrary_Operators_ReturnsNonEmpty()
        {
            int count = VFXLibrary.GetOperators().Count();
            Assert.Greater(count, 0,
                "VFXLibrary.GetOperators() is empty — InternalsVisibleTo patch at " +
                "Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs may be missing " +
                "or the addon asmdef lacks the Unity.VisualEffectGraph.Editor reference.");
        }

        [Test]
        public void VFXLibrary_Blocks_ReturnsNonEmpty()
        {
            int count = VFXLibrary.GetBlocks().Count();
            Assert.Greater(count, 0, "VFXLibrary.GetBlocks() is empty.");
        }

        [Test]
        public void VFXLibrary_Contexts_ReturnsNonEmpty()
        {
            int count = VFXLibrary.GetContexts().Count();
            Assert.Greater(count, 0, "VFXLibrary.GetContexts() is empty.");
        }

        [Test]
        public void VFXLibrary_Parameters_ReturnsNonEmpty()
        {
            int count = VFXLibrary.GetParameters().Count();
            Assert.Greater(count, 0, "VFXLibrary.GetParameters() is empty.");
        }
    }
}
