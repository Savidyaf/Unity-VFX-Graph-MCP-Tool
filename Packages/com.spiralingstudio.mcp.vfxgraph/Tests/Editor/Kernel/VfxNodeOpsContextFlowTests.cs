// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNodeOpsContextFlowTests.cs
//
// W2-A regression: vfx_node.connect must wire context-to-context flow links
// (Spawn → Init → Update → Output) via VFXContext.LinkTo, not via the named
// data-slot path. Disconnect must symmetrically use VFXContext.UnlinkTo.
//
// Verifies the new VfxNodeOps Connect/Disconnect path against the underlying
// VFXContext.outputContexts / inputContexts properties.

using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNodeOpsContextFlowTests
    {
        private const string FixtureDir = "Assets/VfxNodeOpsContextFlowFixtures";
        private const string GraphPath  = FixtureDir + "/FlowProbe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxNodeOpsContextFlowFixtures");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);
            if (AssetDatabase.IsValidFolder(FixtureDir))
            {
                var children = AssetDatabase.FindAssets(string.Empty, new[] { FixtureDir });
                if (children == null || children.Length == 0)
                    AssetDatabase.DeleteAsset(FixtureDir);
            }
        }

        private static string AddContext(string typeFqn, float x, float y)
        {
            var addResult = (JObject)VfxNodeTool.HandleCommand(JObject.FromObject(new {
                action = "add",
                graph  = GraphPath,
                type   = typeFqn,
                x, y,
            }));
            Assert.IsNull(addResult["error"], $"vfx_node.add failed for {typeFqn}: {addResult}");
            return (string)addResult["added"][0]["token"];
        }

        private static VFXContext ResolveContext(string token)
        {
            string guid = AssetDatabase.AssetPathToGUID(GraphPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            Assert.IsNotNull(model, $"Could not resolve token {token}");
            var ctx = model as VFXContext;
            Assert.IsNotNull(ctx, $"Token {token} did not resolve to a VFXContext (got {model.GetType().Name})");
            return ctx;
        }

        [Test]
        public void Connect_SpawnToInitToUpdate_LinksContextsAndDisconnectClears()
        {
            // Create the graph and three contexts via the kernel.
            VfxAssetTool.HandleCommand(JObject.FromObject(new { action = "create", path = GraphPath }));

            string spawnToken  = AddContext("UnityEditor.VFX.VFXBasicSpawner",    0f,   0f);
            string initToken   = AddContext("UnityEditor.VFX.VFXBasicInitialize", 0f, 200f);
            string updateToken = AddContext("UnityEditor.VFX.VFXBasicUpdate",     0f, 400f);

            // vfx_node.list should report all three contexts present.
            var listEntries = VfxKernelContainer.NodeOps.ListNodes(GraphPath);
            Assert.IsTrue(listEntries.Any(e => e.Token == spawnToken),  "spawn context missing from list");
            Assert.IsTrue(listEntries.Any(e => e.Token == initToken),   "init context missing from list");
            Assert.IsTrue(listEntries.Any(e => e.Token == updateToken), "update context missing from list");

            // Connect Spawn → Init and Init → Update via the new flow path
            // (passing "0" for both slot indices, the default flow port).
            VfxKernelContainer.NodeOps.Connect(GraphPath, spawnToken, "0", initToken,  "0");
            VfxKernelContainer.NodeOps.Connect(GraphPath, initToken,  "0", updateToken, "0");

            var spawnCtx  = ResolveContext(spawnToken);
            var initCtx   = ResolveContext(initToken);
            var updateCtx = ResolveContext(updateToken);

            Assert.Contains(initCtx,   spawnCtx.outputContexts.ToList(),
                "Spawn → Init flow link not registered on outputContexts");
            Assert.Contains(spawnCtx,  initCtx.inputContexts.ToList(),
                "Spawn → Init flow link not registered on inputContexts");
            Assert.Contains(updateCtx, initCtx.outputContexts.ToList(),
                "Init → Update flow link not registered on outputContexts");
            Assert.Contains(initCtx,   updateCtx.inputContexts.ToList(),
                "Init → Update flow link not registered on inputContexts");

            // Disconnect Spawn → Init and verify the link is gone while
            // Init → Update remains intact.
            VfxKernelContainer.NodeOps.Disconnect(GraphPath, spawnToken, "0", initToken, "0");

            Assert.IsFalse(spawnCtx.outputContexts.Contains(initCtx),
                "Spawn → Init link still present after Disconnect");
            Assert.IsFalse(initCtx.inputContexts.Contains(spawnCtx),
                "Spawn → Init link still present on init.inputContexts after Disconnect");
            Assert.Contains(updateCtx, initCtx.outputContexts.ToList(),
                "Init → Update link should still be present after disconnecting Spawn → Init");
        }
    }
}
