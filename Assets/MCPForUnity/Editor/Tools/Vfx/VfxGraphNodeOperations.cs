using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphNodeOperations
    {
        internal static object AddNode(JObject @params) => VfxGraphNodeService.AddNode(@params);
        internal static object RemoveNode(JObject @params) => VfxGraphNodeService.RemoveNode(@params);
        internal static object MoveNode(JObject @params) => VfxGraphNodeService.MoveNode(@params);
        internal static object DuplicateNode(JObject @params) => VfxGraphNodeService.DuplicateNode(@params);
        internal static object ConnectNodes(JObject @params) => VfxGraphNodeService.ConnectNodes(@params);
        internal static object DisconnectNodes(JObject @params) => VfxGraphNodeService.DisconnectNodes(@params);
        internal static object GetConnections(JObject @params) => VfxGraphNodeService.GetConnections(@params);
        internal static object SetNodeProperty(JObject @params) => VfxGraphNodeService.SetNodeProperty(@params);
        internal static object SetNodeSetting(JObject @params) => VfxGraphNodeService.SetNodeSetting(@params);
        internal static object GetNodeSettings(JObject @params) => VfxGraphNodeService.GetNodeSettings(@params);
        internal static object ListNodeTypes(JObject @params) => VfxGraphNodeService.ListNodeTypes(@params);
    }
}
