using MCPForUnity.Editor.Tools.Vfx;
using NUnit.Framework;

namespace SpiralingStudio.Mcp.VfxGraph.Tests.Editor
{
    public class VfxActionsTests
    {
        [Test]
        public void NormalizeGraphAction_MapsAliasToCanonicalAction()
        {
            Assert.AreEqual("remove_node", VfxActions.NormalizeGraphAction("graph_remove_node"));
            Assert.AreEqual("remove_node", VfxActions.NormalizeGraphAction("delete_node"));
            Assert.AreEqual("add_node", VfxActions.NormalizeGraphAction("graph_add_node"));
        }

        [Test]
        public void NormalizeGraphAction_ReturnsOriginalForUnknownAction()
        {
            Assert.AreEqual("some_unknown", VfxActions.NormalizeGraphAction("some_unknown"));
        }

        [Test]
        public void NormalizeGraphAction_HandlesNullAndEmpty()
        {
            Assert.IsNull(VfxActions.NormalizeGraphAction(null));
            Assert.AreEqual("", VfxActions.NormalizeGraphAction(""));
        }

        [Test]
        public void GraphActions_ContainsNewOperationalActions()
        {
            CollectionAssert.Contains(VfxActions.GraphActions, "set_capacity");
            CollectionAssert.Contains(VfxActions.GraphActions, "disconnect_nodes");
            CollectionAssert.Contains(VfxActions.GraphActions, "get_connections");
            CollectionAssert.Contains(VfxActions.GraphActions, "save_graph");
        }

        [Test]
        public void IsKnownAction_ReturnsTrueForKnownActions()
        {
            Assert.IsTrue(VfxActions.IsKnownAction("add_node"));
            Assert.IsTrue(VfxActions.IsKnownAction("get_graph_info"));
            Assert.IsTrue(VfxActions.IsKnownAction("save_graph"));
        }

        [Test]
        public void IsKnownAction_ReturnsFalseForUnknown()
        {
            Assert.IsFalse(VfxActions.IsKnownAction("nonexistent_action"));
        }

        [Test]
        public void IsKnownAction_IsCaseInsensitive()
        {
            Assert.IsTrue(VfxActions.IsKnownAction("ADD_NODE"));
            Assert.IsTrue(VfxActions.IsKnownAction("Get_Graph_Info"));
        }
    }
}
