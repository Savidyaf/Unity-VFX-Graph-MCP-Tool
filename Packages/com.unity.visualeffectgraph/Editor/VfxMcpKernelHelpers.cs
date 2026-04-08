// Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs
//
// VFX MCP addon soft-fork patch (Phase 3 Lane C task 3C-2 enabler).
//
// The InternalsVisibleTo patch in AssemblyInfo.cs grants the addon assembly
// access to UnityEditor.VFX.* types defined in Unity.VisualEffectGraph.Editor.
// However, the extension methods VisualEffectObjectExtensions.GetResource and
// VisualEffectResourceExtensions.GetOrCreateGraph both return
// UnityEditor.VFX.VisualEffectResource, which lives in the Unity built-in
// editor module UnityEditor.VFXModule. Built-in modules cannot receive
// InternalsVisibleTo grants from user packages, so the addon assembly cannot
// see VisualEffectResource — and Roslyn rejects any method that returns it
// with CS0122 ("inaccessible due to its protection level").
//
// This helper file is compiled INTO Unity.VisualEffectGraph.Editor and
// therefore has full access to VisualEffectResource. It exposes a single
// public-static bridge that takes a VisualEffectAsset (public type) and
// returns a VFXGraph (which IS reachable from the addon assembly via
// InternalsVisibleTo). The kernel's VfxCompileGate calls this helper instead
// of chaining the inaccessible extension methods directly.
//
// This is NOT runtime reflection — it is a compile-time bridge. The phase 5
// no-reflection static analyzer is unaffected.

using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    /// <summary>
    /// Internal-static bridges that expose VFX Graph operations to the
    /// VFX MCP addon assembly. Marked internal so the methods may take and
    /// return VFX Graph internal types like VFXGraph; the addon assembly
    /// reaches them via the InternalsVisibleTo grant in
    /// AssemblyInfo.cs.
    /// </summary>
    internal static class VfxMcpKernelHelpers
    {
        /// <summary>
        /// Loads (or creates) the editor-side VFXGraph for a VisualEffectAsset.
        /// Mirrors the path Unity's own importer uses
        /// (asset.GetResource().GetOrCreateGraph()), without exposing the
        /// inaccessible VisualEffectResource type to the caller.
        /// </summary>
        internal static VFXGraph LoadGraphFromAsset(VisualEffectAsset asset)
        {
            if (asset == null) return null;
            var resource = asset.GetResource();
            if (resource == null) return null;
            return resource.GetOrCreateGraph();
        }

        /// <summary>
        /// Compiles the given VFXGraph and writes the resulting subassets
        /// onto the supplied VisualEffectAsset. Wraps
        /// <see cref="VFXGraph.CompileAndUpdateAsset(VisualEffectAsset)"/>,
        /// which is internal but accessible inside this assembly.
        /// </summary>
        internal static void CompileAndUpdateAsset(VFXGraph graph, VisualEffectAsset asset)
        {
            if (graph == null || asset == null) return;
            graph.CompileAndUpdateAsset(asset);
        }

        /// <summary>
        /// Forces the VFXErrorManager.compileReporter to be (re)initialized
        /// before a compile pass. Mirrors the importer pipeline at
        /// VFXGraph.cs:137 (graph.errorManager.RefreshCompilationReport()).
        /// </summary>
        internal static void RefreshCompilationReport(VFXGraph graph)
        {
            if (graph == null) return;
            graph.errorManager?.RefreshCompilationReport();
        }
    }
}
