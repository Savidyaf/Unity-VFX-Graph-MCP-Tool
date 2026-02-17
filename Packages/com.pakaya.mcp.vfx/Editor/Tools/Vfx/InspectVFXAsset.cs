using UnityEngine;
using UnityEditor;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    [McpForUnityTool("inspect_vfx_asset", AutoRegister = true)]
    public static class InspectVFXAsset
    {
        public static object HandleCommand(JObject @params)
        {
            var vfxGraphEditorAsm = System.Linq.Enumerable.FirstOrDefault(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
            
            if (vfxGraphEditorAsm == null) return new { success = false, message = "Graph Editor Assembly not found" };

            var slotType = vfxGraphEditorAsm.GetType("UnityEditor.VFX.VFXSlot");
            if (slotType == null) return new { success = false, message = "VFXSlot type not found" };

            var results = new System.Collections.Generic.List<string>();
            results.Add("Searching for Link methods on VFXSlot:");
            
            foreach (var method in slotType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (method.Name == "Link")
                {
                    var paramsInfo = method.GetParameters();
                    var paramStrings = System.Linq.Enumerable.Select(paramsInfo, p => $"{p.ParameterType.Name} {p.Name}");
                    results.Add($"Link({string.Join(", ", paramStrings)})");
                }
            }
             
             return new { success = true, results = results };
        }
    }
}