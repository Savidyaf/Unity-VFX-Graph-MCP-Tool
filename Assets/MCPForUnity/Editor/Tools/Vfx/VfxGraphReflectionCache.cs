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
            return GetMethodCached(ownerType, methodName, flags, null);
        }

        internal static MethodInfo GetMethodCached(Type ownerType, string methodName, BindingFlags flags, params Type[] parameterTypes)
        {
            if (ownerType == null || string.IsNullOrWhiteSpace(methodName)) return null;
            string suffix = parameterTypes == null
                ? "any"
                : string.Join(",", parameterTypes.Select(t => t == null ? "*" : t.FullName));
            string key = ownerType.FullName + "::" + methodName + "::" + (int)flags + "::" + suffix;
            if (MethodBySignature.TryGetValue(key, out var cachedMethod)) return cachedMethod;

            MethodInfo method;
            try
            {
                method = parameterTypes == null
                    ? ownerType.GetMethod(methodName, flags)
                    : ownerType.GetMethod(methodName, flags, null, parameterTypes, null);
            }
            catch (AmbiguousMatchException)
            {
                method = ResolveBestMatch(ownerType, methodName, flags, parameterTypes);
            }

            if (method == null && parameterTypes != null)
            {
                method = ResolveBestMatch(ownerType, methodName, flags, parameterTypes);
            }

            MethodBySignature[key] = method;
            return method;
        }

        private static MethodInfo ResolveBestMatch(Type ownerType, string methodName, BindingFlags flags, Type[] parameterTypes)
        {
            var methods = ownerType.GetMethods(flags).Where(m => m.Name == methodName);
            if (parameterTypes == null || parameterTypes.Length == 0)
            {
                return methods.OrderBy(m => m.GetParameters().Length).FirstOrDefault();
            }

            foreach (var candidate in methods)
            {
                var parms = candidate.GetParameters();
                if (parms.Length != parameterTypes.Length) continue;

                bool match = true;
                for (int i = 0; i < parms.Length; i++)
                {
                    Type expected = parameterTypes[i];
                    if (expected == null) continue;
                    if (!parms[i].ParameterType.IsAssignableFrom(expected) &&
                        parms[i].ParameterType != expected)
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return candidate;
            }

            return null;
        }
    }
}
