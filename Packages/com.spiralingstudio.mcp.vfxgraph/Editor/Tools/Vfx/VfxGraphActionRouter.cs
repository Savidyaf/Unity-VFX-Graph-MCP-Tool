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
                // Introspection
                { "get_graph_info", VfxGraphEdit.GetGraphInfo },
                { "get_connections", VfxGraphEdit.GetConnections },
                { "get_node_settings", VfxGraphEdit.GetNodeSettings },
                { "list_node_types", VfxGraphEdit.ListNodeTypes },
                { "list_block_types", VfxGraphEdit.ListBlockTypes },
                { "list_properties", VfxGraphEdit.ListProperties },
                { "list_attributes", VfxGraphEdit.ListAttributes },
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
                { "add_attribute_block", VfxGraphEdit.AddAttributeBlock },
                { "reorder_block", VfxGraphEdit.ReorderBlock },
                { "set_block_activation", VfxGraphEdit.SetBlockActivation },

                // Context & output configuration
                { "set_context_settings", VfxGraphEdit.SetContextSettings },
                { "configure_output", VfxGraphEdit.ConfigureOutput },
                { "set_bounds", VfxGraphEdit.SetBounds },

                // Blackboard properties
                { "add_property", VfxGraphEdit.AddProperty },
                { "remove_property", VfxGraphEdit.RemoveProperty },
                { "set_property_value", VfxGraphEdit.SetPropertyDefaultValue },

                // Custom attributes
                { "add_custom_attribute", VfxGraphEdit.AddCustomAttribute },
                { "remove_custom_attribute", VfxGraphEdit.RemoveCustomAttribute },

                // HLSL & buffer
                { "set_hlsl_code", VfxGraphEdit.SetHLSLCode },
                { "create_buffer_helper", VfxGraphEdit.CreateGraphicsBufferHelper },
                { "setup_buffer_pipeline", VfxGraphEdit.SetupBufferPipeline },

                // GPU events, space & capacity
                { "link_gpu_event", VfxGraphEdit.LinkGPUEvent },
                { "set_capacity", VfxGraphEdit.SetCapacity },
                { "set_space", VfxGraphEdit.SetSpace },

                // Compilation
                { "compile_graph", VfxGraphEdit.CompileGraph },
                { "get_compilation_status", VfxGraphEdit.GetCompilationStatus },

                // Batch & recipes
                { "batch", VfxGraphEdit.BatchExecute },
                { "create_from_recipe", VfxGraphEdit.CreateFromRecipe },

                // Asset lifecycle
                { "create_asset", VfxGraphAssets.CreateAsset },
                { "list_assets", VfxGraphAssets.ListAssets },
                { "list_templates", VfxGraphAssets.ListTemplates },
                { "assign_asset", VfxGraphAssets.AssignAsset },

                // Console
                { "read_vfx_console", VfxConsoleReader.HandleAction },
            };

        internal static bool TryHandle(string action, JObject @params, out object mappedResult)
        {
            mappedResult = null;
            if (!GraphHandlers.TryGetValue(action, out var handler))
                return false;

            mappedResult = VfxGraphResultMapper.Wrap(handler(@params), action);
            return true;
        }
    }
}
