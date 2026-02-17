using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphActionRouter
    {
        private static readonly IReadOnlyDictionary<string, Func<JObject, object>> GraphHandlers =
            new Dictionary<string, Func<JObject, object>>(StringComparer.OrdinalIgnoreCase)
            {
                // Graph introspection
                { "get_graph_info", VfxGraphEdit.GetGraphInfo },

                // Node CRUD
                { "add_node", VfxGraphNodeOperations.AddNode },
                { "remove_node", VfxGraphNodeOperations.RemoveNode },
                { "move_node", VfxGraphNodeOperations.MoveNode },
                { "duplicate_node", VfxGraphNodeOperations.DuplicateNode },
                { "connect_nodes", VfxGraphNodeOperations.ConnectNodes },
                { "set_node_property", VfxGraphNodeOperations.SetNodeProperty },
                { "set_node_setting", VfxGraphNodeOperations.SetNodeSetting },
                { "get_node_settings", VfxGraphNodeOperations.GetNodeSettings },
                { "list_node_types", VfxGraphNodeOperations.ListNodeTypes },

                // Context flow & blocks
                { "link_contexts", VfxGraphConnectionOperations.LinkContexts },
                { "add_block", VfxGraphConnectionOperations.AddBlock },
                { "remove_block", VfxGraphConnectionOperations.RemoveBlock },
                { "list_block_types", VfxGraphConnectionOperations.ListBlockTypes },

                // Blackboard properties
                { "add_property", VfxGraphPropertyOperations.AddProperty },
                { "list_properties", VfxGraphPropertyOperations.ListProperties },
                { "remove_property", VfxGraphPropertyOperations.RemoveProperty },
                { "set_property_value", VfxGraphPropertyOperations.SetPropertyValue },

                // HLSL & buffer
                { "set_hlsl_code", VfxGraphPropertyOperations.SetHlslCode },
                { "create_buffer_helper", VfxGraphPropertyOperations.CreateBufferHelper },

                // GPU events & space
                { "link_gpu_event", VfxGraphConnectionOperations.LinkGpuEvent },
                { "set_space", VfxGraphConnectionOperations.SetSpace },

                // Asset lifecycle
                { "create_asset", p => VfxGraphAssets.CreateAsset(p) },
                { "list_assets", p => VfxGraphAssets.ListAssets(p) },
                { "list_templates", p => VfxGraphAssets.ListTemplates(p) },
                { "assign_asset", p => VfxGraphAssets.AssignAsset(p) }
            };

        internal static bool TryHandle(string action, JObject @params, out object mappedResult)
        {
            mappedResult = null;
            if (!GraphHandlers.TryGetValue(action, out var handler))
            {
                return false;
            }

            mappedResult = VfxGraphResultMapper.Wrap(handler(@params), action);
            return true;
        }
    }
}
