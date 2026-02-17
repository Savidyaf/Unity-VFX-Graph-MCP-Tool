using Newtonsoft.Json.Linq;
using UnityEngine;

using UnityEngine.VFX;

namespace MCPForUnity.Editor.Tools.Vfx
{
    /// <summary>
    /// Common utilities for VFX Graph operations.
    /// </summary>
    internal static class VfxGraphCommon
    {

        /// <summary>
        /// Finds a VisualEffect component on the target GameObject.
        /// </summary>
        public static VisualEffect FindVisualEffect(JObject @params)
        {
            if (@params == null)
                return null;

            GameObject go = ManageVfxCommon.FindTargetGameObject(@params);
            return go?.GetComponent<VisualEffect>();
        }

    }
}
