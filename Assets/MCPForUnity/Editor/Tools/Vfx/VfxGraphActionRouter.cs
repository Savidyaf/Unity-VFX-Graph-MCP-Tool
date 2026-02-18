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
                { "list_node_types", VfxGraphEdit.ListNodeTypes },
                { "list_block_types", VfxGraphEdit.ListBlockTypes },
                { "list_properties", VfxGraphEdit.ListProperties },
                { "get_node_settings", VfxGraphEdit.GetNodeSettings },
                { "get_connections", VfxGraphEdit.GetConnections },
                { "save_graph", VfxGraphEdit.SaveGraph },

                // Node CRUD
                { "add_node", VfxGraphEdit.AddNode },
                { "remove_node", VfxGraphEdit.RemoveNode },
                { "move_node", VfxGraphEdit.MoveNode },
                { "duplicate_node", VfxGraphEdit.DuplicateNode },
                { "connect_nodes", VfxGraphEdit.ConnectNodes },
                { "disconnect_nodes", VfxGraphEdit.DisconnectNodes },
                { "set_node_property", VfxGraphEdit.SetNodeProperty },
                { "set_node_setting", VfxGraphEdit.SetNodeSetting },

                // Context flow & blocks
                { "link_contexts", VfxGraphEdit.LinkContexts },
                { "add_block", VfxGraphEdit.AddBlock },
                { "remove_block", VfxGraphEdit.RemoveBlock },

                // Blackboard properties
                { "add_property", VfxGraphEdit.AddProperty },
                { "remove_property", VfxGraphEdit.RemoveProperty },
                { "set_property_value", VfxGraphEdit.SetPropertyDefaultValue },

                // HLSL & buffer
                { "set_hlsl_code", VfxGraphEdit.SetHLSLCode },
                { "create_buffer_helper", VfxGraphEdit.CreateGraphicsBufferHelper },

                // GPU events & space
                { "link_gpu_event", VfxGraphEdit.LinkGPUEvent },
                { "set_capacity", VfxGraphEdit.SetCapacity },
                { "set_space", VfxGraphEdit.SetSpace },

                // Asset lifecycle
                { "create_asset", VfxGraphAssets.CreateAsset },
                { "list_assets", VfxGraphAssets.ListAssets },
                { "list_templates", VfxGraphAssets.ListTemplates },
                { "assign_asset", VfxGraphAssets.AssignAsset },

                // Console (temporary workaround for broken upstream read_console in Unity 6)
                { "read_vfx_console", VfxConsoleReader.HandleAction },
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
