// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxRecipeTool.cs
//
// Phase 4C — VFX recipe tool scaffold.
//
// Recipes are deferred to v0.3.1.  This file provides the minimum required
// surface so the tool is registered and batch-compatible:
//
//   list — the only working action; returns an empty recipe array with a note.
//   any other action — surfaces "unknown_action" via the standard error path.
//
// ApplyInTransaction — stub; throws NotImplementedException("v0.3.1").

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_recipe", AutoRegister = true, Group = "vfx")]
    public static class VfxRecipeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "list" => List(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"vfx_recipe only supports 'list' in v0.3.0",
                        Hint    = "Recipes are deferred to v0.3.1.",
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
            catch (System.Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        // ── ApplyInTransaction — F9 batch dispatch entry point ──
        // VfxRecipeTool is a scaffold with no mutating actions in v0.3.x; recipes
        // are deferred to a later release.
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            throw new System.NotImplementedException(
                $"{nameof(VfxRecipeTool)} has no mutating actions and cannot participate in batches");
        }

        // ── per-action private helpers ─────────────────────────────────────────

        private static object List(JObject @params, bool verbose)
        {
            // Read-only — skip BusyGate and transaction.
            // Recipes deferred to v0.3.1.
            var payload = new JObject
            {
                ["recipes"] = new JArray(),
                ["note"]    = "Recipes deferred to v0.3.1.",
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }
    }
}
