// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxCatalogGenerator.cs
// Phase 2 task 10 + Phase 3 lane 3A wiring: orchestrator + editor menu item.
//
// Writes Editor/Generated/*.g.cs and a .catalog-version sentinel. Performs a
// startup sanity check that warns when the installed VFX Graph package
// version drifts from the generated catalog's recorded version.
//
// Phase 3 wiring runs every emitter in the lane:
//
//   VfxCatalog.g.cs           — name resolution table + diagnostic arrays
//   VfxCoercers.g.cs          — JToken -> typed slot value coercers
//   VfxNodeWrappers.g.cs      — operator/block/context/parameter factories
//   VfxSubgraphWrappers.g.cs  — subgraph reference m_Subgraph binder
//   VfxToolSchemas.g.cs       — per-tool action enum strings
//   VfxOverrides.g.cs         — phase-3 stub; phase 5 fills from Quirks/Hints YAML
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal static class VfxCatalogGenerator
    {
        private const string GeneratedDir =
            "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generated";
        private const string SentinelPath = GeneratedDir + "/.catalog-version";

        [MenuItem("Tools/VFX MCP/Regenerate Catalog", priority = 200)]
        public static void Regenerate()
        {
            // Sanity check the InternalsVisibleTo patch before doing anything.
            if (!VFXLibrary.GetOperators().Any())
            {
                EditorUtility.DisplayDialog(
                    "VFX MCP: InternalsVisibleTo patch missing",
                    "VFXLibrary.GetOperators() returned 0 items. The patch at\n" +
                    "  Packages/com.unity.visualeffectgraph/Editor/AssemblyInfo.cs\n" +
                    "may be missing or stale. Re-apply from the spec and retry.",
                    "OK");
                return;
            }

            Directory.CreateDirectory(GeneratedDir);

            // Walk catalog (including settings + slot bindings via templates)
            // and feed the live templates into the slot tree builder so the
            // emitter has the full compound-type decomposition. The templates
            // must be destroyed after the slot tree pass — they're transient
            // ScriptableObjects.
            var walker = new VfxLibraryWalker();
            var (ir, templates) = walker.WalkWithTemplates();
            try
            {
                new VfxSlotTreeBuilder().BuildFromTemplates(ir, templates);

                File.WriteAllText(Path.Combine(GeneratedDir, "VfxCatalog.g.cs"),
                    new CatalogEmitter().Emit(ir));
                File.WriteAllText(Path.Combine(GeneratedDir, "VfxCoercers.g.cs"),
                    new CoercersEmitter().Emit(ir));
                File.WriteAllText(Path.Combine(GeneratedDir, "VfxNodeWrappers.g.cs"),
                    new WrappersEmitter().Emit(ir));
                File.WriteAllText(Path.Combine(GeneratedDir, "VfxSubgraphWrappers.g.cs"),
                    new SubgraphWrappersEmitter().Emit(ir));
                File.WriteAllText(Path.Combine(GeneratedDir, "VfxToolSchemas.g.cs"),
                    new SchemasEmitter().Emit(ir));
                File.WriteAllText(Path.Combine(GeneratedDir, "VfxOverrides.g.cs"),
                    new OverridesEmitter().Emit(ir));
            }
            finally
            {
                foreach (var t in templates)
                    if (t != null) Object.DestroyImmediate(t);
            }

            // Write sentinel — the recorded live package version so the
            // InitializeOnLoad staleness check can warn on drift.
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.unity.visualeffectgraph/package.json");
            string version = pkgInfo?.version ?? "unknown";
            File.WriteAllText(SentinelPath, version);

            // ERRATUM P-M6: refresh the asset DB so Unity creates .meta files
            // for any newly written .g.cs files before the user commits.
            AssetDatabase.Refresh();
            Debug.Log($"[VFX MCP] Catalog regenerated: " +
                $"{ir.Operators.Count} ops, {ir.Blocks.Count} blocks, " +
                $"{ir.Contexts.Count} contexts, {ir.Parameters.Count} params, " +
                $"{ir.SubgraphOperators.Count + ir.SubgraphBlocks.Count + ir.SubgraphContexts.Count} subgraphs, " +
                $"{ir.SlotTypes.Count} slot types. " +
                $"Package version: {version}");
        }

        [InitializeOnLoadMethod]
        private static void CheckStalenessOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SentinelPath)) return;
                var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/com.unity.visualeffectgraph/package.json");
                if (pkgInfo == null) return;
                string recorded = File.ReadAllText(SentinelPath).Trim();
                if (recorded != pkgInfo.version)
                {
                    Debug.LogWarning(
                        $"[VFX MCP] Catalog was generated for package version '{recorded}' " +
                        $"but live version is '{pkgInfo.version}'. Run " +
                        "Tools/VFX MCP/Regenerate Catalog to refresh.");
                }
            };
        }
    }
}
