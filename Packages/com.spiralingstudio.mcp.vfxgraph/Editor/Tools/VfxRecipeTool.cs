// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxRecipeTool.cs
//
// Phase 4C — VFX recipe tool scaffold.
//
// W3-D LIFT (v0.3.1): Recipe NAMES are listed here; recipe EXECUTION is
// deferred to v0.3.2. Calling vfx_recipe.create (or similar) is NOT YET
// implemented and will return state=not_implemented via unknown_action.
//
//   list — returns the canonical catalog of 4 recipe {name, description}
//          entries. These match the legacy README create_from_recipe names.
//   any other action — surfaces "unknown_action" via the standard error path.
//
// ApplyInTransaction — stub; throws NotImplementedException (recipes don't
// participate in batches yet).

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
                        Message = $"vfx_recipe only supports 'list' in v0.3.1",
                        Hint    = "Recipe execution is deferred to v0.3.2.",
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
            // W3-D LIFT: Recipe NAMES are listed here; recipe EXECUTION is
            // deferred to v0.3.2. Hardcoded because recipes don't exist as a
            // Unity API — they are scaffolding templates we author ourselves.
            var recipes = new JArray
            {
                new JObject
                {
                    ["name"]        = "ecs_buffer_particles",
                    ["description"] = "Spawn → Init → Update → Output with GraphicsBuffer property and SampleBuffer",
                },
                new JObject
                {
                    ["name"]        = "simple_spawn_particles",
                    ["description"] = "Basic particle system with lifetime, velocity, color",
                },
                new JObject
                {
                    ["name"]        = "gpu_event_chain",
                    ["description"] = "Parent → child particle system via GPU events",
                },
                new JObject
                {
                    ["name"]        = "particle_strip_trail",
                    ["description"] = "Particle strip trail rendering",
                },
            };
            var payload = new JObject
            {
                ["recipes"] = recipes,
                ["total"]   = recipes.Count,
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }
    }
}
