// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxDiagTool.cs
//
// Phase 4 Lane A — VFX catalog inspection and diagnostics.
//
// Actions (all READ-ONLY — no BusyGate, no transaction):
//   list_node_types, list_block_types, list_contexts, list_attributes,
//   list_settings, list_subgraphs, read_console, get_warnings.
//
// Catalog source: VfxCatalog.g.cs (auto-generated, do not reflect on VFX types).
// Page size: 50 items per page by default.

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Generated;
using SpiralingStudio.VfxMcp.Kernel;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_diag", AutoRegister = true, Group = "vfx")]
    public static class VfxDiagTool
    {
        private const int DefaultPageSize = 50;

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
                    "list_node_types"  => ListNodeTypes(@params, verbose),
                    "list_block_types" => ListBlockTypes(@params, verbose),
                    "list_contexts"    => ListContexts(@params, verbose),
                    "list_attributes"  => ListAttributes(@params, verbose),
                    "list_settings"    => ListSettings(@params, verbose),
                    "list_subgraphs"   => ListSubgraphs(@params, verbose),
                    "read_console"     => ReadConsole(@params, verbose),
                    "get_warnings"     => GetWarnings(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code = "unknown_action",
                        Message = $"Unknown vfx_diag action '{action}'",
                        Hint = "Valid: list_node_types, list_block_types, list_contexts, " +
                               "list_attributes, list_settings, list_subgraphs, read_console, get_warnings",
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
                "VfxDiagTool.ApplyInTransaction will be wired in phase 4C batch integration");
        }

        // ── per-action private helpers ──

        private static object ListNodeTypes(JObject @params, bool verbose)
        {
            int page = @params?.Value<int?>("page") ?? 0;
            return VfxKernelContainer.Shaper.ShapeRead(
                Page(VfxCatalog.Operators, page, DefaultPageSize, "node_types"), verbose);
        }

        private static object ListBlockTypes(JObject @params, bool verbose)
        {
            int page = @params?.Value<int?>("page") ?? 0;
            return VfxKernelContainer.Shaper.ShapeRead(
                Page(VfxCatalog.Blocks, page, DefaultPageSize, "block_types"), verbose);
        }

        private static object ListContexts(JObject @params, bool verbose)
        {
            int page = @params?.Value<int?>("page") ?? 0;
            return VfxKernelContainer.Shaper.ShapeRead(
                Page(VfxCatalog.Contexts, page, DefaultPageSize, "contexts"), verbose);
        }

        private static object ListAttributes(JObject @params, bool verbose)
        {
            // F10 (v0.3.1): honest not_implemented stub. The VFX attribute
            // catalog (VFXAttributesManager built-ins + per-graph custom
            // attributes) requires either an internal-API walker or a soft-fork
            // bridge helper — deferred to v0.3.2 per spec §4.1 Path A4-stub.
            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["state"]      = "not_implemented",
                ["hint"]       = "Built-in attribute enumeration deferred to v0.3.2 — see Phase 4a F10.",
                ["attributes"] = new JArray(),
            }, verbose);
        }

        private static object ListSettings(JObject @params, bool verbose)
        {
            // F10 (v0.3.1): honest not_implemented stub. Per-type setting
            // enumeration (walking [VFXSetting]-attributed fields across every
            // VFXModel subclass) requires walker + emitter changes deferred to
            // v0.3.2 per spec §4.1 Path A4-stub.
            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["state"]    = "not_implemented",
                ["hint"]     = "Per-type setting enumeration deferred to v0.3.2 — see Phase 4a F10.",
                ["settings"] = new JArray(),
            }, verbose);
        }

        private static object ListSubgraphs(JObject @params, bool verbose)
        {
            int page = @params?.Value<int?>("page") ?? 0;

            // Merge SubgraphOperators and SubgraphBlocks into one response.
            // SubgraphContexts exists in the catalog but is empty (left as-is).
            int opsCount = VfxCatalog.SubgraphOperators?.Length ?? 0;
            int blkCount = VfxCatalog.SubgraphBlocks?.Length ?? 0;
            int total = opsCount + blkCount;

            // Build a combined array with a "kind" label so callers can distinguish.
            var combined = new string[total];
            if (opsCount > 0) System.Array.Copy(VfxCatalog.SubgraphOperators, 0, combined, 0, opsCount);
            if (blkCount > 0) System.Array.Copy(VfxCatalog.SubgraphBlocks, 0, combined, opsCount, blkCount);

            int start   = System.Math.Max(0, page * DefaultPageSize);
            int end     = System.Math.Min(start + DefaultPageSize, total);
            var arr     = new JArray();
            for (int i = start; i < end; i++)
            {
                string kind = i < opsCount ? "operator" : "block";
                arr.Add(new JObject { ["fqn"] = combined[i], ["kind"] = kind });
            }

            return VfxKernelContainer.Shaper.ShapeRead(new JObject
            {
                ["subgraphs"] = arr,
                ["page"]      = page,
                ["total"]     = total,
                ["has_next"]  = end < total,
            }, verbose);
        }

        private static object ReadConsole(JObject @params, bool verbose)
        {
            // ERRATUM P-M4: ring-buffer read via high-water-mark approach.
            var lines = VfxConsoleReader.GetLinesSince(s_lastReadMark);
            s_lastReadMark = VfxConsoleReader.GetHighWaterMark();
            return new JObject { ["lines"] = new JArray(lines) };
        }

        private static object GetWarnings(JObject @params, bool verbose)
        {
            // TODO Phase 5: wire into IVFXErrorReporter.GetDirtyModelErrors when
            // the reporter is accessible via a non-reflection surface.
            return new JObject { ["warnings"] = new JArray() };
        }

        // ── pagination helper ──

        private static JObject Page(string[] items, int page, int size, string key)
        {
            int start = System.Math.Max(0, page * size);
            int end   = System.Math.Min(start + size, items?.Length ?? 0);
            var arr   = new JArray();
            for (int i = start; i < end; i++) arr.Add(items[i]);
            return new JObject
            {
                [key]        = arr,
                ["page"]     = page,
                ["total"]    = items?.Length ?? 0,
                ["has_next"] = end < (items?.Length ?? 0),
            };
        }
    }
}
