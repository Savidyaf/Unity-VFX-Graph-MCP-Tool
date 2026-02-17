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
                { "get_graph_info", VfxGraphEdit.GetGraphInfo },
                { "add_node", VfxGraphNodeOperations.AddNode },
                { "connect_nodes", VfxGraphNodeOperations.ConnectNodes },
                { "set_node_property", VfxGraphNodeOperations.SetNodeProperty },
                { "set_node_setting", VfxGraphNodeOperations.SetNodeSetting },
                { "get_node_settings", VfxGraphNodeOperations.GetNodeSettings },
                { "list_node_types", VfxGraphNodeOperations.ListNodeTypes },
                { "link_contexts", VfxGraphConnectionOperations.LinkContexts },
                { "add_block", VfxGraphConnectionOperations.AddBlock },
                { "remove_block", VfxGraphConnectionOperations.RemoveBlock },
                { "list_block_types", VfxGraphConnectionOperations.ListBlockTypes },
                { "add_property", VfxGraphPropertyOperations.AddProperty },
                { "list_properties", VfxGraphPropertyOperations.ListProperties },
                { "remove_property", VfxGraphPropertyOperations.RemoveProperty },
                { "set_property_value", VfxGraphPropertyOperations.SetPropertyValue },
                { "set_hlsl_code", VfxGraphPropertyOperations.SetHlslCode },
                { "create_buffer_helper", VfxGraphPropertyOperations.CreateBufferHelper },
                { "link_gpu_event", VfxGraphConnectionOperations.LinkGpuEvent },
                { "set_space", VfxGraphConnectionOperations.SetSpace }
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
