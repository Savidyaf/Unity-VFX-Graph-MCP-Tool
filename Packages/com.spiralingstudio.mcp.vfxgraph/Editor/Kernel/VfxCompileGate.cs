// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxCompileGate.cs
//
// Phase 3 Lane C, task 3C-2. Wraps VFXGraph.CompileAndUpdateAsset and surfaces
// any compile errors found via the IVFXErrorReporter compileReporter.
//
// Per erratum P-B1 (verified against
//   Packages/com.unity.visualeffectgraph/Editor/Models/VFXGraph.cs:1464
//     internal UnityObject[] CompileAndUpdateAsset(VisualEffectAsset asset)
// ), the call MUST pass the VisualEffectAsset argument.
//
// API ACCESS NOTE
// ───────────────
// VisualEffectResource is defined in Unity's built-in editor module
// UnityEditor.VFXModule, which cannot receive InternalsVisibleTo grants from
// user packages. The Phase 1 InternalsVisibleTo patch on
// com.unity.visualeffectgraph/Editor/AssemblyInfo.cs only grants access to
// the Unity.VisualEffectGraph.Editor assembly. As a result, the addon
// assembly cannot directly call asset.GetResource() — Roslyn rejects every
// extension method that returns VisualEffectResource with CS0122
// ("inaccessible due to its protection level").
//
// To preserve the plan's intended call path (graph.CompileAndUpdateAsset(asset))
// without violating the reflection rule clarification at the top of the plan
// ERRATA, this lane introduces a sibling soft-fork patch file at
//   Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs
// which compiles INTO Unity.VisualEffectGraph.Editor and exposes a
// public-static bridge (VfxMcpKernelHelpers.LoadGraphFromAsset →
// VfxMcpKernelHelpers.CompileAndUpdateAsset). The bridge is compile-time
// only — the kernel still contains zero runtime reflection on
// UnityEditor.VFX.* / UnityEngine.VFX.* types.

using System.Diagnostics;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxCompileGate : IVfxCompileGate
    {
        public VfxCompileResult Compile(string graphAssetPath)
        {
            var sw = Stopwatch.StartNew();
            var result = new VfxCompileResult();

            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
            {
                sw.Stop();
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                result.Ok = false;
                result.Errors.Add(new VfxCompileError
                {
                    ErrorId = "asset_not_found",
                    Description = $"Could not load VisualEffectAsset at '{graphAssetPath}'.",
                    Severity = "Error",
                });
                return result;
            }

            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
            {
                sw.Stop();
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                result.Ok = false;
                result.Errors.Add(new VfxCompileError
                {
                    ModelTypeFqn = asset.GetType().FullName,
                    ErrorId = "graph_unavailable",
                    Description = $"Could not load VFXGraph for asset '{graphAssetPath}'.",
                    Severity = "Error",
                });
                return result;
            }

            // VFXErrorManager.compileReporter is lazy: null until
            // RefreshCompilationReport initializes a fresh VFXErrorReporter.
            // Unity's importer at VFXGraph.cs:137 follows the same pattern
            // before driving CompileForImport.
            VfxMcpKernelHelpers.RefreshCompilationReport(graph);

            // ERRATUM P-B1: pass the VisualEffectAsset argument.
            VfxMcpKernelHelpers.CompileAndUpdateAsset(graph, asset);

            // graph.errorManager.compileReporter IS reachable from the addon
            // assembly because IVFXErrorReporter, ReportError, and VFXModel
            // all live in Unity.VisualEffectGraph.Editor (the InternalsVisibleTo
            // target).
            var reporter = graph.errorManager?.compileReporter;
            if (reporter != null)
            {
                foreach (var model in reporter.dirtyModels)
                {
                    foreach (var error in reporter.GetDirtyModelErrors(model))
                    {
                        result.Errors.Add(new VfxCompileError
                        {
                            ModelTypeFqn = model != null ? model.GetType().FullName : null,
                            ErrorId = error.error,
                            Description = error.description,
                            Severity = error.type.ToString(),
                        });
                    }
                }
            }

            sw.Stop();
            result.DurationMs = (int)sw.ElapsedMilliseconds;
            // Ok if no Error-severity entries were collected. Warnings tolerated.
            result.Ok = !HasError(result);
            return result;
        }

        private static bool HasError(VfxCompileResult r)
        {
            for (int i = 0; i < r.Errors.Count; i++)
            {
                // VFXErrorType.Error => "Error" string. Warnings ("Warning"/
                // "PerfWarning") do not flip Ok to false.
                if (string.Equals(r.Errors[i].Severity, "Error", System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
