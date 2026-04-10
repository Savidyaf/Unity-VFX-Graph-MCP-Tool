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

        // ── ApplyInTransaction — F9 batch dispatch entry point ──
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            string action = (opParams?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
            return action switch
            {
                "add"            => AddInner(opParams, scope),
                "remove"         => RemoveInner(opParams, scope),
                "reorder"        => ReorderInner(opParams, scope),
                "set_activation" => SetActivationInner(opParams, scope),
                "set_attribute"  => SetAttributeInner(opParams, scope),
                _ => throw new System.NotImplementedException(
                    $"vfx_block action '{action}' not supported in batch"),
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
            string path         = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string parentToken  = @params.Value<string>("parent_token")
                ?? throw new VfxValidationException("missing_required_param", "parent_token is required", null);
            string typeFqn      = @params.Value<string>("type")
                ?? throw new VfxValidationException("missing_required_param", "type is required", null);
            int index = @params.Value<int?>("index") ?? -1;

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

            return new JObject
            {
                ["added"] = new JArray(new JObject
                {
                    ["token"]        = token,
                    ["type"]         = typeFqn,
                    ["parent_token"] = parentToken,
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

        // ────────────────────────── reorder ──────────────────────────

        private static object Reorder(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)ReorderInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object ReorderInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            int newIndex = @params.Value<int?>("new_index")
                ?? throw new VfxValidationException("missing_required_param", "new_index is required", null);

            string guid = AssetDatabase.AssetPathToGUID(path);
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
            // C1 safety fix: capture the original index first so we can restore
            // the block if AddChild throws (e.g. out-of-bounds new_index). Without
            // this guard a failed reorder leaves the block detached and lost.
            int originalIndex = parent.GetIndex(block);
            parent.RemoveChild(block, notify: false);
            try
            {
                parent.AddChild(block, newIndex, notify: true);
            }
            catch
            {
                // Restore the block at its original position and re-throw so the
                // caller still sees the validation error from the kernel.
                parent.AddChild(block, originalIndex, notify: true);
                throw;
            }

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

            return new JObject
            {
                ["reordered"] = new JObject { ["token"] = token, ["new_index"] = newIndex },
            };
        }

        // ────────────────────────── set_activation ──────────────────────────
        // VFXBlock.enabled has no setter (get-only, VFXBlock.cs:37).
        // Activation is controlled via the activationSlot (name: "_vfx_enabled"),
        // which is a bool VFXSlot. Setting slot.value = true/false enables/disables.
        // VFXBlock.activationSlotName = "_vfx_enabled" — verified VFXBlock.cs:11.

        private static object SetActivation(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)SetActivationInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object SetActivationInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            bool active = @params.Value<bool?>("active")
                ?? throw new VfxValidationException("missing_required_param", "active is required", null);

            string guid = AssetDatabase.AssetPathToGUID(path);
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

            return new JObject
            {
                ["set_activation"] = new JObject
                {
                    ["token"]  = token,
                    ["active"] = active,
                },
            };
        }

        // ────────────────────────── set_attribute ──────────────────────────

        private static object SetAttribute(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)SetAttributeInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object SetAttributeInner(JObject @params, VfxTransactionScope scope)
        {
            string path           = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token          = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string attributeName  = @params.Value<string>("attribute_name")
                ?? throw new VfxValidationException("missing_required_param", "attribute_name is required", null);
            object value          = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            // W2-B fix: SetAttribute blocks expose the attribute name as a [VFXSetting]
            // field called "attribute" (UnityEditor.VFX.Block.SetAttribute.attribute).
            // The value lives on an input slot whose name is derived from the attribute
            // name via "_" + capitalised first char + remainder (e.g. "velocity" → "_Velocity").
            // See SetAttribute.cs::GenerateLocalAttributeName + inputProperties.
            // Step 1: persist the setting so the block re-syncs its slots for the chosen attribute.
            VfxKernelContainer.NodeOps.SetSetting(path, token, "attribute", attributeName);

            // Step 2: write the value into the local-name slot. Mirrors GenerateLocalAttributeName.
            string slotName = "_" + char.ToUpperInvariant(attributeName[0]) + attributeName.Substring(1);
            VfxKernelContainer.NodeOps.SetProperty(path, token, slotName, value);

            scope.Record(new VfxIntentOp
            {
                OpIndex       = 0,
                Kind          = "set_setting",
                ExpectedToken = token,
                Payload       = new Dictionary<string, object>
                {
                    ["name"]  = "attribute",
                    ["value"] = attributeName,
                },
            });
            scope.Record(new VfxIntentOp
            {
                OpIndex       = 1,
                Kind          = "set_property",
                ExpectedToken = token,
                Payload       = new Dictionary<string, object>
                {
                    ["name"]  = slotName,
                    ["value"] = value,
                },
            });

            return new JObject
            {
                ["set_attribute"] = new JObject
                {
                    ["token"]          = token,
                    ["attribute_name"] = attributeName,
                    ["slot_name"]      = slotName,
                    ["value"]          = value?.ToString(),
                },
            };
        }

        // ────────────────────────── list_attributes (read-only stub) ──────────────────────────

        private static object ListAttributes(JObject @params, bool verbose)
        {
            // Phase 5 deferred — rich attribute listing belongs to the diagnostics lane.
            // W2-B fix: return the canonical {state, hint} stub envelope rather than the
            // ad-hoc {note} shape, which downstream consumers cannot reliably classify.
            return VfxKernelContainer.Shaper.ShapeRead(
                new JObject
                {
                    ["state"] = "not_implemented",
                    ["hint"]  = "Use vfx_diag.list_attributes for the catalog of built-in and per-graph attributes.",
                },
                verbose);
        }
    }
}
