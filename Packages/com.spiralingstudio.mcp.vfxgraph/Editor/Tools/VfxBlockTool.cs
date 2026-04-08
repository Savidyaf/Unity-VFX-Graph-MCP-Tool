// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxBlockTool.cs
//
// Phase 4B — VFX block manipulation tool.
//
// Actions (6):
//   Mutating : add, remove, reorder, set_activation, set_attribute
//   Read-only: list_attributes (stub — deferred to Phase 5 / vfx_diag)
//
// Pattern follows VfxAssetTool.cs exactly (Lane 4A reference).

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEditor.VFX;
using System.Collections.Generic;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_block", AutoRegister = true, Group = "vfx")]
    public static class VfxBlockTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose  = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "add"              => Add(@params, verbose),
                    "remove"           => Remove(@params, verbose),
                    "reorder"          => Reorder(@params, verbose),
                    "set_activation"   => SetActivation(@params, verbose),
                    "set_attribute"    => SetAttribute(@params, verbose),
                    "list_attributes"  => ListAttributes(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"Unknown vfx_block action '{action}'",
                        Hint    = "Valid: add, remove, reorder, set_activation, set_attribute, list_attributes",
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
            string path         = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string parentToken  = @params.Value<string>("parent_token")
                ?? throw new VfxValidationException("missing_required_param", "parent_token is required", null);
            string typeFqn      = @params.Value<string>("type")
                ?? throw new VfxValidationException("missing_required_param", "type is required", null);
            int index = @params.Value<int?>("index") ?? -1;

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                string token = VfxKernelContainer.NodeOps.AddBlock(path, parentToken, typeFqn, index);

                scope.Record(new VfxIntentOp
                {
                    OpIndex         = 0,
                    Kind            = "add",
                    ExpectedToken   = token,
                    ExpectedTypeFqn = typeFqn,
                    ParentToken     = parentToken,
                    Payload         = new Dictionary<string, object>
                    {
                        ["parent_token"] = parentToken,
                        ["index"]        = index,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["added"] = new JArray(new JObject
                    {
                        ["token"]        = token,
                        ["type"]         = typeFqn,
                        ["parent_token"] = parentToken,
                    });
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

        // ────────────────────────── reorder ──────────────────────────

        private static object Reorder(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            int newIndex = @params.Value<int?>("new_index")
                ?? throw new VfxValidationException("missing_required_param", "new_index is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var model = VfxKernelContainer.Identity.Resolve(guid, token);
                if (model == null)
                    throw new VfxIdentityException("node_lost",
                        $"Token {token} not found in {path}", null);

                var block = model as VFXBlock;
                if (block == null)
                    throw new VfxValidationException("invalid_node",
                        $"Token {token} ({model.GetType().Name}) is not a VFXBlock", null);

                var parent = block.GetParent() as VFXContext;
                if (parent == null)
                    throw new VfxValidationException("invalid_parent",
                        $"Block {token} has no parent VFXContext", null);

                // Remove then re-insert at the new index.
                // VFXModel.RemoveChild + AddChild — both verified in VFXModel.cs:165,145
                parent.RemoveChild(block, notify: false);
                parent.AddChild(block, newIndex, notify: true);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object>
                    {
                        ["token"]     = token,
                        ["new_index"] = newIndex,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["reordered"] = new JObject { ["token"] = token, ["new_index"] = newIndex };
                return shaped;
            }
        }

        // ────────────────────────── set_activation ──────────────────────────
        // VFXBlock.enabled has no setter (get-only, VFXBlock.cs:37).
        // Activation is controlled via the activationSlot (name: "_vfx_enabled"),
        // which is a bool VFXSlot. Setting slot.value = true/false enables/disables.
        // VFXBlock.activationSlotName = "_vfx_enabled" — verified VFXBlock.cs:11.

        private static object SetActivation(JObject @params, bool verbose)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            bool active = @params.Value<bool?>("active")
                ?? throw new VfxValidationException("missing_required_param", "active is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var model = VfxKernelContainer.Identity.Resolve(guid, token);
                if (model == null)
                    throw new VfxIdentityException("node_lost",
                        $"Token {token} not found in {path}", null);

                var block = model as VFXBlock;
                if (block == null)
                    throw new VfxValidationException("invalid_node",
                        $"Token {token} ({model.GetType().Name}) is not a VFXBlock", null);

                // activationSlot is the bool slot named "_vfx_enabled" (VFXBlock.cs:11,33)
                var activSlot = block.activationSlot;
                if (activSlot == null)
                    throw new VfxValidationException("slot_not_found",
                        $"Block {token} ({block.GetType().Name}) has no activationSlot", null);

                activSlot.value = active;

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_setting",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object>
                    {
                        ["token"]  = token,
                        ["active"] = active,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["set_activation"] = new JObject
                    {
                        ["token"]  = token,
                        ["active"] = active,
                    };
                return shaped;
            }
        }

        // ────────────────────────── set_attribute ──────────────────────────

        private static object SetAttribute(JObject @params, bool verbose)
        {
            string path           = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token          = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string attributeName  = @params.Value<string>("attribute_name")
                ?? throw new VfxValidationException("missing_required_param", "attribute_name is required", null);
            object value          = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                // Delegate to NodeOps.SetProperty — operates on input slots by name.
                VfxKernelContainer.NodeOps.SetProperty(path, token, attributeName, value);

                scope.Record(new VfxIntentOp
                {
                    OpIndex       = 0,
                    Kind          = "set_property",
                    ExpectedToken = token,
                    Payload       = new Dictionary<string, object>
                    {
                        ["attribute_name"] = attributeName,
                        ["value"]          = value,
                    },
                });

                var commit = scope.Commit();
                var shaped = VfxKernelContainer.Shaper.Shape(commit, verbose);
                if (commit.Ok && shaped is JObject obj)
                    obj["set_attribute"] = new JObject
                    {
                        ["token"]          = token,
                        ["attribute_name"] = attributeName,
                        ["value"]          = value?.ToString(),
                    };
                return shaped;
            }
        }

        // ────────────────────────── list_attributes (read-only stub) ──────────────────────────

        private static object ListAttributes(JObject @params, bool verbose)
        {
            // Phase 5 deferred — rich attribute listing belongs to the diagnostics lane.
            return VfxKernelContainer.Shaper.ShapeRead(
                new Newtonsoft.Json.Linq.JObject
                {
                    ["note"] = "Phase 5: use vfx_diag.list_attributes",
                },
                verbose);
        }
    }
}
