// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentity.cs
//
// Lane 3B task 3B-3: identity service that mints + resolves stable tokens
// for VFX Graph models. Identity lives entirely in the sidecar — the .vfx
// asset is never mutated. Implements IVfxIdentity from VfxKernelContracts.cs.
//
// Resolution algorithm:
//   1. Strict match: walk the graph, find the model whose structural
//      fingerprint equals the sidecar's stored fingerprint. Return on hit.
//   2. Recovery (per erratum P-H3): filter candidates by (typeFqn,
//      parentFingerprint) from the sidecar. If exactly one candidate matches,
//      update the sidecar with its new fingerprint and return it. If zero or
//      more than one match, throw VfxIdentityException("node_lost").
//
// The caller is responsible for emitting an "identity_drifted" warning when
// the recovery branch returns; we have no logger here on purpose.
//
// REFLECTION NOTE: VisualEffectResource is defined in Unity engine's
// UnityEditor.VFXModule assembly (NOT the package) and is `internal` there,
// so the InternalsVisibleTo patch on com.unity.visualeffectgraph cannot grant
// access to it. The only way to translate a graph GUID into a VFXGraph is to
// reflect on UnityEditor.VFX.VisualEffectResource.GetResourceAtPath. This is
// a one-time bootstrap, not a runtime hot-path on the catalog. The legacy
// addon code at Editor/Tools/Vfx/VfxGraphEdit.cs:1166 (GetGraph) uses the
// same pattern and was the only working approach on Unity 6.x at the time
// of the v0.3.0 rebuild. See lane 3B handover notes.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxIdentity : IVfxIdentity
    {
        private readonly VfxIdentitySidecar _sidecar;

        public VfxIdentity(VfxIdentitySidecar sidecar)
        {
            _sidecar = sidecar;
        }

        public string Mint(string graphGuid, VFXModel model)
        {
            if (model == null)
                throw new VfxIdentityException("node_lost", "Mint called with null model.");

            ulong fp = VfxStructuralFingerprint.Compute(graphGuid, model);
            string prefix = PrefixFor(model);
            string token = VfxStructuralFingerprint.ToShortToken(prefix, fp);

            string typeFqn = model.GetType().FullName;
            var parent = model.GetParent();
            ulong parentFp = parent != null
                ? VfxStructuralFingerprint.Compute(graphGuid, parent)
                : 0UL;

            _sidecar.Put(graphGuid, token, fp, typeFqn, parentFp);
            return token;
        }

        public VFXModel Resolve(string graphGuid, string token)
        {
            ulong expected = _sidecar.Get(graphGuid, token);
            if (expected == 0UL)
            {
                throw new VfxIdentityException("node_lost",
                    $"Token '{token}' not found in identity sidecar.",
                    new Dictionary<string, object> { ["token"] = token });
            }

            var graph = LoadGraph(graphGuid);
            if (graph == null)
            {
                throw new VfxIdentityException("node_lost",
                    $"Could not load graph for guid '{graphGuid}'.",
                    new Dictionary<string, object>
                    {
                        ["token"] = token,
                        ["graphGuid"] = graphGuid,
                    });
            }

            // 1. Strict match — fingerprint equality.
            foreach (var candidate in WalkAllModels(graph))
            {
                if (VfxStructuralFingerprint.Compute(graphGuid, candidate) == expected)
                    return candidate;
            }

            // 2. Recovery — filter by (typeFqn, parentFingerprint). Erratum P-H3.
            string expectedTypeFqn = _sidecar.GetTypeFqn(graphGuid, token);
            ulong expectedParentFp = _sidecar.GetParentFingerprint(graphGuid, token);

            // Legacy v1 sidecar entries lack type/parentFp; they will throw
            // node_lost here because there is no safe way to filter them.
            if (string.IsNullOrEmpty(expectedTypeFqn))
            {
                throw new VfxIdentityException("node_lost",
                    $"Token '{token}' has no type metadata; legacy v1 sidecar entry " +
                    $"cannot be recovered after structural drift.",
                    new Dictionary<string, object> { ["token"] = token });
            }

            var matches = new List<VFXModel>();
            foreach (var candidate in WalkAllModels(graph))
            {
                if (candidate.GetType().FullName != expectedTypeFqn)
                    continue;

                var parent = candidate.GetParent();
                ulong parentFp = parent != null
                    ? VfxStructuralFingerprint.Compute(graphGuid, parent)
                    : 0UL;
                if (parentFp != expectedParentFp)
                    continue;

                matches.Add(candidate);
            }

            if (matches.Count == 1)
            {
                // Update the sidecar with the new fingerprint so the next
                // resolve hits the strict-match path. Caller emits the
                // "identity_drifted" warning.
                var match = matches[0];
                ulong newFp = VfxStructuralFingerprint.Compute(graphGuid, match);
                var newParent = match.GetParent();
                ulong newParentFp = newParent != null
                    ? VfxStructuralFingerprint.Compute(graphGuid, newParent)
                    : 0UL;
                _sidecar.Put(graphGuid, token, newFp, match.GetType().FullName, newParentFp);
                return match;
            }

            throw new VfxIdentityException("node_lost",
                $"Token '{token}' could not be recovered: " +
                $"{matches.Count} matches by (type='{expectedTypeFqn}', parentFp='{expectedParentFp:x16}').",
                new Dictionary<string, object>
                {
                    ["token"] = token,
                    ["matchCount"] = matches.Count,
                });
        }

        public void Flush() => _sidecar.Flush();

        // ---- helpers ----

        private static string PrefixFor(VFXModel m)
        {
            // Order matters: VFXBlock derives from VFXSlotContainerModel, not from
            // VFXContext or VFXOperator, so the type checks are independent.
            if (m is VFXBlock) return "b_";
            if (m is VFXContext) return "ctx_";
            if (m is VFXParameter) return "n_";
            if (m is VFXOperator) return "n_";
            return "n_";
        }

        // ── reflection cache (Unity 6 fallback) ──
        private static Type s_VisualEffectResourceType;
        private static MethodInfo s_GetResourceAtPathMethod;
        private static Type s_ExtensionsType;
        private static MethodInfo s_GetOrCreateGraphMethod;

        private static VFXGraph LoadGraph(string graphGuid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(graphGuid);
            if (string.IsNullOrEmpty(assetPath)) return null;

            EnsureReflectionCache();
            if (s_GetResourceAtPathMethod == null || s_GetOrCreateGraphMethod == null)
                return null;

            UnityEngine.Object resource;
            try
            {
                resource = s_GetResourceAtPathMethod.Invoke(null, new object[] { assetPath })
                    as UnityEngine.Object;
            }
            catch
            {
                return null;
            }
            if (resource == null) return null;

            try
            {
                return s_GetOrCreateGraphMethod.Invoke(null, new object[] { resource }) as VFXGraph;
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureReflectionCache()
        {
            if (s_GetResourceAtPathMethod != null && s_GetOrCreateGraphMethod != null)
                return;

            // VisualEffectResource lives in Unity engine's UnityEditor.VFXModule
            // assembly and is `internal` there. There is no way to grant access
            // via InternalsVisibleTo because we don't own UnityEditor.VFXModule.
            // We resolve it by walking loaded assemblies once and caching the
            // MethodInfo handles. This mirrors what
            // Editor/Tools/Vfx/VfxGraphEdit.cs:GetGraph does in legacy addon code.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (s_VisualEffectResourceType == null
                    && asm.GetName().Name == "UnityEditor.VFXModule")
                {
                    s_VisualEffectResourceType = asm.GetType("UnityEditor.VFX.VisualEffectResource");
                }
                if (s_ExtensionsType == null
                    && asm.GetName().Name == "Unity.VisualEffectGraph.Editor")
                {
                    s_ExtensionsType = asm.GetType("UnityEditor.VFX.VisualEffectResourceExtensions");
                }
                if (s_VisualEffectResourceType != null && s_ExtensionsType != null)
                    break;
            }

            if (s_VisualEffectResourceType != null)
            {
                s_GetResourceAtPathMethod = s_VisualEffectResourceType.GetMethod(
                    "GetResourceAtPath",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (s_ExtensionsType != null)
            {
                s_GetOrCreateGraphMethod = s_ExtensionsType.GetMethod(
                    "GetOrCreateGraph",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        private static IEnumerable<VFXModel> WalkAllModels(VFXModel root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.children)
            {
                foreach (var descendant in WalkAllModels(child))
                    yield return descendant;
            }
        }
    }
}
