using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxToolContract
    {
        internal const string ToolVersion = "1.0.0";

        internal static object Success(string message, object data = null, object details = null)
        {
            return new
            {
                success = true,
                error_code = (string)null,
                message,
                data,
                details,
                tool_version = ToolVersion
            };
        }

        internal static object Error(string errorCode, string message, object details = null, object data = null)
        {
            return new
            {
                success = false,
                error_code = string.IsNullOrEmpty(errorCode) ? VfxErrorCodes.UnknownError : errorCode,
                message,
                data,
                details,
                tool_version = ToolVersion
            };
        }
    }

    internal static class VfxErrorCodes
    {
        internal const string MissingAction = "missing_action";
        internal const string UnknownAction = "unknown_action";
        internal const string ValidationError = "validation_error";
        internal const string UnsupportedPipeline = "unsupported_pipeline";
        internal const string AssetNotFound = "asset_not_found";
        internal const string NotFound = "not_found";
        internal const string ReflectionError = "reflection_error";
        internal const string InternalException = "internal_exception";
        internal const string UnknownError = "unknown_error";
    }

    internal static class VfxActions
    {
        private static readonly HashSet<string> GraphActionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "get_graph_info",
            "add_node", "remove_node", "move_node", "duplicate_node",
            "connect_nodes", "disconnect_nodes", "get_connections", "set_node_property", "set_node_setting", "get_node_settings", "list_node_types",
            "link_contexts", "add_block", "remove_block", "list_block_types",
            "add_property", "list_properties", "remove_property", "set_property_value",
            "set_hlsl_code", "create_buffer_helper",
            "link_gpu_event", "set_capacity", "set_space",
            "create_asset", "list_assets", "list_templates", "assign_asset", "save_graph",
            "read_vfx_console"
        };

        internal static readonly string[] GraphActions = GraphActionSet.ToArray();

        internal static bool IsKnownAction(string action) => GraphActionSet.Contains(action);

        internal static readonly IDictionary<string, string> GraphAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "graph_add_node", "add_node" },
            { "delete_node", "remove_node" },
            { "graph_remove_node", "remove_node" },
            { "graph_move_node", "move_node" },
            { "graph_duplicate_node", "duplicate_node" }
        };

        internal static string NormalizeGraphAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return action;
            if (GraphAliases.TryGetValue(action, out var canonical)) return canonical;
            return action;
        }
    }
}
