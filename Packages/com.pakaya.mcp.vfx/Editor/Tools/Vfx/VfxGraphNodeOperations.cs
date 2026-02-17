using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphNodeOperations
    {
        internal static object AddNode(JObject @params) => VfxGraphEdit.AddNode(@params);
        internal static object RemoveNode(JObject @params) => VfxGraphEdit.RemoveNode(@params);
        internal static object MoveNode(JObject @params) => VfxGraphEdit.MoveNode(@params);
        internal static object DuplicateNode(JObject @params) => VfxGraphEdit.DuplicateNode(@params);
        internal static object ConnectNodes(JObject @params) => VfxGraphEdit.ConnectNodes(@params);
        internal static object SetNodeProperty(JObject @params) => VfxGraphEdit.SetNodeProperty(@params);
        internal static object SetNodeSetting(JObject @params) => VfxGraphEdit.SetNodeSetting(@params);
        internal static object GetNodeSettings(JObject @params) => VfxGraphEdit.GetNodeSettings(@params);
        internal static object ListNodeTypes(JObject @params) => VfxGraphEdit.ListNodeTypes(@params);
    }
}
