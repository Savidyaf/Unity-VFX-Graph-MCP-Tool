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
// LoadGraph implementation note: VisualEffectResource is defined in Unity
// engine's UnityEditor.VFXModule assembly and is `internal` there, so the
// addon cannot reference it directly. The Lane 3C soft-fork bridge file at
// Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs exposes
// VfxMcpKernelHelpers.LoadGraphFromAsset(VisualEffectAsset) which compiles
// inside the package (and therefore has full access to VisualEffectResource)
// and returns a VFXGraph reachable to the addon via InternalsVisibleTo.
// This is a COMPILE-TIME bridge — zero runtime reflection.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.VFX;
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
            // Per spec: top-level nodes (direct children of the VFXGraph) have
            // parentFp = 0 ("" in the spec). Match VfxStructuralFingerprint.Compute.
            ulong parentFp = parent != null && !(parent is VFXGraph)
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

                // Per spec: top-level nodes have parentFp = 0; the VFXGraph root
                // is the recursion stop condition, not a participating parent.
                var parent = candidate.GetParent();
                ulong parentFp = parent != null && !(parent is VFXGraph)
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
                // Top-level nodes use parentFp=0 (see Mint).
                ulong newParentFp = newParent != null && !(newParent is VFXGraph)
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

        private static VFXGraph LoadGraph(string graphGuid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(graphGuid);
            if (string.IsNullOrEmpty(assetPath)) return null;

            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath);
            if (asset == null) return null;

            // Use the soft-fork bridge from
            // Packages/com.unity.visualeffectgraph/Editor/VfxMcpKernelHelpers.cs
            // — internal-static helper compiled inside Unity.VisualEffectGraph.Editor
            // that hides the engine-internal VisualEffectResource type behind a
            // public-friendly signature reachable via our InternalsVisibleTo grant.
            return VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
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
