using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphIntrospectionService
    {
        internal static object GetGraphInfo(JObject @params) => VfxGraphEdit.GetGraphInfo(@params);
        internal static object ListNodeTypes(JObject @params) => VfxGraphEdit.ListNodeTypes(@params);
        internal static object ListBlockTypes(JObject @params) => VfxGraphEdit.ListBlockTypes(@params);
        internal static object ListProperties(JObject @params) => VfxGraphEdit.ListProperties(@params);
        internal static object GetNodeSettings(JObject @params) => VfxGraphEdit.GetNodeSettings(@params);
        internal static object SaveGraph(JObject @params) => VfxGraphEdit.SaveGraph(@params);
    }
}
