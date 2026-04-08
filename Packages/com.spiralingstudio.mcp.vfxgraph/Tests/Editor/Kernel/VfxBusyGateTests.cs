// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxBusyGateTests.cs
//
// Phase 3 Lane C, task 3C-1.

using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxBusyGateTests
    {
        [Test]
        public void EnsureIdle_WhenIdle_DoesNotThrow()
        {
            // Test runs may overlap a domain reload in CI; if Unity is still
            // compiling or the asset import worker is busy, the gate's
            // contract is to throw. Both outcomes are valid — the gate must
            // never spuriously throw when the editor is genuinely idle.
            if (EditorApplication.isCompiling || AssetDatabase.IsAssetImportWorkerProcess())
            {
                Assert.Throws<VfxBusyException>(() => new VfxBusyGate().EnsureIdle(),
                    "When the editor is busy, EnsureIdle must throw VfxBusyException.");
                return;
            }

            var gate = new VfxBusyGate();
            Assert.DoesNotThrow(() => gate.EnsureIdle());
        }

        // TODO(phase 5): Deterministically forcing EditorApplication.isCompiling
        // / AssetDatabase.IsAssetImportWorkerProcess() in a unit test requires
        // a fixture that triggers a compile or spawns an import worker.
        // Coverage of the throw branch in isolation is deferred to phase 5
        // (perf + busy tests).
        [Test]
        [Ignore("TODO(phase 5): requires triggering compile / asset import worker")]
        public void EnsureIdle_WhenCompiling_ThrowsBusy()
        {
            // Placeholder. Phase 5 will craft a fixture that flips
            // EditorApplication.isCompiling and asserts the throw.
        }
    }
}
