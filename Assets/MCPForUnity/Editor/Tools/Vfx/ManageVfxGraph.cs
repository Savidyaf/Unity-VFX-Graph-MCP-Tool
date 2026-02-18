using System;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Vfx
{
    /// <summary>
    /// Custom tool for VFX Graph editing operations (add nodes, blocks, properties, etc).
    /// Registered as a separate custom tool so the MCP schema doesn't block graph editing actions.
    /// </summary>
    [McpForUnityTool("manage_vfx_graph", AutoRegister = true)]
    public static class ManageVfxGraph
    {
        public static object HandleCommand(JObject @params)
        {
            if (@params == null) @params = new JObject();
            string action = @params["action"]?.ToString();
            if (string.IsNullOrEmpty(action))
                return VfxToolContract.Error(
                    VfxErrorCodes.MissingAction,
                    "action is required",
                    new { available_actions = VfxActions.GraphActions });

            string normalizedAction = VfxActions.NormalizeGraphAction(action.ToLowerInvariant());

            var pipelineSupport = VfxPipelineSupport.ValidateUrpSupport();
            if (!pipelineSupport.supported)
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.UnsupportedPipeline,
                    "This tool currently guarantees URP support only.",
                    new
                    {
                        active_pipeline = pipelineSupport.activePipeline,
                        required_pipeline = "UniversalRenderPipelineAsset",
                        remediation = "Switch Graphics and Quality render pipeline assets to URP, or run in a project configured for URP."
                    });
            }

            try
            {
                if (VfxGraphActionRouter.TryHandle(normalizedAction, @params, out object result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.InternalException,
                    $"Action '{normalizedAction}' failed: {ex.Message}",
                    new { action = normalizedAction, exception_type = ex.GetType().Name });
            }

            return VfxToolContract.Error(
                VfxErrorCodes.UnknownAction,
                $"Unknown graph action: {action}",
                new { available_actions = VfxActions.GraphActions });
        }
    }
}
