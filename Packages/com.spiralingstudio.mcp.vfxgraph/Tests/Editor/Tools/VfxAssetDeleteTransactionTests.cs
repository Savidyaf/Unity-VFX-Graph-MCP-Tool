// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxAssetDeleteTransactionTests.cs
//
// W3-B (was C6) — vfx_asset.delete must bypass VfxTransaction. Before the fix,
// Delete opened a transaction and called Commit(), which runs a three-part
// health gate (YAML diff + compile gate + console correlation) against an
// asset that had just been removed from disk — so the caller saw a spurious
// "compile_error" envelope even though the delete itself succeeded.
//
// This test proves the fix by asserting the delete response is a plain
// {"deleted": path} payload with no "error" key and no "health" key (the
// presence of "health" would indicate the response flowed through
// Shaper.Shape(commit, verbose), i.e. the transaction health gate).

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxAssetDeleteTransactionTests
    {
        private const string FixtureDir = "Assets/VfxAssetDeleteFixtures";
        private const string ProbePath  = FixtureDir + "/Probe.vfx";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxAssetDeleteFixtures");
            if (AssetDatabase.LoadMainAssetAtPath(ProbePath) != null)
                AssetDatabase.DeleteAsset(ProbePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ProbePath) != null)
                AssetDatabase.DeleteAsset(ProbePath);
            if (AssetDatabase.IsValidFolder(FixtureDir))
                AssetDatabase.DeleteAsset(FixtureDir);
        }

        [Test]
        public void Delete_BypassesTransaction_ReturnsPlainDeletedPayloadNoHealth()
        {
            // 1. Create the fixture via vfx_asset.create.
            var createResp = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new
            {
                action = "create",
                path   = ProbePath,
            }));
            Assert.IsNull(createResp["error"],
                $"create returned error: {createResp}");

            // 2. vfx_asset.list should contain the freshly-created probe.
            var listBefore = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new
            {
                action = "list",
            }));
            Assert.IsNull(listBefore["error"],
                $"list (before delete) returned error: {listBefore}");
            var assetsBefore = (JArray)listBefore["assets"];
            Assert.IsNotNull(assetsBefore, "list response missing 'assets'");
            Assert.IsTrue(ContainsPath(assetsBefore, ProbePath),
                $"list (before delete) did not contain {ProbePath}: {listBefore}");

            // 3. Call vfx_asset.delete — this is the method under test.
            var deleteResp = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new
            {
                action = "delete",
                path   = ProbePath,
            }));

            // 4a. No error envelope — before the fix this was a spurious
            //     {code: "compile_error", ...} even though the delete succeeded.
            Assert.IsNull(deleteResp["error"],
                $"delete returned error envelope (regression of W3-B compile_error bug): {deleteResp}");

            // 4b. Plain {"deleted": "<path>"} payload.
            Assert.IsNotNull(deleteResp["deleted"],
                $"delete response missing 'deleted' key: {deleteResp}");
            Assert.AreEqual(ProbePath, deleteResp.Value<string>("deleted"),
                $"delete 'deleted' value mismatch: {deleteResp}");

            // 4c. No 'health' key — proof the response did NOT flow through
            //     Shaper.Shape(commit, verbose), i.e. the transaction health
            //     gate has been bypassed. This is the load-bearing assertion.
            Assert.IsNull(deleteResp["health"],
                $"delete response must not include 'health' — presence proves the transaction health gate still runs: {deleteResp}");

            // 5. Subsequent list must not contain the deleted probe.
            var listAfter = (JObject)VfxAssetTool.HandleCommand(JObject.FromObject(new
            {
                action = "list",
            }));
            Assert.IsNull(listAfter["error"],
                $"list (after delete) returned error: {listAfter}");
            var assetsAfter = (JArray)listAfter["assets"];
            Assert.IsNotNull(assetsAfter, "list response missing 'assets'");
            Assert.IsFalse(ContainsPath(assetsAfter, ProbePath),
                $"list (after delete) still contained {ProbePath}: {listAfter}");
        }

        private static bool ContainsPath(JArray assets, string path)
        {
            foreach (var entry in assets)
            {
                if (entry is JObject obj && obj.Value<string>("path") == path)
                    return true;
            }
            return false;
        }
    }
}
