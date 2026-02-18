using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphPropertyOperations
    {
        internal static object AddProperty(JObject @params) => VfxGraphPropertyService.AddProperty(@params);
        internal static object ListProperties(JObject @params) => VfxGraphPropertyService.ListProperties(@params);
        internal static object RemoveProperty(JObject @params) => VfxGraphPropertyService.RemoveProperty(@params);
        internal static object SetPropertyValue(JObject @params) => VfxGraphPropertyService.SetPropertyValue(@params);
        internal static object SetHlslCode(JObject @params) => VfxGraphPropertyService.SetHlslCode(@params);
        internal static object CreateBufferHelper(JObject @params) => VfxGraphPropertyService.CreateBufferHelper(@params);
    }
}
