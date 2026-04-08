// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxBusyGate.cs
//
// Phase 3 Lane C, task 3C-1. Throws VfxBusyException when the editor is not
// in a state where mutating asset writes are safe (compiling or running an
// asset import worker).
//
// Phase 5: extracted the "is busy?" predicate into an injectable delegate so
// the test suite can synthesise a busy state without spawning a real compile
// or import worker process. The default delegate reads the live Unity flags;
// tests override _isCompilingOverride / _isImportWorkerOverride.

using System;
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxBusyGate : IVfxBusyGate
    {
        // Test seams: when non-null these replace the live Unity predicates.
        // Production code must never set these — only tests may override them.
        internal Func<bool> _isCompilingOverride;
        internal Func<bool> _isImportWorkerOverride;

        private bool IsCompiling()
            => _isCompilingOverride != null
                ? _isCompilingOverride()
                : EditorApplication.isCompiling;

        private bool IsImportWorker()
            => _isImportWorkerOverride != null
                ? _isImportWorkerOverride()
                : AssetDatabase.IsAssetImportWorkerProcess();

        public void EnsureIdle()
        {
            if (IsCompiling())
            {
                throw new VfxBusyException(
                    "asset_pipeline_busy",
                    "Unity is compiling; retry after compile finishes.",
                    1500);
            }

            if (IsImportWorker())
            {
                throw new VfxBusyException(
                    "asset_pipeline_busy",
                    "Asset import worker process is active; retry in 1-2s.",
                    1500);
            }
        }
    }
}
