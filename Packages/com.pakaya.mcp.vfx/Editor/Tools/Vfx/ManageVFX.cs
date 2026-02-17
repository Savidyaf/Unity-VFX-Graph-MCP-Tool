using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using UnityEngine;
using UnityEditor;
using UnityEngine.VFX;

namespace MCPForUnity.Editor.Tools.Vfx
{
    /// <summary>
    /// Tool for managing Unity VFX components:
    /// - ParticleSystem (legacy particle effects)
    /// - Visual Effect Graph (modern GPU particles, currently only support HDRP, other SRPs may not work)
    /// - LineRenderer (lines, bezier curves, shapes)
    /// - TrailRenderer (motion trails)
    /// </summary>
    [McpForUnityTool("manage_vfx", AutoRegister = true)]
    public static class ManageVFX
    {
        private static readonly Dictionary<string, string> ParamAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "size_over_lifetime", "size" },
            { "start_color_line", "startColor" },
            { "sorting_layer_id", "sortingLayerID" },
            { "material", "materialPath" },
        };

        private static JObject NormalizeParams(JObject source)
        {
            if (source == null) return new JObject();
            var normalized = new JObject();
            var properties = ExtractProperties(source);
            if (properties != null)
            {
                foreach (var prop in properties.Properties())
                    normalized[NormalizeKey(prop.Name, true)] = NormalizeToken(prop.Value);
            }
            foreach (var prop in source.Properties())
            {
                if (string.Equals(prop.Name, "properties", StringComparison.OrdinalIgnoreCase)) continue;
                normalized[NormalizeKey(prop.Name, true)] = NormalizeToken(prop.Value);
            }
            return normalized;
        }

        private static JObject ExtractProperties(JObject source)
        {
            if (source == null) return null;
            if (!source.TryGetValue("properties", StringComparison.OrdinalIgnoreCase, out var token)) return null;
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token is JObject obj) return obj;
            if (token.Type == JTokenType.String)
            {
                try { return JToken.Parse(token.ToString()) as JObject; }
                catch (JsonException ex) { throw new JsonException($"Failed to parse 'properties' JSON string. Raw value: {token}", ex); }
            }
            return null;
        }

        private static string NormalizeKey(string key, bool allowAliases)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (string.Equals(key, "action", StringComparison.OrdinalIgnoreCase)) return "action";
            if (allowAliases && ParamAliases.TryGetValue(key, out var alias)) return alias;
            if (key.IndexOf('_') >= 0) return ToCamelCase(key);
            return key;
        }

        private static JToken NormalizeToken(JToken token)
        {
            if (token == null) return null;
            if (token is JObject obj)
            {
                var normalized = new JObject();
                foreach (var prop in obj.Properties())
                    normalized[NormalizeKey(prop.Name, false)] = NormalizeToken(prop.Value);
                return normalized;
            }
            if (token is JArray array)
            {
                var normalized = new JArray();
                foreach (var item in array) normalized.Add(NormalizeToken(item));
                return normalized;
            }
            return token;
        }

        private static string ToCamelCase(string key) => StringCaseUtility.ToCamelCase(key);

        public static object HandleCommand(JObject @params)
        {
            JObject normalizedParams = NormalizeParams(@params);
            string action = normalizedParams["action"]?.ToString();

            if (string.IsNullOrEmpty(action))
            {
                return VfxToolContract.Error(VfxErrorCodes.MissingAction, "Action is required");
            }

            try
            {
                string actionLower = action.ToLowerInvariant();

                if (actionLower == "ping")
                {
                    return VfxToolContract.Success(
                        "manage_vfx is reachable",
                        new
                        {
                            tool = "manage_vfx",
                            components = new[] { "ParticleSystem", "VisualEffect", "LineRenderer", "TrailRenderer" }
                        });
                }

                if (actionLower.StartsWith("particle_"))
                {
                    return HandleParticleSystemAction(normalizedParams, actionLower.Substring(9));
                }

                if (actionLower.StartsWith("vfx_"))
                {
                    return HandleVFXGraphAction_Inline(normalizedParams, actionLower.Substring(4));
                }

                if (actionLower.StartsWith("line_"))
                {
                    return HandleLineRendererAction(normalizedParams, actionLower.Substring(5));
                }

                if (actionLower.StartsWith("trail_"))
                {
                    return HandleTrailRendererAction(normalizedParams, actionLower.Substring(6));
                }

                return VfxToolContract.Error(
                    VfxErrorCodes.UnknownAction,
                    $"Unknown action: {action}. Actions must be prefixed with: particle_, vfx_, line_, or trail_");
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.InternalException,
                    ex.Message,
                    new { stackTrace = ex.StackTrace });
            }
        }

        private static object HandleParticleSystemAction(JObject @params, string action)
        {
            switch (action)
            {
                case "get_info": return ParticleRead.GetInfo(@params);
                case "set_main": return ParticleWrite.SetMain(@params);
                case "set_emission": return ParticleWrite.SetEmission(@params);
                case "set_shape": return ParticleWrite.SetShape(@params);
                case "set_color_over_lifetime": return ParticleWrite.SetColorOverLifetime(@params);
                case "set_size_over_lifetime": return ParticleWrite.SetSizeOverLifetime(@params);
                case "set_velocity_over_lifetime": return ParticleWrite.SetVelocityOverLifetime(@params);
                case "set_noise": return ParticleWrite.SetNoise(@params);
                case "set_renderer": return ParticleWrite.SetRenderer(@params);
                case "enable_module": return ParticleControl.EnableModule(@params);
                case "play": return ParticleControl.Control(@params, "play");
                case "stop": return ParticleControl.Control(@params, "stop");
                case "pause": return ParticleControl.Control(@params, "pause");
                case "restart": return ParticleControl.Control(@params, "restart");
                case "clear": return ParticleControl.Control(@params, "clear");
                case "add_burst": return ParticleControl.AddBurst(@params);
                case "clear_bursts": return ParticleControl.ClearBursts(@params);
                default:
                    return new { success = false, message = $"Unknown particle action: {action}" };
            }
        }

        // ==================== VFX GRAPH ====================
        #region VFX Graph

        private static object HandleVFXGraphAction_Inline(JObject @params, string action)
        {
//#if !UNITY_VFX_GRAPH
//            return new { success = false, message = "VFX Graph package (com.unity.visualeffectgraph) not installed" };
//#else
            var pipelineSupport = VfxPipelineSupport.ValidateUrpSupport();
            if (!pipelineSupport.supported)
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.UnsupportedPipeline,
                    "vfx_* actions currently guarantee URP support only.",
                    new
                    {
                        active_pipeline = pipelineSupport.activePipeline,
                        required_pipeline = "UniversalRenderPipelineAsset"
                    });
            }

            switch (action)
            {
                // New Actions
                case "add_node": return VfxGraphResultMapper.Wrap(VfxGraphEdit.AddNode(@params), "add_node");
                case "graph_add_node": return VfxGraphResultMapper.Wrap(VfxGraphEdit.AddNode(@params), "add_node");
                case "connect_nodes": return VfxGraphResultMapper.Wrap(VfxGraphEdit.ConnectNodes(@params), "connect_nodes");
                case "set_node_property": return VfxGraphResultMapper.Wrap(VfxGraphEdit.SetNodeProperty(@params), "set_node_property");
                case "get_graph_info": return VfxGraphResultMapper.Wrap(VfxGraphEdit.GetGraphInfo(@params), "get_graph_info");
                case "list_node_types": return VfxGraphResultMapper.Wrap(VfxGraphEdit.ListNodeTypes(@params), "list_node_types");

                // Context flow linking
                case "link_contexts": return VfxGraphResultMapper.Wrap(VfxGraphEdit.LinkContexts(@params), "link_contexts");

                // Block management
                case "add_block": return VfxGraphResultMapper.Wrap(VfxGraphEdit.AddBlock(@params), "add_block");
                case "remove_block": return VfxGraphResultMapper.Wrap(VfxGraphEdit.RemoveBlock(@params), "remove_block");
                case "list_block_types": return VfxGraphResultMapper.Wrap(VfxGraphEdit.ListBlockTypes(@params), "list_block_types");

                // Node settings
                case "set_node_setting": return VfxGraphResultMapper.Wrap(VfxGraphEdit.SetNodeSetting(@params), "set_node_setting");
                case "get_node_settings": return VfxGraphResultMapper.Wrap(VfxGraphEdit.GetNodeSettings(@params), "get_node_settings");

                // Property/blackboard management (Phase 2)
                case "add_property": return VfxGraphResultMapper.Wrap(VfxGraphEdit.AddProperty(@params), "add_property");
                case "list_properties": return VfxGraphResultMapper.Wrap(VfxGraphEdit.ListProperties(@params), "list_properties");
                case "remove_property": return VfxGraphResultMapper.Wrap(VfxGraphEdit.RemoveProperty(@params), "remove_property");
                case "set_property_value": return VfxGraphResultMapper.Wrap(VfxGraphEdit.SetPropertyDefaultValue(@params), "set_property_value");

                // Custom HLSL + GraphicsBuffer (Phase 3)
                case "set_hlsl_code": return VfxGraphResultMapper.Wrap(VfxGraphEdit.SetHLSLCode(@params), "set_hlsl_code");
                case "create_buffer_helper": return VfxGraphResultMapper.Wrap(VfxGraphEdit.CreateGraphicsBufferHelper(@params), "create_buffer_helper");

                // GPU Events + Space (Phase 4)
                case "link_gpu_event": return VfxGraphResultMapper.Wrap(VfxGraphEdit.LinkGPUEvent(@params), "link_gpu_event");
                case "set_space": return VfxGraphResultMapper.Wrap(VfxGraphEdit.SetSpace(@params), "set_space");

                // Asset management
                case "create_asset": return VfxGraphAssets.CreateAsset(@params);
                case "assign_asset": return VfxGraphAssets.AssignAsset(@params);
                case "list_templates": return VfxGraphAssets.ListTemplates(@params);
                case "list_assets": return VfxGraphAssets.ListAssets(@params);

                // Runtime parameter control
                case "get_info": return VfxGraphRead.GetInfo(@params);
                case "set_float": return VfxGraphWrite.SetParameter<float>(@params, (vfx, n, v) => vfx.SetFloat(n, v));
                case "set_int": return VfxGraphWrite.SetParameter<int>(@params, (vfx, n, v) => vfx.SetInt(n, v));
                case "set_bool": return VfxGraphWrite.SetParameter<bool>(@params, (vfx, n, v) => vfx.SetBool(n, v));
                case "set_vector2": return VfxGraphWrite.SetVector(@params, 2);
                case "set_vector3": return VfxGraphWrite.SetVector(@params, 3);
                case "set_vector4": return VfxGraphWrite.SetVector(@params, 4);
                case "set_color": return VfxGraphWrite.SetColor(@params);
                case "set_gradient": return VfxGraphWrite.SetGradient(@params);
                case "set_texture": return VfxGraphWrite.SetTexture(@params);
                case "set_mesh": return VfxGraphWrite.SetMesh(@params);
                case "set_curve": return VfxGraphWrite.SetCurve(@params);
                case "send_event": return VfxGraphWrite.SendEvent(@params);
                case "play": return VfxGraphControl.Control(@params, "play");
                case "stop": return VfxGraphControl.Control(@params, "stop");
                case "pause": return VfxGraphControl.Control(@params, "pause");
                case "reinit": return VfxGraphControl.Control(@params, "reinit");
                case "set_playback_speed": return VfxGraphControl.SetPlaybackSpeed(@params);
                case "set_seed": return VfxGraphControl.SetSeed(@params);

                default:
                    return VfxToolContract.Error(VfxErrorCodes.UnknownAction, $"Unknown vfx action: {action}");
            }
//#endif
        }

        #endregion

        private static object HandleLineRendererAction(JObject @params, string action)
        {
            switch (action)
            {
                case "get_info": return LineRead.GetInfo(@params);
                case "set_positions": return LineWrite.SetPositions(@params);
                case "add_position": return LineWrite.AddPosition(@params);
                case "set_position": return LineWrite.SetPosition(@params);
                case "set_width": return LineWrite.SetWidth(@params);
                case "set_color": return LineWrite.SetColor(@params);
                case "set_material": return LineWrite.SetMaterial(@params);
                case "set_properties": return LineWrite.SetProperties(@params);
                case "clear": return LineWrite.Clear(@params);
                case "create_line": return LineCreate.CreateLine(@params);
                case "create_circle": return LineCreate.CreateCircle(@params);
                case "create_arc": return LineCreate.CreateArc(@params);
                case "create_bezier": return LineCreate.CreateBezier(@params);
                default:
                    return new { success = false, message = $"Unknown line action: {action}" };
            }
        }

        private static object HandleTrailRendererAction(JObject @params, string action)
        {
            switch (action)
            {
                case "get_info": return TrailRead.GetInfo(@params);
                case "set_time": return TrailWrite.SetTime(@params);
                case "set_width": return TrailWrite.SetWidth(@params);
                case "set_color": return TrailWrite.SetColor(@params);
                case "set_material": return TrailWrite.SetMaterial(@params);
                case "set_properties": return TrailWrite.SetProperties(@params);
                case "clear": return TrailControl.Clear(@params);
                case "emit": return TrailControl.Emit(@params);
                default:
                    return new { success = false, message = $"Unknown trail action: {action}" };
            }
        }
    }
}
