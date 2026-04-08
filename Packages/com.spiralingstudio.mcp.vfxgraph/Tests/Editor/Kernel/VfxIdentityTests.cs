// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxIdentityTests.cs
//
// Lane 3B task 3B-3: mint, resolve, and recovery tests for VfxIdentity.
// We use a unique temp sidecar path per test (NOT Library/VfxMcpIdentity.json)
// so parallel-running test workers cannot collide.
//
// The fixture creates a real .vfx asset under Assets/Tests/VfxFixtures/ via
// VisualEffectAssetEditorUtility.CreateNew<VisualEffectAsset>(path), then
// adds operators by calling VFXLibrary descriptors directly. Both the helper
// and VFXLibrary are visible because the embedded VFX Graph package's
// AssemblyInfo.cs grants InternalsVisibleTo to this assembly.
//
// REFLECTION NOTE: VisualEffectResource is in Unity engine's
// UnityEditor.VFXModule assembly and is `internal` there. We cannot grant
// InternalsVisibleTo on assemblies we don't own, so the test fixture
// resolves a VFXGraph from a freshly-created VisualEffectAsset via the same
// reflection path that VfxIdentity.LoadGraph uses internally. See the
// detailed comment in VfxIdentity.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxIdentityTests
    {
        private const string FixtureDir = "Assets/Tests/VfxFixtures";
        private string _assetPath;
        private string _tempSidecarPath;
        private VisualEffectAsset _asset;
        private VFXGraph _graph;
        private string _graphGuid;

        [SetUp]
        public void SetUp()
        {
            _tempSidecarPath = Path.Combine(
                Path.GetTempPath(),
                "VfxMcpIdentityTest_" + Guid.NewGuid().ToString("n") + ".json");

            if (!Directory.Exists(FixtureDir))
                Directory.CreateDirectory(FixtureDir);
            // Reflect-and-import the directory into Unity so AssetDatabase sees it.
            AssetDatabase.Refresh();

            _assetPath = $"{FixtureDir}/IdentityTest_{Guid.NewGuid():N}.vfx";
            _asset = VisualEffectAssetEditorUtility.CreateNewAsset(_assetPath);
            Assert.IsNotNull(_asset, $"CreateNewAsset returned null for path '{_assetPath}'");
            _graph = LoadGraphReflectively(_assetPath);
            Assert.IsNotNull(_graph, "Failed to load VFXGraph from freshly-created asset.");
            _graphGuid = AssetDatabase.AssetPathToGUID(_assetPath);
            Assert.IsFalse(string.IsNullOrEmpty(_graphGuid), "Failed to obtain asset GUID");
        }

        // See the file header note. We mirror VfxIdentity.LoadGraph here so the
        // test never references VisualEffectResource directly. Cached statically
        // for fixture-pool reuse.
        private static MethodInfo s_GetResourceAtPath;
        private static MethodInfo s_GetOrCreateGraph;

        private static VFXGraph LoadGraphReflectively(string assetPath)
        {
            if (s_GetResourceAtPath == null || s_GetOrCreateGraph == null)
            {
                Type resourceType = null;
                Type extensionsType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = asm.GetName().Name;
                    if (resourceType == null && asmName == "UnityEditor.VFXModule")
                        resourceType = asm.GetType("UnityEditor.VFX.VisualEffectResource");
                    if (extensionsType == null && asmName == "Unity.VisualEffectGraph.Editor")
                        extensionsType = asm.GetType("UnityEditor.VFX.VisualEffectResourceExtensions");
                    if (resourceType != null && extensionsType != null) break;
                }
                Assert.IsNotNull(resourceType,
                    "Could not locate UnityEditor.VFX.VisualEffectResource via reflection.");
                Assert.IsNotNull(extensionsType,
                    "Could not locate UnityEditor.VFX.VisualEffectResourceExtensions via reflection.");
                s_GetResourceAtPath = resourceType.GetMethod("GetResourceAtPath",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                s_GetOrCreateGraph = extensionsType.GetMethod("GetOrCreateGraph",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(s_GetResourceAtPath, "GetResourceAtPath method not found.");
                Assert.IsNotNull(s_GetOrCreateGraph, "GetOrCreateGraph method not found.");
            }

            var resource = s_GetResourceAtPath.Invoke(null, new object[] { assetPath }) as UnityEngine.Object;
            if (resource == null) return null;
            return s_GetOrCreateGraph.Invoke(null, new object[] { resource }) as VFXGraph;
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_assetPath) && File.Exists(_assetPath))
            {
                AssetDatabase.DeleteAsset(_assetPath);
            }
            if (!string.IsNullOrEmpty(_tempSidecarPath) && File.Exists(_tempSidecarPath))
            {
                try { File.Delete(_tempSidecarPath); }
                catch { /* best effort */ }
            }
        }

        // Add a fresh VFXOperator to the graph using the first available descriptor.
        // We deliberately avoid the GraphView controller path here because the
        // tests don't need its UI bookkeeping — they only need a live VFXModel.
        private VFXOperator AddOperator()
        {
            var descriptor = VFXLibrary.GetOperators().First();
            var op = descriptor.CreateInstance();
            op.position = new Vector2(0, 0);
            _graph.AddChild(op);
            return op;
        }

        [Test]
        public void Mint_ReturnsTokenStartingWithPrefix_AndIs7Chars()
        {
            var op = AddOperator();
            var sidecar = new VfxIdentitySidecar(_tempSidecarPath);
            var identity = new VfxIdentity(sidecar);

            var token = identity.Mint(_graphGuid, op);

            Assert.IsTrue(token.StartsWith("n_"),
                $"Expected operator token to start with 'n_', got '{token}'");
            Assert.AreEqual(7, token.Length,
                $"Expected 'n_' + 5 hex chars = 7, got '{token}'");
        }

        [Test]
        public void Resolve_RoundTrip_StrictMatch()
        {
            var op = AddOperator();
            var sidecar = new VfxIdentitySidecar(_tempSidecarPath);
            var identity = new VfxIdentity(sidecar);

            var token = identity.Mint(_graphGuid, op);
            var resolved = identity.Resolve(_graphGuid, token);

            Assert.AreSame(op, resolved,
                "Resolve should return the same instance after a no-drift round trip.");
        }

        [Test]
        public void Resolve_NodeRemoved_ThrowsNodeLost()
        {
            var op = AddOperator();
            var sidecar = new VfxIdentitySidecar(_tempSidecarPath);
            var identity = new VfxIdentity(sidecar);
            var token = identity.Mint(_graphGuid, op);

            // Remove the operator from the graph; the strict-match path fails
            // and recovery has zero candidates.
            _graph.RemoveChild(op);

            var ex = Assert.Throws<VfxIdentityException>(
                () => identity.Resolve(_graphGuid, token));
            Assert.AreEqual("node_lost", ex.Code);
        }

        [Test]
        public void Resolve_RecoveryAfterSiblingDrift_FindsByTypeAndParent()
        {
            // Per erratum P-H3: when the structural fingerprint changes (e.g.
            // sibling-index drift caused by inserting an unrelated sibling at
            // a lower index), recovery filters by (typeFqn, parentFingerprint)
            // and accepts a single match.
            var op = AddOperator();

            var sidecar = new VfxIdentitySidecar(_tempSidecarPath);
            var identity = new VfxIdentity(sidecar);
            var token = identity.Mint(_graphGuid, op);

            // Cause drift: add another operator of a DIFFERENT type before
            // re-resolving. We don't want a same-type sibling because that
            // would yield 2 candidates and the recovery path requires
            // exactly one match. Many VFXOperator descriptors share the same
            // type (e.g., VFXInlineOperator with different m_Type SerializableType
            // values still has typeof = VFXInlineOperator), so we filter for
            // distinct types until we find one.
            VFXOperator other = null;
            foreach (var descriptor in VFXLibrary.GetOperators())
            {
                var candidate = descriptor.CreateInstance();
                if (candidate.GetType() != op.GetType())
                {
                    other = candidate;
                    break;
                }
                UnityEngine.Object.DestroyImmediate(candidate);
            }
            Assume.That(other, Is.Not.Null,
                "Need at least two operator descriptors of different C# types.");
            other.position = new Vector2(100, 100);
            _graph.AddChild(other);

            // Forge a different fingerprint into the sidecar to simulate drift
            // *without* removing the original operator. The strict-match path
            // misses, recovery filters by (type, parent), and there is exactly
            // one match (the original op). Recovery returns it.
            sidecar.Put(_graphGuid, token, 0xDEADBEEFCAFEBABEUL,
                op.GetType().FullName, 0UL);

            var resolved = identity.Resolve(_graphGuid, token);
            Assert.AreSame(op, resolved,
                "Recovery should locate the original op by (typeFqn, parentFp).");

            // Sidecar must be updated with the operator's NEW fingerprint, so
            // a subsequent strict-match resolve succeeds without recovery.
            ulong updatedFp = VfxStructuralFingerprint.Compute(_graphGuid, op);
            Assert.AreEqual(updatedFp, sidecar.Get(_graphGuid, token));
        }

        [Test]
        public void Resolve_RecoveryFiltersOutWrongType_ThrowsNodeLost()
        {
            // If the sidecar has a typeFqn that no model in the graph matches,
            // recovery returns zero candidates and we throw node_lost.
            var op = AddOperator();
            var sidecar = new VfxIdentitySidecar(_tempSidecarPath);
            var identity = new VfxIdentity(sidecar);
            var token = identity.Mint(_graphGuid, op);

            // Force a fingerprint mismatch and a typeFqn that does not match
            // anything in the graph.
            sidecar.Put(_graphGuid, token, 0xDEADBEEFCAFEBABEUL,
                "UnityEditor.VFX.NoSuchType", 0UL);

            var ex = Assert.Throws<VfxIdentityException>(
                () => identity.Resolve(_graphGuid, token));
            Assert.AreEqual("node_lost", ex.Code);
        }
    }
}
