using System.Reflection;
using MCPForUnity.Editor.Tools.Vfx;
using NUnit.Framework;

namespace Pakaya.Mcp.Vfx.Tests.Editor
{
    public class VfxGraphReflectionCacheTests
    {
        private class FakeOverloads
        {
            public void Foo() { }
            public void Foo(string value) { }
            public void Foo(int value, bool flag) { }
        }

        [Test]
        public void GetMethodCached_ResolvesSpecificOverloadWhenSignatureProvided()
        {
            MethodInfo method = VfxGraphReflectionCache.GetMethodCached(
                typeof(FakeOverloads),
                "Foo",
                BindingFlags.Instance | BindingFlags.Public,
                typeof(int),
                typeof(bool));

            Assert.IsNotNull(method);
            var parms = method.GetParameters();
            Assert.AreEqual(2, parms.Length);
            Assert.AreEqual(typeof(int), parms[0].ParameterType);
            Assert.AreEqual(typeof(bool), parms[1].ParameterType);
        }
    }
}
