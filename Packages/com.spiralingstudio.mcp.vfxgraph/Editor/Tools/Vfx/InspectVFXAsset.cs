using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Vfx
{
    [McpForUnityTool("inspect_vfx_asset", AutoRegister = true)]
    public static class InspectVFXAsset
    {
        public static object HandleCommand(JObject @params)
        {
            if (!VfxInputValidation.TryGetRequiredString(@params, "path", out string path, out object error))
            {
                return error;
            }

            var graphInfoResult = VfxGraphResultMapper.Wrap(
                VfxGraphEdit.GetGraphInfo(new JObject { ["path"] = path }),
                "get_graph_info");

            var propertyResult = VfxGraphResultMapper.Wrap(
                VfxGraphEdit.ListProperties(new JObject { ["path"] = path }),
                "list_properties");

            return VfxToolContract.Success(
                $"Inspection complete for {path}",
                new
                {
                    path,
                    graph = graphInfoResult,
                    properties = propertyResult
                },
                new
                {
                    note = "inspect_vfx_asset now returns live graph and property introspection data."
                });
        }
    }
}