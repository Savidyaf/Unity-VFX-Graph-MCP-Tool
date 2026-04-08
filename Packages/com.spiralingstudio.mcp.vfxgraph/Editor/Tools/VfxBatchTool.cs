// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxBatchTool.cs
//
// Phase 4C — VFX batch dispatcher tool.
//
// Accepts a single "commit" action with a graph path and an ops[] array.
// Each op carries a "tool" name (one of the 8 known tool names) and a full
// set of parameters for that tool.  All ops execute inside a single
// VfxTransactionScope with a single end-of-batch health gate.
//
// Implementation detail:
//   The dispatcher resolves each tool name to its static class via a compile-time
//   switch (allowed: reflection on our OWN tool classes; forbidden: reflection on
//   UnityEditor.VFX.* types).  It then calls ApplyInTransaction(opParams, scope)
//   on the resolved type via MethodInfo.Invoke.  Lanes 4A/4B left those stubs
//   as NotImplementedException("v0.3.1 batch integration"), so most ops will
//   surface that error at runtime in v0.3.0.  VfxSubgraphTool.add_ref is the
//   one op with a real ApplyInTransaction body (this lane).
//
// ApplyInTransaction (self) — throws NotImplementedException; batches cannot nest.

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using System.Reflection;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_batch", AutoRegister = true, Group = "vfx")]
    public static class VfxBatchTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? "commit").ToLowerInvariant();
                bool verbose = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "commit" => Commit(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"Unknown vfx_batch action '{action}'",
                        Hint    = "Valid: commit",
                    }),
                };
            }
            catch (VfxException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                });
            }
            catch (System.Reflection.TargetInvocationException tex)
                when (tex.InnerException is System.NotImplementedException)
            {
                // MethodInfo.Invoke wraps inner exceptions in TargetInvocationException,
                // so NotImplementedException from a tool's ApplyInTransaction arrives
                // wrapped. Unwrap it into a friendly not_implemented envelope.
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = "not_implemented",
                    Message = tex.InnerException.Message,
                    Hint    = "This action is not yet wired for batch dispatch — see Phase 4a F9.",
                });
            }
            catch (System.Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        // ── ApplyInTransaction — batches cannot nest ───────────────────────────
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            throw new System.NotImplementedException("Batches cannot nest inside batches");
        }

        // ── commit ─────────────────────────────────────────────────────────────

        private static object Commit(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException(
                    "missing_required_param", "graph is required", null);

            var ops = @params.Value<JArray>("ops");
            if (ops == null || ops.Count == 0)
                throw new VfxValidationException(
                    "missing_required_param", "ops array is required and must be non-empty", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.Batch))
            {
                int opIndex = 0;
                foreach (JObject op in ops)
                {
                    string toolName = op.Value<string>("tool")
                        ?? throw new VfxValidationException("missing_required_param",
                            $"op[{opIndex}].tool is required", null);

                    // Compile-time switch — resolves our OWN static tool classes.
                    // Reflection on UnityEditor.VFX.* is forbidden; reflection on our
                    // own generated/tool classes is allowed and explicitly tested.
                    System.Type toolType = ResolveToolType(toolName);
                    if (toolType == null)
                        throw new VfxValidationException("unknown_tool",
                            $"op[{opIndex}].tool '{toolName}' is not one of the 8 known tools", null);

                    MethodInfo method = toolType.GetMethod(
                        "ApplyInTransaction",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (method == null)
                        throw new VfxValidationException("missing_apply_in_transaction",
                            $"{toolType.Name}.ApplyInTransaction not found — " +
                            "tool class is not batch-compatible", null);

                    method.Invoke(null, new object[] { op, scope });
                    opIndex++;
                }

                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        // ── tool name → type resolution ────────────────────────────────────────
        // Compile-time switch; no Type.GetType() or assembly scanning.

        private static System.Type ResolveToolType(string toolName)
        {
            return toolName switch
            {
                "vfx_asset"    => typeof(VfxAssetTool),
                "vfx_graph"    => typeof(VfxGraphTool),
                "vfx_node"     => typeof(VfxNodeTool),
                "vfx_block"    => typeof(VfxBlockTool),
                "vfx_property" => typeof(VfxPropertyTool),
                "vfx_subgraph" => typeof(VfxSubgraphTool),
                "vfx_recipe"   => typeof(VfxRecipeTool),
                "vfx_diag"     => typeof(VfxDiagTool),
                _              => null,
            };
        }
    }
}
