// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Tools/VfxAssetTool.cs
//
// Phase 4 Lane A — VFX asset lifecycle tool.
//
// Actions: create, list, delete, assign.
// Mutating actions (create, delete, assign) gate on VfxBusyGate and open a
// VfxTransaction; read-only action (list) skips both.

using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Tools
{
    [McpForUnityTool("vfx_asset", AutoRegister = true, Group = "vfx")]
    public static class VfxAssetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string action = (@params?.Value<string>("action") ?? string.Empty).ToLowerInvariant();
                bool verbose = @params?.Value<bool?>("verbose") ?? false;
                return action switch
                {
                    "create" => Create(@params, verbose),
                    "list"   => List(@params, verbose),
                    "delete" => Delete(@params, verbose),
                    "assign" => Assign(@params, verbose),
                    _ => VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                    {
                        Code = "unknown_action",
                        Message = $"Unknown vfx_asset action '{action}'",
                        Hint = "Valid: create, list, delete, assign",
                    }),
                };
            }
            catch (VfxException ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                });
            }
            catch (System.Exception ex)
            {
                return VfxKernelContainer.Shaper.ShapeError(new VfxErrorEnvelope
                {
                    Code = "vfx_exception",
                    Message = ex.Message,
                });
            }
        }

        // ── ApplyInTransaction stub — lane C's batch dispatcher finds this via reflection ──
        internal static object ApplyInTransaction(JObject opParams, VfxTransactionScope scope)
        {
            throw new System.NotImplementedException(
                "VfxAssetTool.ApplyInTransaction will be wired in phase 4C batch integration");
        }

        // ── per-action private helpers ──

        private static object Create(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("path")
                ?? throw new VfxValidationException("missing_required_param", "path is required", null);

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

            VfxKernelContainer.BusyGate.EnsureIdle();

            // ERRATUM P-M5 + NEW-1: use the soft-fork bridge (VfxMcpKernelHelpers) to call
            // VisualEffectAssetEditorUtility, which is internal to Unity.VisualEffectGraph.Editor
            // and not directly reachable from this addon assembly. The bridge is compiled inside
            // the VFX editor assembly and exposed via InternalsVisibleTo — zero reflection.
            UnityEngine.Object asset = ext switch
            {
                ".vfx"         => UnityEditor.VFX.VfxMcpKernelHelpers.CreateVfxAsset(path),
                ".vfxblock"    => UnityEditor.VFX.VfxMcpKernelHelpers.CreateVfxSubgraphBlock(path),
                ".vfxoperator" => UnityEditor.VFX.VfxMcpKernelHelpers.CreateVfxSubgraphOperator(path),
                _ => throw new VfxValidationException("validation_error",
                    $"Unsupported extension: {ext}. Use .vfx, .vfxblock, or .vfxoperator.", null),
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

        private static object List(JObject @params, bool verbose)
        {
            // Read-only — skip BusyGate and transaction.
            var arr = new JArray();

            string[] types = new[]
            {
                "t:VisualEffectAsset",
                "t:VisualEffectSubgraphBlock",
                "t:VisualEffectSubgraphOperator",
            };

            string[] kindLabels = new[] { "vfx", "vfxblock", "vfxoperator" };

            for (int t = 0; t < types.Length; t++)
            {
                string[] guids = AssetDatabase.FindAssets(types[t]);
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    arr.Add(new JObject
                    {
                        ["guid"] = guid,
                        ["path"] = assetPath,
                        ["kind"] = kindLabels[t],
                    });
                }
            }

            return new JObject { ["assets"] = arr };
        }

        private static object Delete(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("path")
                ?? throw new VfxValidationException("missing_required_param", "path is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                bool deleted = AssetDatabase.DeleteAsset(path);
                if (!deleted)
                    throw new VfxValidationException("asset_not_found",
                        $"Asset at path '{path}' could not be deleted. It may not exist.", null);

                var commit = scope.Commit();
                return VfxKernelContainer.Shaper.Shape(commit, verbose);
            }
        }

        private static object Assign(JObject @params, bool verbose)
        {
            string path = @params.Value<string>("path")
                ?? throw new VfxValidationException("missing_required_param", "path is required", null);

            string gameObjectName = @params.Value<string>("gameObject")
                ?? throw new VfxValidationException("missing_required_param", "gameObject is required", null);

            VfxKernelContainer.BusyGate.EnsureIdle();

            string guid = AssetDatabase.AssetPathToGUID(path);
            using (var scope = VfxKernelContainer.Transaction.Begin(guid, path, VfxTransactionScopeKind.SingleCall))
            {
                GameObject go = GameObject.Find(gameObjectName);
                if (go == null)
                    throw new VfxValidationException("gameobject_not_found",
                        $"GameObject '{gameObjectName}' not found in the active scene.", null);

                VisualEffect comp = go.GetComponent<VisualEffect>();
                if (comp == null)
                    comp = go.AddComponent<VisualEffect>();

                VisualEffectAsset vfxAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (vfxAsset == null)
                    throw new VfxValidationException("asset_not_found",
                        $"VisualEffectAsset not found at path '{path}'.", null);

                comp.visualEffectAsset = vfxAsset;

                var commit = scope.Commit();
                if (!commit.Ok)
                    return VfxKernelContainer.Shaper.Shape(commit, verbose);

                return new JObject { ["assigned"] = path };
            }
        }
    }
}
