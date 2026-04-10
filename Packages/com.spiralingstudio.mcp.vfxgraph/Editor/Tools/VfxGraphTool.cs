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

using System.Collections.Generic;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_graph", AutoRegister = true, Group = "vfx")]
    public static class VfxGraphTool
    {
        // Ring-buffer mark for read_console — persists between calls.
        private static object s_lastReadMark = VfxConsoleReader.GetHighWaterMark();

        // W4-C Lift 2: cache the last VfxCompileResult per asset path so the
        // read-only CompilationStatus action can return a real payload without
        // re-running the compile. Populated by Compile() on every invocation.
        private static readonly Dictionary<string, VfxCompileResult> s_lastCompileByPath
            = new Dictionary<string, VfxCompileResult>();

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

        // ── ApplyInTransaction — F9 batch dispatch entry point ──
        // Note: compile and discard_changes do not open a transaction. The
        // set_space/set_capacity/set_bounds convenience wrappers (W4-C Lift 1)
        // each open their own single-call scope internally and are NOT wired
        // into batch dispatch — callers that want them inside a batch should
        // use set_data_settings or vfx_node.set_setting on explicit tokens.
        // Only save and set_data_settings are batch-transaction-backed.
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            string action = (opParams?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
            return action switch
            {
                "save"              => SaveInner(opParams, scope),
                "set_data_settings" => SetDataSettingsInner(opParams, scope),
                _ => throw new System.NotImplementedException(
                    $"vfx_graph action '{action}' not supported in batch"),
            };
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
            var distinctSpaces = new System.Collections.Generic.HashSet<string>();
            var distinctBoundsModes = new System.Collections.Generic.HashSet<string>();

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

                        // W4-C Lift 4: collect distinct coordinate spaces from the
                        // context.space property (delegates to ISpaceable data).
                        // VFXSpace is in UnityEngine.VFX (already imported above).
                        if (ctx.spaceable)
                        {
                            var s = ctx.space;
                            if (s != VFXSpace.None)
                                distinctSpaces.Add(s.ToString());
                        }

                        // W4-C Lift 4: collect distinct bounds modes from the owning
                        // VFXDataParticle (boundsMode is per-system, not per-graph).
                        var data = ctx.GetData() as UnityEditor.VFX.VFXDataParticle;
                        if (data != null)
                            distinctBoundsModes.Add(data.boundsMode.ToString());
                    }
                }
                foreach (var n in distinctSystemNames) systemNamesArr.Add(n);
            }

            // W4-C Lift 4: dominant-or-mixed rule. Return the single distinct
            // value when all contexts agree, or an empty string when mixed/none.
            string dominantSpace = distinctSpaces.Count == 1 ? FirstOf(distinctSpaces) : "";
            string dominantBoundsMode = distinctBoundsModes.Count == 1 ? FirstOf(distinctBoundsModes) : "";

            var info = new JObject
            {
                ["graph_path"]          = path,
                ["child_count"]         = childCount,
                ["system_count"]        = distinctSystemNames.Count,
                ["system_names"]        = systemNamesArr,
                // W4-C Lift 4 (v0.3.1):
                ["space"]               = dominantSpace,         // per-context, aggregated
                ["bounds_setting_mode"] = dominantBoundsMode,    // per-system on VFXDataParticle
                // update_mode does NOT exist on VFXGraph in Unity 6000.4. VFX Graph
                // has no graph-level update mode; each system has its own implicit
                // per-frame update via its VFXBasicUpdate context. We return a
                // documented constant here so clients don't have to special-case an
                // empty string; it is not read from Unity state.
                ["update_mode"]         = "per_system",
            };

            return VfxKernelContainer.Shaper.ShapeRead(info, verbose);
        }

        // Helper: tiny one-liner for FirstOf over a HashSet (no LINQ import needed).
        private static string FirstOf(System.Collections.Generic.HashSet<string> set)
        {
            foreach (var s in set) return s;
            return "";
        }

        private static object CompilationStatus(JObject @params, bool verbose)
        {
            // W4-C Lift 2 (v0.3.1): read the cached VfxCompileResult from the
            // most recent Compile() call for this graph path. This is a read-
            // only probe — it does NOT re-run the compile. Clients that want
            // a fresh compile should call the 'compile' action.
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            if (s_lastCompileByPath.TryGetValue(path, out var cached) && cached != null)
            {
                var body = new JObject
                {
                    ["Ok"]         = cached.Ok,
                    ["DurationMs"] = cached.DurationMs,
                    ["Errors"]     = cached.Errors != null ? JArray.FromObject(cached.Errors) : new JArray(),
                    ["cached"]     = true,
                };
                return VfxKernelContainer.Shaper.ShapeRead(body, verbose);
            }

            // No compile has run for this path in the current editor session.
            // "no_compile_yet" is a documented response state, not a stub.
            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["state"] = "no_compile_yet",
                ["hint"]  = "Run vfx_graph.compile to populate the cache, then retry.",
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
            // W4-C Lift 3 (v0.3.1): run an empty transaction (no recorded ops)
            // and return the commit's Health report. VfxTransaction.Commit()
            // always runs the three-part gate (YAML diff + compile + console)
            // against the current on-disk state, so an empty commit is a valid
            // "what's the current health?" probe. Force verbose=true on the
            // shaper so the health key is always included in the response.
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var commit = scope.Commit();
                // Always verbose for get_health — the whole point is the health
                // report, which the terse shaper omits.
                return VfxKernelContainer.Shaper.Shape(commit, true);
            }
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
                var payload = (JObject)SaveInner(@params, scope);
                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                return payload;
            }
        }

        internal static object SaveInner(JObject @params, VfxTransactionScope scope)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            // Save doesn't record ops; the YAML verifier sees the current on-disk
            // state match an empty intent list (no-op commit).
            VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset != null)
                AssetDatabase.SaveAssetIfDirty(asset);

            return new JObject { ["saved"] = path };
        }

        private static object Compile(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            var result = VfxKernelContainer.CompileGate.Compile(path);
            // W4-C Lift 2: cache the result so compilation_status can return it
            // without re-running the compile.
            if (result != null)
                s_lastCompileByPath[path] = result;
            return VfxKernelContainer.Shaper.ShapeRead(JObject.FromObject(result), verbose);
        }

        // W4-C Lift 1 helper: walk the graph and return (a) the list of all
        // VFXBasicInitialize contexts in the graph, and (b) the list of all
        // spaceable top-level contexts. Returns empty lists if the graph can't
        // be loaded.
        private static void CollectSystemContexts(
            string path,
            out List<VFXContext> initContexts,
            out List<VFXContext> spaceableContexts)
        {
            initContexts = new List<VFXContext>();
            spaceableContexts = new List<VFXContext>();

            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null) return;

            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null) return;

            foreach (var child in graph.children)
            {
                if (!(child is VFXContext ctx)) continue;
                if (ctx is VFXBasicInitialize) initContexts.Add(ctx);
                if (ctx.spaceable) spaceableContexts.Add(ctx);
            }
        }

        // W4-C Lift 1 helper: returns a needs_explicit_target envelope. Used
        // by the three convenience wrappers when they can't find a unique
        // target (zero or multiple candidates).
        private static object NeedsExplicitTarget(string key, string reason, bool verbose)
        {
            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["state"] = "needs_explicit_target",
                ["hint"]  = $"set_{key}: {reason} " +
                            "Call vfx_node.set_setting on a specific context/init token instead, " +
                            "or use vfx_graph.set_data_settings for graph-level settings.",
            }, verbose);
        }

        // W4-C Lift 1 — set_space convenience wrapper.
        //
        // VFXContext.space is a C# property (not a VFXSetting) that delegates
        // through to VFXDataParticle.space (ISpaceable). We locate a unique
        // spaceable context, coerce the string value to VFXSpace, and set it
        // directly. The mutation is wrapped in a transaction so the three-part
        // health gate still runs. set_setting ops are not structurally verified
        // against the YAML (see VfxYamlVerifier: only "add"/"connect" kinds are
        // checked), so recording with a synthetic token is safe.
        private static object SetSpace(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            object rawValue = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            CollectSystemContexts(path, out _, out var spaceableContexts);
            if (spaceableContexts.Count == 0)
                return NeedsExplicitTarget("space", "no spaceable contexts found in the graph.", verbose);
            if (spaceableContexts.Count > 1)
                return NeedsExplicitTarget("space",
                    $"{spaceableContexts.Count} spaceable contexts found — set_space requires a unique target.", verbose);

            // Coerce "World" / "Local" / "None" to VFXSpace.
            string asString = rawValue.ToString();
            if (!System.Enum.TryParse<VFXSpace>(asString, ignoreCase: true, out var parsed))
                throw new VfxValidationException("invalid_value",
                    $"Cannot coerce '{asString}' to VFXSpace (expected World, Local, or None).", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                spaceableContexts[0].space = parsed;
                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = "@context_space",
                    Payload       = new Dictionary<string, object>
                    {
                        ["name"]  = "space",
                        ["value"] = parsed.ToString(),
                    },
                });

                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                var payload = new JObject
                {
                    ["set_space"] = new JObject
                    {
                        ["value"] = parsed.ToString(),
                    },
                };
                return payload;
            }
        }

        // W4-C Lift 1 — set_capacity convenience wrapper.
        //
        // capacity is a VFXSetting on VFXDataParticle (not on the init context
        // directly). We locate the unique VFXBasicInitialize context, walk to
        // its VFXDataParticle, and call SetSettingValue("capacity", uint).
        private static object SetCapacity(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            object rawValue = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            CollectSystemContexts(path, out var initContexts, out _);
            if (initContexts.Count == 0)
                return NeedsExplicitTarget("capacity", "no VFXBasicInitialize context found in the graph.", verbose);
            if (initContexts.Count > 1)
                return NeedsExplicitTarget("capacity",
                    $"{initContexts.Count} VFXBasicInitialize contexts found — set_capacity requires a unique target.", verbose);

            var data = initContexts[0].GetData() as VFXDataParticle;
            if (data == null)
                return NeedsExplicitTarget("capacity",
                    "init context does not own a VFXDataParticle (spawner/event init?).", verbose);

            uint coerced;
            try { coerced = System.Convert.ToUInt32(rawValue); }
            catch (System.Exception ex)
            {
                throw new VfxValidationException("invalid_value",
                    $"Cannot coerce '{rawValue}' to uint for capacity: {ex.Message}", null);
            }

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                data.SetSettingValue("capacity", coerced);
                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = "@init_data",
                    Payload       = new Dictionary<string, object>
                    {
                        ["name"]  = "capacity",
                        ["value"] = coerced,
                    },
                });

                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                return new JObject
                {
                    ["set_capacity"] = new JObject
                    {
                        ["value"] = coerced,
                    },
                };
            }
        }

        // W4-C Lift 1 — set_bounds convenience wrapper.
        //
        // Real VFXBasicInitialize bounds are a compound (center, size) stored
        // on the init context's "bounds" input *slot* (property, not setting),
        // with a mode enum on VFXDataParticle.boundsMode. The convenience
        // wrapper sets the mode to Manual (if `mode` is supplied, we honor it)
        // which is the correct precursor step before setting bounds via
        // vfx_node.set_property on the init context. Clients that want center/
        // size still need the explicit set_property call because slot values
        // require token resolution and coerced compound payloads that the
        // kernel only does through NodeOps.SetProperty.
        private static object SetBounds(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            CollectSystemContexts(path, out var initContexts, out _);
            if (initContexts.Count == 0)
                return NeedsExplicitTarget("bounds", "no VFXBasicInitialize context found in the graph.", verbose);
            if (initContexts.Count > 1)
                return NeedsExplicitTarget("bounds",
                    $"{initContexts.Count} VFXBasicInitialize contexts found — set_bounds requires a unique target.", verbose);

            var data = initContexts[0].GetData() as VFXDataParticle;
            if (data == null)
                return NeedsExplicitTarget("bounds",
                    "init context does not own a VFXDataParticle (spawner/event init?).", verbose);

            // Accept an optional "mode" string: "Manual", "Recorded", "Automatic".
            // If omitted, default to Manual (the mode that lets callers supply
            // explicit center/size through the vfx_node.set_property path).
            string modeStr = @params.Value<string>("mode") ?? "Manual";
            if (!System.Enum.TryParse<BoundsSettingMode>(modeStr, ignoreCase: true, out var parsedMode))
                throw new VfxValidationException("invalid_value",
                    $"Cannot coerce '{modeStr}' to BoundsSettingMode (expected Manual, Recorded, or Automatic).", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                data.SetSettingValue("boundsMode", parsedMode);
                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = "@init_data",
                    Payload       = new Dictionary<string, object>
                    {
                        ["name"]  = "boundsMode",
                        ["value"] = parsedMode.ToString(),
                    },
                });

                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                return new JObject
                {
                    ["set_bounds"] = new JObject
                    {
                        ["mode"] = parsedMode.ToString(),
                        ["hint"] = "boundsMode set. For explicit center/size, call vfx_node.set_property " +
                                   "on the init context's 'bounds' slot.",
                    },
                };
            }
        }

        private static object SetDataSettings(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string name = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);

            // F-A5 (v0.3.1): conditional dispatch. If 'name' is a VFXGraph-level setting,
            // route through the @graph branch on NodeOps.SetSetting. Otherwise the kernel
            // throws setting_not_found and we surface an honest not_implemented envelope.
            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                try
                {
                    SetDataSettingsInner(@params, scope);
                }
                catch (VfxValidationException ex) when (ex.Code == "setting_not_found")
                {
                    return VfxKernelContainer.Shaper.ShapeRead(new JObject
                    {
                        ["state"] = "not_implemented",
                        ["hint"]  = $"'{name}' is not a graph-level setting. " +
                                    "Use vfx_node.set_setting on the relevant context/init token.",
                    }, verbose);
                }

                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        internal static object SetDataSettingsInner(JObject @params, VfxTransactionScope scope)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string name = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);
            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.NodeOps.SetSetting(path, "@graph", name, value);

            scope.Record(new VfxIntentOp
            {
                OpIndex       = 0,
                Kind          = "set_setting",
                ExpectedToken = "@graph",
                Payload       = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["name"]  = name,
                    ["value"] = value,
                },
            });

            return new JObject
            {
                ["set_data_settings"] = new JObject
                {
                    ["name"]  = name,
                    ["value"] = value?.ToString(),
                },
            };
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
