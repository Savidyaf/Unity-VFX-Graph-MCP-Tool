// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxPropertyTool.cs
//
// Phase 4B — VFX graph property (parameter / exposed property) tool.
//
// Actions (4):
//   Mutating : add, remove, set_value, set_exposed
//
// Pattern follows VfxAssetTool.cs exactly (Lane 4A reference).

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using System.Collections.Generic;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_property", AutoRegister = true, Group = "vfx")]
    public static class VfxPropertyTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose  = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "add"          => Add(@params, verbose),
                    "remove"       => Remove(@params, verbose),
                    "set_value"    => SetValue(@params, verbose),
                    "set_exposed"  => SetExposed(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"Unknown vfx_property action '{action}'",
                        Hint    = "Valid: add, remove, set_value, set_exposed",
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

        // ── ApplyInTransaction — F9 batch dispatch entry point ──
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            string action = (opParams?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
            return action switch
            {
                "add"         => AddInner(opParams, scope),
                "remove"      => RemoveInner(opParams, scope),
                "set_value"   => SetValueInner(opParams, scope),
                "set_exposed" => SetExposedInner(opParams, scope),
                _ => throw new System.NotImplementedException(
                    $"vfx_property action '{action}' not supported in batch"),
            };
        }

        // ────────────────────────── add ──────────────────────────

        private static object Add(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)AddInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object AddInner(JObject @params, VfxTransactionScope scope)
        {
            string path    = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string typeFqn = @params.Value<string>("type")
                ?? throw new VfxValidationException("missing_required_param", "type is required", null);
            float x = @params.Value<float?>("x") ?? 0f;
            float y = @params.Value<float?>("y") ?? 0f;

            string token = VfxKernelContainer.NodeOps.AddParameter(
                path, typeFqn, new UnityEngine.Vector2(x, y));

            scope.Record(new VfxIntentOp
            {
                OpIndex         = 0,
                Kind            = "add",
                ExpectedToken   = token,
                ExpectedTypeFqn = typeFqn,
                Payload         = new Dictionary<string, object> { ["x"] = x, ["y"] = y },
            });

            return new JObject
            {
                ["added"] = new JArray(new JObject
                {
                    ["token"] = token,
                    ["type"]  = typeFqn,
                }),
            };
        }

        // ────────────────────────── remove ──────────────────────────

        private static object Remove(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)RemoveInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object RemoveInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);

            VfxKernelContainer.NodeOps.RemoveNode(path, token);

            scope.Record(new VfxIntentOp
            {
                OpIndex       = 0,
                Kind          = "remove",
                ExpectedToken = token,
                Payload       = new Dictionary<string, object> { ["token"] = token },
            });

            return new JObject
            {
                ["removed"] = new JArray(new JObject { ["token"] = token }),
            };
        }

        // ────────────────────────── set_value ──────────────────────────
        // Sets the parameter's value slot (named "value" on VFXParameter output slot).

        private static object SetValue(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)SetValueInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object SetValueInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            // NodeOps.SetProperty walks inputSlots then outputSlots by name "value".
            // VFXParameter has an output slot whose property.name is the type name.
            // We pass "value" and let GetProperty fall through to output slots.
            VfxKernelContainer.NodeOps.SetProperty(path, token, "value", value);

            scope.Record(new VfxIntentOp
            {
                OpIndex       = 0,
                Kind          = "set_property",
                ExpectedToken = token,
                Payload       = new Dictionary<string, object>
                {
                    ["name"]  = "value",
                    ["value"] = value,
                },
            });

            return new JObject
            {
                ["set_value"] = new JObject
                {
                    ["token"] = token,
                    ["value"] = value?.ToString(),
                },
            };
        }

        // ────────────────────────── set_exposed ──────────────────────────
        // VFXParameter.m_Exposed is a [VFXSetting(VisibleFlags.InInspector)] bool field
        // — verified VFXParameter.cs:58. Accessible via SetSetting("m_Exposed", bool).

        private static object SetExposed(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)SetExposedInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object SetExposedInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            bool exposed = @params.Value<bool?>("exposed")
                ?? throw new VfxValidationException("missing_required_param", "exposed is required", null);

            // "m_Exposed" — VFXParameter.cs:58 — [VFXSetting(VisibleFlags.InInspector)]
            // NodeOps.SetSetting matches "m_Exposed" or "Exposed" (m_ prefix fallback).
            VfxKernelContainer.NodeOps.SetSetting(path, token, "m_Exposed", exposed);

            scope.Record(new VfxIntentOp
            {
                OpIndex       = 0,
                Kind          = "set_setting",
                ExpectedToken = token,
                Payload       = new Dictionary<string, object>
                {
                    ["name"]  = "m_Exposed",
                    ["value"] = exposed,
                },
            });

            return new JObject
            {
                ["set_exposed"] = new JObject
                {
                    ["token"]   = token,
                    ["exposed"] = exposed,
                },
            };
        }
    }
}
