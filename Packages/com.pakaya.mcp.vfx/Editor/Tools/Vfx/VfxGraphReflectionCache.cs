using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphReflectionCache
    {
        private static readonly Dictionary<string, Type> TypeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MethodInfo> MethodBySignature = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        static VfxGraphReflectionCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        internal static void Clear()
        {
            TypeByName.Clear();
            MethodBySignature.Clear();
        }

        internal static Type GetEditorVfxType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            if (TypeByName.TryGetValue(typeName, out var cachedType)) return cachedType;

            var resolved = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t =>
                    t.Name == typeName &&
                    t.Namespace != null &&
                    t.Namespace.StartsWith("UnityEditor.VFX", StringComparison.Ordinal));

            TypeByName[typeName] = resolved;
            return resolved;
        }

        internal static MethodInfo GetMethodCached(Type ownerType, string methodName, BindingFlags flags)
        {
            if (ownerType == null || string.IsNullOrWhiteSpace(methodName)) return null;
            string key = ownerType.FullName + "::" + methodName + "::" + (int)flags;
            if (MethodBySignature.TryGetValue(key, out var cachedMethod)) return cachedMethod;

            var method = ownerType.GetMethod(methodName, flags);
            MethodBySignature[key] = method;
            return method;
        }
    }
}
