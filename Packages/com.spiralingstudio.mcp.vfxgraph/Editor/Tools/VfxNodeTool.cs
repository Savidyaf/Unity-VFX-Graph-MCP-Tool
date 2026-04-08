// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxNodeTool.cs
//
// Phase 4B — VFX node manipulation tool.
//
// Actions (10):
//   Mutating : add, remove, move, duplicate, connect, disconnect,
//              set_setting, set_property
//   Read-only: get_setting, get_property
//
// Pattern follows VfxAssetTool.cs exactly (Lane 4A reference).

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using SpiralingStudio.VfxMcp.Generated;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_node", AutoRegister = true, Group = "vfx")]
    public static class VfxNodeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose  = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "list"         => List(@params, verbose),
                    "add"          => Add(@params, verbose),
                    "remove"       => Remove(@params, verbose),
                    "move"         => Move(@params, verbose),
                    "duplicate"    => Duplicate(@params, verbose),
                    "connect"      => Connect(@params, verbose),
                    "disconnect"   => Disconnect(@params, verbose),
                    "set_setting"  => SetSetting(@params, verbose),
                    "set_property" => SetProperty(@params, verbose),
                    "get_setting"  => GetSetting(@params, verbose),
                    "get_property" => GetProperty(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"Unknown vfx_node action '{action}'",
                        Hint    = "Valid: list, add, remove, move, duplicate, connect, disconnect, " +
                                  "set_setting, set_property, get_setting, get_property",
                    }),
                };
            }
            catch (VfxException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                });
            }
            catch (System.Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        // ── ApplyInTransaction stub — lane C's batch dispatcher finds this via reflection ──
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            throw new System.NotImplementedException(
                "v0.3.1 batch integration");
        }

        // ────────────────────────── add ──────────────────────────

        private static object Add(JObject @params, bool verbose)
        {
            string path    = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string typeFqn = @params.Value<string>("type")
                ?? throw new VfxValidationException("missing_required_param", "type is required", null);
            float x = @params.Value<float?>("x") ?? 0f;
            float y = @params.Value<float?>("y") ?? 0f;

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                bool isContext = System.Array.IndexOf(VfxCatalog.Contexts, typeFqn) >= 0;
                string token = isContext
                    ? VfxKernelContainer.NodeOps.AddContext(path, typeFqn, new Vector2(x, y))
                    : VfxKernelContainer.NodeOps.AddOperator(path, typeFqn, new Vector2(x, y));

                scope.Record(new VfxIntentOp
                {
                    OpIndex         = 0,
                    Kind            = "add",
                    ExpectedToken   = token,
                    ExpectedTypeFqn = typeFqn,
                    Payload         = new Dictionary<string, object> { ["x"] = x, ["y"] = y },
                });

                var commit  = scope.Commit();
                var shaped  = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["added"] = new JArray(new JObject { ["token"] = token, ["type"] = typeFqn });
                return shaped;
            }
        }

        // ────────────────────────── remove ──────────────────────────

        private static object Remove(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.RemoveNode(path, token);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "remove",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object> { ["token"] = token },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["removed"] = new JArray(new JObject { ["token"] = token });
                return shaped;
            }
        }

        // ────────────────────────── move ──────────────────────────

        private static object Move(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            float x = @params.Value<float?>("x") ?? 0f;
            float y = @params.Value<float?>("y") ?? 0f;

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var model = VfxKernelContainer.Identity.Resolve(guid, token);
                if (model == null)
                    throw new VfxIdentityException("node_lost",
                        $"Token {token} not found in {path}", null);

                model.position = new Vector2(x, y);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object> { ["x"] = x, ["y"] = y },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["moved"] = new JObject { ["token"] = token, ["x"] = x, ["y"] = y };
                return shaped;
            }
        }

        // ────────────────────────── duplicate ──────────────────────────
        // NOTE: VFX Graph has no public single-model duplication API on VFXGraph.
        // We implement duplicate as add-of-same-type at offset position.
        // A full copy-paste round-trip is deferred to Phase 5.
        // TODO(Phase5): Use VFXGraph copy-paste API when available.

        private static object Duplicate(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            float offsetX = @params.Value<float?>("offset_x") ?? 30f;
            float offsetY = @params.Value<float?>("offset_y") ?? 30f;

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var model = VfxKernelContainer.Identity.Resolve(guid, token);
                if (model == null)
                    throw new VfxIdentityException("node_lost",
                        $"Token {token} not found in {path}", null);

                string typeFqn  = model.GetType().FullName;
                Vector2 newPos  = model.position + new Vector2(offsetX, offsetY);

                bool isContext = model is VFXContext;
                bool isParam   = model is VFXParameter;

                string newToken;
                if (isParam)
                    newToken = VfxKernelContainer.NodeOps.AddParameter(path, typeFqn, newPos);
                else if (isContext)
                    newToken = VfxKernelContainer.NodeOps.AddContext(path, typeFqn, newPos);
                else
                    newToken = VfxKernelContainer.NodeOps.AddOperator(path, typeFqn, newPos);

                scope.Record(new VfxIntentOp
                {
                    OpIndex         = 0,
                    Kind            = "add",
                    ExpectedToken   = newToken,
                    ExpectedTypeFqn = typeFqn,
                    Payload         = new Dictionary<string, object>
                    {
                        ["sourceToken"] = token,
                        ["x"] = newPos.x,
                        ["y"] = newPos.y,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["duplicated"] = new JObject
                    {
                        ["source"] = token,
                        ["token"]  = newToken,
                        ["type"]   = typeFqn,
                    };
                return shaped;
            }
        }

        // ────────────────────────── connect ──────────────────────────

        private static object Connect(JObject @params, bool verbose)
        {
            string path      = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string fromToken = @params.Value<string>("from_token")
                ?? throw new VfxValidationException("missing_required_param", "from_token is required", null);
            string fromSlot  = @params.Value<string>("from_slot")
                ?? throw new VfxValidationException("missing_required_param", "from_slot is required", null);
            string toToken   = @params.Value<string>("to_token")
                ?? throw new VfxValidationException("missing_required_param", "to_token is required", null);
            string toSlot    = @params.Value<string>("to_slot")
                ?? throw new VfxValidationException("missing_required_param", "to_slot is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.Connect(path, fromToken, fromSlot, toToken, toSlot);

                scope.Record(new VfxIntentOp
                {
                    OpIndex = 0,
                    Kind    = "connect",
                    Payload = new Dictionary<string, object>
                    {
                        ["from_token"] = fromToken,
                        ["from_slot"]  = fromSlot,
                        ["to_token"]   = toToken,
                        ["to_slot"]    = toSlot,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["connected"] = new JObject
                    {
                        ["from_token"] = fromToken,
                        ["from_slot"]  = fromSlot,
                        ["to_token"]   = toToken,
                        ["to_slot"]    = toSlot,
                    };
                return shaped;
            }
        }

        // ────────────────────────── disconnect ──────────────────────────

        private static object Disconnect(JObject @params, bool verbose)
        {
            string path      = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string fromToken = @params.Value<string>("from_token")
                ?? throw new VfxValidationException("missing_required_param", "from_token is required", null);
            string fromSlot  = @params.Value<string>("from_slot")
                ?? throw new VfxValidationException("missing_required_param", "from_slot is required", null);
            string toToken   = @params.Value<string>("to_token")
                ?? throw new VfxValidationException("missing_required_param", "to_token is required", null);
            string toSlot    = @params.Value<string>("to_slot")
                ?? throw new VfxValidationException("missing_required_param", "to_slot is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.Disconnect(path, fromToken, fromSlot, toToken, toSlot);

                scope.Record(new VfxIntentOp
                {
                    OpIndex = 0,
                    Kind    = "disconnect",
                    Payload = new Dictionary<string, object>
                    {
                        ["from_token"] = fromToken,
                        ["from_slot"]  = fromSlot,
                        ["to_token"]   = toToken,
                        ["to_slot"]    = toSlot,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["disconnected"] = new JObject
                    {
                        ["from_token"] = fromToken,
                        ["from_slot"]  = fromSlot,
                        ["to_token"]   = toToken,
                        ["to_slot"]    = toSlot,
                    };
                return shaped;
            }
        }

        // ────────────────────────── set_setting ──────────────────────────

        private static object SetSetting(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string name  = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);
            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetSetting(path, token, name, value);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object>
                    {
                        ["name"]  = name,
                        ["value"] = value,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["set_setting"] = new JObject
                    {
                        ["token"] = token,
                        ["name"]  = name,
                        ["value"] = value?.ToString(),
                    };
                return shaped;
            }
        }

        // ────────────────────────── set_property ──────────────────────────

        private static object SetProperty(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string name  = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);
            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                VfxKernelContainer.NodeOps.SetProperty(path, token, name, value);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_property",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object>
                    {
                        ["name"]  = name,
                        ["value"] = value,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["set_property"] = new JObject
                    {
                        ["token"] = token,
                        ["name"]  = name,
                        ["value"] = value?.ToString(),
                    };
                return shaped;
            }
        }

        // ────────────────────────── get_setting (read-only) ──────────────────────────

        private static object GetSetting(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string name  = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);

            // Read-only — skip BusyGate and transaction.
            object val = VfxKernelContainer.NodeOps.GetSetting(path, token, name);

            var payload = new JObject
            {
                ["token"] = token,
                ["name"]  = name,
                ["value"] = val != null ? JToken.FromObject(val) : JValue.CreateNull(),
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }

        // ────────────────────────── get_property (read-only) ──────────────────────────

        private static object GetProperty(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string name  = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);

            // Read-only — skip BusyGate and transaction.
            object val = VfxKernelContainer.NodeOps.GetProperty(path, token, name);

            var payload = new JObject
            {
                ["token"] = token,
                ["name"]  = name,
                ["value"] = val != null ? JToken.FromObject(val) : JValue.CreateNull(),
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }

        // ──────────────────────── list (read-only) ──────────────────────────

        private static object List(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            int pageSize = @params.Value<int?>("page_size") ?? 200;
            int offset   = @params.Value<int?>("offset") ?? 0;

            // Read-only — skip BusyGate and transaction.
            var entries = VfxKernelContainer.NodeOps.ListNodes(path);

            int total = entries.Count;
            int end   = System.Math.Min(offset + pageSize, total);

            var arr = new JArray();
            for (int i = offset; i < end; i++)
            {
                var e = entries[i];
                arr.Add(new JObject
                {
                    ["token"]        = e.Token,
                    ["type"]         = e.TypeFqn,
                    ["x"]            = e.Position.x,
                    ["y"]            = e.Position.y,
                    ["parent_token"] = e.ParentToken,
                    ["category"]     = e.Category,
                    ["block_index"]  = e.BlockIndex,
                });
            }

            var payload = new JObject
            {
                ["graph_path"] = path,
                ["total"]      = total,
                ["offset"]     = offset,
                ["page_size"]  = pageSize,
                ["nodes"]      = arr,
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }
    }
}
