using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphConnectionOperations
    {
        internal static object LinkContexts(JObject @params) => VfxGraphEdit.LinkContexts(@params);
        internal static object AddBlock(JObject @params) => VfxGraphEdit.AddBlock(@params);
        internal static object RemoveBlock(JObject @params) => VfxGraphEdit.RemoveBlock(@params);
        internal static object ListBlockTypes(JObject @params) => VfxGraphEdit.ListBlockTypes(@params);
        internal static object LinkGpuEvent(JObject @params) => VfxGraphEdit.LinkGPUEvent(@params);
        internal static object SetSpace(JObject @params) => VfxGraphEdit.SetSpace(@params);
    }
}
