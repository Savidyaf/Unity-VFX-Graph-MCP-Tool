// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxBusyGate.cs
//
// Phase 3 Lane C, task 3C-1. Throws VfxBusyException when the editor is not
// in a state where mutating asset writes are safe (compiling or running an
// asset import worker).

using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxBusyGate : IVfxBusyGate
    {
        public void EnsureIdle()
        {
            if (EditorApplication.isCompiling)
            {
                throw new VfxBusyException(
                    "asset_pipeline_busy",
                    "Unity is compiling; retry after compile finishes.",
                    1500);
            }

            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                throw new VfxBusyException(
                    "asset_pipeline_busy",
                    "Asset import worker process is active; retry in 1-2s.",
                    1500);
            }
        }
    }
}
