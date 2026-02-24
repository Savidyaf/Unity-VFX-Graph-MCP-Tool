using System.Linq;
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
                return error;

            var pathParam = new JObject { ["path"] = path };

            var graphInfo = VfxGraphResultMapper.Wrap(VfxGraphEdit.GetGraphInfo(pathParam), "get_graph_info");
            var properties = VfxGraphResultMapper.Wrap(VfxGraphEdit.ListProperties(pathParam), "list_properties");
            var connections = VfxGraphResultMapper.Wrap(VfxGraphEdit.GetConnections(pathParam), "get_connections");
            var compilation = VfxGraphResultMapper.Wrap(VfxGraphEdit.GetCompilationStatus(pathParam), "get_compilation_status");
            var attributes = VfxGraphResultMapper.Wrap(VfxGraphEdit.ListAttributes(pathParam), "list_attributes");

            return VfxToolContract.Success(
                $"Full inspection of {path}",
                new
                {
                    path,
                    graph = graphInfo,
                    properties,
                    connections,
                    compilation,
                    attributes,
                    availableBlockAliases = VfxAttributeAliases.AllBlockAliases
                        .Select(kv => new { alias = kv.Key, internalType = kv.Value.InternalType, description = kv.Value.Description })
                        .OrderBy(x => x.alias)
                        .ToList()
                });
        }
    }
}
