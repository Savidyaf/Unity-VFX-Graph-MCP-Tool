// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Tools/VfxRecipeListTests.cs
//
// W3-D LIFT (v0.3.1): vfx_recipe.list now returns a real catalog of 4 recipe
// {name, description} entries instead of the empty-array + stale deferral
// note. Recipe EXECUTION remains deferred to v0.3.2.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Tools;

namespace SpiralingStudio.VfxMcp.Tools.Tests
{
    public class VfxRecipeListTests
    {
        [Test]
        public void List_ReturnsCanonicalCatalog_WithoutStaleNote()
        {
            var response = (JObject)VfxRecipeTool.HandleCommand(JObject.FromObject(new
            {
                action = "list",
            }));

            Assert.IsNotNull(response, "vfx_recipe.list must return a response");
            Assert.IsNull(response["error"],
                $"vfx_recipe.list must not surface an error envelope: {response}");

            // recipes[] non-empty
            var recipes = response["recipes"] as JArray;
            Assert.IsNotNull(recipes, "response must include 'recipes' array");
            Assert.Greater(recipes.Count, 0, "'recipes' array must be non-empty");

            // total >= 4
            var total = response["total"]?.Value<int?>();
            Assert.IsNotNull(total, "response must include 'total' key");
            Assert.GreaterOrEqual(total.Value, 4,
                "recipe catalog must contain at least 4 entries");

            // No stale 'note' key
            Assert.IsNull(response["note"],
                "stale deferral 'note' key must be removed in v0.3.1");

            // Every entry has both 'name' and 'description'
            bool foundEcsBuffer = false;
            foreach (var entry in recipes)
            {
                var name = entry["name"]?.Value<string>();
                var description = entry["description"]?.Value<string>();
                Assert.IsNotNull(name,
                    $"every recipe entry must have a 'name' key; got: {entry}");
                Assert.IsNotNull(description,
                    $"every recipe entry must have a 'description' key; got: {entry}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(name),
                    "'name' must be non-empty");
                Assert.IsFalse(string.IsNullOrWhiteSpace(description),
                    "'description' must be non-empty");
                if (name == "ecs_buffer_particles")
                    foundEcsBuffer = true;
            }

            // Canonical anchor entry must be present
            Assert.IsTrue(foundEcsBuffer,
                "recipe catalog must contain 'ecs_buffer_particles' entry");
        }
    }
}
