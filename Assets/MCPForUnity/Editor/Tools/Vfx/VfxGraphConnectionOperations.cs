using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphConnectionOperations
    {
        internal static object LinkContexts(JObject @params) => VfxGraphConnectionService.LinkContexts(@params);
        internal static object AddBlock(JObject @params) => VfxGraphConnectionService.AddBlock(@params);
        internal static object RemoveBlock(JObject @params) => VfxGraphConnectionService.RemoveBlock(@params);
        internal static object ListBlockTypes(JObject @params) => VfxGraphConnectionService.ListBlockTypes(@params);
        internal static object LinkGpuEvent(JObject @params) => VfxGraphConnectionService.LinkGpuEvent(@params);
        internal static object SetCapacity(JObject @params) => VfxGraphConnectionService.SetCapacity(@params);
        internal static object SetSpace(JObject @params) => VfxGraphConnectionService.SetSpace(@params);
    }
}
