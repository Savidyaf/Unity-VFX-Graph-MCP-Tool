// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxGraphTool.cs
//
// Phase 4 Lane A — VFX graph-level settings and meta-operations.
//
// Actions: get_info, save, compile, compilation_status, read_console,
//          set_space, set_capacity, set_bounds, set_data_settings,
//          discard_changes, get_health.
//
// Mutating actions gate on VfxBusyGate and open a VfxTransaction.
// Read-only actions skip both.

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_graph", AutoRegister = true, Group = "vfx")]
    public static class VfxGraphTool
    {
        // Ring-buffer mark for read_console — persists between calls.
        private static object s_lastReadMark = VfxConsoleReader.GetHighWaterMark();

        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "get_info"           => GetInfo(@params, verbose),
                    "save"               => Save(@params, verbose),
                    "compile"            => Compile(@params, verbose),
                    "compilation_status" => CompilationStatus(@params, verbose),
                    "read_console"       => ReadConsole(@params, verbose),
                    "set_space"          => SetSpace(@params, verbose),
                    "set_capacity"       => SetCapacity(@params, verbose),
                    "set_bounds"         => SetBounds(@params, verbose),
                    "set_data_settings"  => SetDataSettings(@params, verbose),
                    "discard_changes"    => DiscardChanges(@params, verbose),
                    "get_health"         => GetHealth(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code = "unknown_action",
                        Message = $"Unknown vfx_graph action '{action}'",
                        Hint = "Valid: get_info, save, compile, compilation_status, read_console, " +
                               "set_space, set_capacity, set_bounds, set_data_settings, discard_changes, get_health",
                    }),
                };
            }
            catch (VfxException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                });
            }
            catch (System.Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        // ── ApplyInTransaction stub — lane C's batch dispatcher finds this via reflection ──
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            throw new System.NotImplementedException(
                "VfxGraphTool.ApplyInTransaction will be wired in phase 4C batch integration");
        }

        // ── per-action private helpers ──

        // READ-ONLY ─────────────────────────────────────────────────────────

        private static object GetInfo(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"VisualEffectAsset not found at '{path}'.", null);

            // Use soft-fork bridge to load VFXGraph — zero reflection on UnityEditor.VFX.* types.
            UnityEditor.VFX.VFXGraph graph = UnityEditor.VFX.VfxMcpKernelHelpers.LoadGraphFromAsset(asset);

            int childCount = 0;
            var systemNamesArr = new JArray();
            var distinctSystemNames = new System.Collections.Generic.HashSet<string>();

            if (graph != null)
            {
                // VFXGraph.children is a public IEnumerable<VFXModel>; we count without
                // accessing internal members.
                foreach (var child in graph.children)
                {
                    childCount++;
                    // F3 (v0.3.1): collect distinct system names by walking top-level
                    // VFXContexts. VFXSystemNames.GetSystemName is a public static method
                    // that resolves a context's system name via its parent VFXData.
                    if (child is UnityEditor.VFX.VFXContext ctx)
                    {
                        string sysName = UnityEditor.VFX.VFXSystemNames.GetSystemName(ctx);
                        if (!string.IsNullOrEmpty(sysName))
                            distinctSystemNames.Add(sysName);
                    }
                }
                foreach (var n in distinctSystemNames) systemNamesArr.Add(n);
            }

            var info = new JObject
            {
                ["graph_path"]          = path,
                ["child_count"]         = childCount,
                ["system_count"]        = distinctSystemNames.Count,
                ["system_names"]        = systemNamesArr,
                // F3 stubs (v0.3.1) — these accessors don't exist on VFXGraph in Unity 6000.4:
                ["space"]               = "",  // VFXCoordinateSpace lives on VFXContext, not VFXGraph
                ["bounds_setting_mode"] = "",  // BoundsSettingMode is per-system on VFXDataParticle
                ["update_mode"]         = "",  // No update mode property exists on VFXGraph
            };

            return VfxKernelContainer.Shaper.ShapeRead(info, verbose);
        }

        private static object CompilationStatus(JObject @params, bool verbose)
        {
            // F4 (v0.3.1): honest not_implemented stub. A standalone read of the
            // last compile status without running a compile is not in v0.3.1's
            // scope — clients should call the 'compile' action instead.
            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["state"] = "not_implemented",
                ["hint"]  = "Use the 'compile' action to trigger a compile and read its result.",
            }, verbose);
        }

        private static object ReadConsole(JObject @params, bool verbose)
        {
            // ERRATUM P-M4: ring-buffer read via high-water-mark approach.
            var lines = VfxConsoleReader.GetLinesSince(s_lastReadMark);
            s_lastReadMark = VfxConsoleReader.GetHighWaterMark();
            return new JObject { ["lines"] = new JArray(lines) };
        }

        private static object GetHealth(JObject @params, bool verbose)
        {
            // F4 (v0.3.1): honest not_implemented stub. Real health is computed
            // inside VfxTransaction.Commit() (three-part gate). A standalone query
            // outside a transaction cannot run the full gate without performing a
            // write, so we surface this as not_implemented rather than fake data.
            return new JObject
            {
                ["state"] = "not_implemented",
                ["hint"]  = "Health is computed per-transaction. Run a save/compile/mutate to get a real report.",
            };
        }

        // MUTATING ──────────────────────────────────────────────────────────

        private static object Save(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.Save))
            {
                // Save doesn't record ops; the YAML verifier sees the current on-disk
                // state match an empty intent list (no-op commit).
                VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (asset != null)
                    AssetDatabase.SaveAssetIfDirty(asset);

                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                return new JObject { ["saved"] = path };
            }
        }

        private static object Compile(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            var result = VfxKernelContainer.CompileGate.Compile(path);
            return VfxKernelContainer.Shaper.ShapeRead(JObject.FromObject(result), verbose);
        }

        private static object SetSpace(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            string value = @params.Value<string>("value")
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetSetting(path, "@graph", "space", value);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        private static object SetCapacity(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetSetting(path, "@graph", "capacity", value);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        private static object SetBounds(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetSetting(path, "@graph", "boundsSettingMode", value);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        private static object SetDataSettings(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            string name = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);

            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetSetting(path, "@graph", name, value);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        private static object DiscardChanges(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            // VfxNodeOps.DiscardChanges has a real body (re-imports the asset).
            VfxKernelContainer.NodeOps.DiscardChanges(path);

            return new JObject { ["discarded"] = path };
        }
    }
}
