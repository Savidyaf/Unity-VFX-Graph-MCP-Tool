// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs
//
// Phase 3D-3 — IVfxNodeOps STUB.
//
// This is the ONLY layer in the kernel that touches Unity's VFX graph
// mutation APIs. Phase 3 commits this as a stub so the kernel container
// has a concrete to bind to; phase 4 fills in each method body when the
// matching tool class lands. The exception is DiscardChanges, which has
// a real body because it does not depend on any tool-level helpers.

using System;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxNodeOps : IVfxNodeOps
    {
        public string AddOperator(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public string AddContext(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public string AddBlock(string graphAssetPath, string parentContextToken, string typeFqn, int index)
            => throw new NotImplementedException("Phase 4 — VfxBlockTool");

        public string AddParameter(string graphAssetPath, string typeFqn, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, Vector2 pos)
            => throw new NotImplementedException("Phase 4 — VfxSubgraphTool");

        public void RemoveNode(string graphAssetPath, string token)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void Connect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void Disconnect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void SetSetting(string graphAssetPath, string token, string name, object value)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public void SetProperty(string graphAssetPath, string token, string name, object value)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public object GetSetting(string graphAssetPath, string token, string name)
            => throw new NotImplementedException("Phase 4 — VfxNodeTool");

        public object GetProperty(string graphAssetPath, string token, string name)
            => throw new NotImplementedException("Phase 4 — VfxPropertyTool");

        public void DiscardChanges(string graphAssetPath)
        {
            // Force a re-import of the asset from disk, discarding any in-memory
            // mutations to the underlying VisualEffectAsset / VFXGraph. This is
            // safe to call before phase 4 lands the tool layer because it only
            // touches AssetDatabase, which is always available in the editor.
            UnityEditor.AssetDatabase.ImportAsset(
                graphAssetPath,
                UnityEditor.ImportAssetOptions.ForceUpdate);
            // VFXViewController re-fetch happens naturally via
            // GetController(..., forceUpdate: true) on the next access.
        }
    }
}
