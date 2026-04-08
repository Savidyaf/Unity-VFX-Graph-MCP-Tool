// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxBusyGateTests.cs
//
// Phase 3 Lane C, task 3C-1.
// Phase 5: un-ignore the throw-case test via the delegate seam added to
// VfxBusyGate. The seam replaces the live Unity predicates with lambdas
// the test controls, so no real compile or import worker is needed.

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

        [Test]
        public void EnsureIdle_WhenCompiling_ThrowsBusy()
        {
            // Phase 5 approach: inject a delegate seam that returns true for
            // isCompiling so we don't need a real domain reload.
            // VfxBusyGate._isCompilingOverride and _isImportWorkerOverride are
            // internal fields set only by tests; production code never touches them.
            var gate = new VfxBusyGate
            {
                _isCompilingOverride = () => true,
                _isImportWorkerOverride = () => false,
            };

            var ex = Assert.Throws<VfxBusyException>(() => gate.EnsureIdle(),
                "EnsureIdle must throw VfxBusyException when isCompiling returns true.");

            Assert.AreEqual("asset_pipeline_busy", ex.Code,
                "VfxBusyException.Code must be 'asset_pipeline_busy'.");
            Assert.IsTrue(ex.RetryAfterHintMs > 0,
                "VfxBusyException.RetryAfterHintMs must be positive.");
        }

        [Test]
        public void EnsureIdle_WhenImportWorkerActive_ThrowsBusy()
        {
            // Same seam approach: simulate the import worker being active.
            var gate = new VfxBusyGate
            {
                _isCompilingOverride = () => false,
                _isImportWorkerOverride = () => true,
            };

            var ex = Assert.Throws<VfxBusyException>(() => gate.EnsureIdle(),
                "EnsureIdle must throw VfxBusyException when isImportWorker returns true.");

            Assert.AreEqual("asset_pipeline_busy", ex.Code);
        }

        [Test]
        public void EnsureIdle_WhenOverrideIdle_DoesNotThrow()
        {
            // Both predicates return false via override — must not throw even
            // if the real Unity state is uncertain.
            var gate = new VfxBusyGate
            {
                _isCompilingOverride = () => false,
                _isImportWorkerOverride = () => false,
            };

            Assert.DoesNotThrow(() => gate.EnsureIdle(),
                "EnsureIdle must not throw when both predicates return false.");
        }
    }
}
