using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxPipelineSupport
    {
        internal static (bool supported, string activePipeline) ValidateUrpSupport()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                return (false, "BuiltInRenderPipeline");
            }

            if (pipeline is UniversalRenderPipelineAsset)
            {
                return (true, pipeline.GetType().Name);
            }

            return (false, pipeline.GetType().Name);
        }
    }
}
