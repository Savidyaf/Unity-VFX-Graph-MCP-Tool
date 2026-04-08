// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxSubgraphTool.cs
//
// Phase 4C — VFX subgraph reference tool.
//
// Actions (6):
//   Mutating : create, add_ref, set_override
//   Deferred : inline (v0.3.1), extract (v0.3.1)
//   Read-only: get_exposed
//
// create     — creates a new .vfxblock or .vfxoperator asset on disk via the
//              soft-fork bridge.  No transaction needed (asset creation, not
//              graph mutation).
// add_ref    — opens a VfxTransaction, calls NodeOps.AddSubgraphRef, records
//              intent, commits + shapes.
// inline     — throws NotImplementedException("v0.3.1") per erratum A-H2.
// extract    — throws NotImplementedException("v0.3.1") per erratum A-H2.
// get_exposed — read-only stub; returns an empty exposed array with a Phase 5 note.
// set_override— delegates to NodeOps.SetProperty (subgraph overrides ride the
//              standard slot system).
//
// ApplyInTransaction — REAL implementation for the "add_ref" op only; the
// VfxBatchTool dispatcher calls it to batch subgraph adds.

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
    [McpForUnityTool("vfx_subgraph", AutoRegister = true, Group = "vfx")]
    public static class VfxSubgraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "create"       => Create(@params, verbose),
                    "add_ref"      => AddRef(@params, verbose),
                    "inline"       => throw new System.NotImplementedException("v0.3.1"),
                    "extract"      => throw new System.NotImplementedException("v0.3.1"),
                    "get_exposed"  => GetExposed(@params, verbose),
                    "set_override" => SetOverride(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code    = "unknown_action",
                        Message = $"Unknown vfx_subgraph action '{action}'",
                        Hint    = "Valid: create, add_ref, inline, extract, get_exposed, set_override",
                    }),
                };
            }
            catch (System.NotImplementedException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code    = "not_implemented",
                    Message = ex.Message,
                    Hint    = "This action is scheduled for a future release.",
                });
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

        // ── ApplyInTransaction — F9 batch dispatch entry point ──────────────────
        // VfxBatchTool discovers this via reflection and calls it inside an active
        // VfxTransactionScope. Only transaction-backed actions are wired here —
        // "create" is an asset-creation op, not a graph mutation, so it does not
        // participate in batches.
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            string action = (opParams?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
            return action switch
            {
                "add_ref"      => AddRefInner(opParams, scope),
                "set_override" => SetOverrideInner(opParams, scope),
                _ => throw new System.NotImplementedException(
                    $"vfx_subgraph action '{action}' not supported in batch"),
            };
        }

        // ── per-action private helpers ─────────────────────────────────────────

        private static object Create(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("path")
                ?? throw new VfxValidationException("missing_required_param", "path is required", null);

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

            VfxKernelContainer.BusyGate.EnsureIdle();

            // Delegate to the soft-fork bridge — these methods are compiled inside
            // Unity.VisualEffectGraph.Editor and exposed via InternalsVisibleTo.
            // Zero reflection on UnityEditor.VFX.* types.
            UnityEngine.Object asset = ext switch
            {
                ".vfxblock"    => VfxMcpKernelHelpers.CreateVfxSubgraphBlock(path),
                ".vfxoperator" => VfxMcpKernelHelpers.CreateVfxSubgraphOperator(path),
                _ => throw new VfxValidationException("validation_error",
                    $"path must end in .vfxblock or .vfxoperator; got '{ext}'", null),
            };

            AssetDatabase.SaveAssets();

            return new JObject
            {
                ["created"] = new JObject
                {
                    ["path"] = path,
                    ["kind"] = ext.TrimStart('.'),
                },
            };
        }

        private static object AddRef(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)AddRefInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object AddRefInner(JObject @params, VfxTransactionScope scope)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string subgraph = @params.Value<string>("subgraph")
                ?? throw new VfxValidationException("missing_required_param", "subgraph is required", null);
            float x = @params.Value<float?>("x") ?? 0f;
            float y = @params.Value<float?>("y") ?? 0f;

            string token = VfxKernelContainer.NodeOps.AddSubgraphRef(path, subgraph, new Vector2(x, y));

            scope.Record(new VfxIntentOp
            {
                OpIndex         = 0,
                Kind            = "add",
                ExpectedToken   = token,
                ExpectedTypeFqn = "UnityEditor.VFX.VFXSubgraphRef",
                Payload         = new Dictionary<string, object>
                {
                    ["subgraph"] = subgraph,
                    ["x"] = x,
                    ["y"] = y,
                },
            });

            return new JObject
            {
                ["added"] = new JObject { ["token"] = token, ["subgraph"] = subgraph },
            };
        }

        private static object GetExposed(JObject @params, bool verbose)
        {
            // Read-only — skip BusyGate and transaction.
            // Phase 5: enumerate VFXSubgraphOperator/Block/Context exposed inputs.
            var payload = new JObject
            {
                ["exposed"] = new JArray(),
                ["note"]    = "Phase 5: enumerate VFXSubgraphOperator/Block/Context exposed inputs",
            };
            return VfxKernelContainer.Shaper.ShapeRead(payload, verbose);
        }

        private static object SetOverride(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();
            string guid = AssetDatabase.AssetPathToGUID(path);

            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                var payload = (JObject)SetOverrideInner(@params, scope);
                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.ShapeMutation(commit, verbose, payload);
            }
        }

        internal static object SetOverrideInner(JObject @params, VfxTransactionScope scope)
        {
            string path  = @params.Value<string>("graph")
                ?? throw new VfxValidationException("missing_required_param", "graph is required", null);
            string token = @params.Value<string>("token")
                ?? throw new VfxValidationException("missing_required_param", "token is required", null);
            string name  = @params.Value<string>("name")
                ?? throw new VfxValidationException("missing_required_param", "name is required", null);
            object value = @params["value"]?.ToObject<object>()
                ?? throw new VfxValidationException("missing_required_param", "value is required", null);

            // Subgraph overrides ride the standard slot system.
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

            return new JObject
            {
                ["set_override"] = new JObject
                {
                    ["token"] = token,
                    ["name"]  = name,
                    ["value"] = value?.ToString(),
                },
            };
        }
    }
}
