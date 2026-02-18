using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphPropertyService
    {
        internal static object AddProperty(JObject @params) => VfxGraphEdit.AddProperty(@params);
        internal static object ListProperties(JObject @params) => VfxGraphEdit.ListProperties(@params);
        internal static object RemoveProperty(JObject @params) => VfxGraphEdit.RemoveProperty(@params);
        internal static object SetPropertyValue(JObject @params) => VfxGraphEdit.SetPropertyDefaultValue(@params);
        internal static object SetHlslCode(JObject @params) => VfxGraphEdit.SetHLSLCode(@params);
        internal static object CreateBufferHelper(JObject @params) => VfxGraphEdit.CreateGraphicsBufferHelper(@params);
    }
}
