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
        private static Assembly[] _cachedAssemblies;

        static VfxGraphReflectionCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        internal static void Clear()
        {
            TypeByName.Clear();
            MethodBySignature.Clear();
            _cachedAssemblies = null;
        }

        internal static Assembly[] GetAssemblies()
        {
            return _cachedAssemblies ?? (_cachedAssemblies = AppDomain.CurrentDomain.GetAssemblies());
        }

        internal static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Type.EmptyTypes; }
        }

        internal static Type GetEditorVfxType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            if (TypeByName.TryGetValue(typeName, out var cachedType)) return cachedType;

            var resolved = GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t =>
                    t.Name == typeName &&
                    t.Namespace != null &&
                    t.Namespace.StartsWith("UnityEditor.VFX", StringComparison.Ordinal));

            if (resolved != null) TypeByName[typeName] = resolved;
            return resolved;
        }

        internal static Type ResolveType(string typeName, Type requiredBase = null, bool caseInsensitive = true)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            string cacheKey = typeName + "|" + (requiredBase?.FullName ?? "*");
            if (TypeByName.TryGetValue(cacheKey, out var cached)) return cached;

            var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var resolved = GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t =>
                    (string.Equals(t.Name, typeName, comparison) ||
                     string.Equals(t.FullName, typeName, comparison)) &&
                    (requiredBase == null || requiredBase.IsAssignableFrom(t)));

            TypeByName[cacheKey] = resolved;
            return resolved;
        }

        internal static Type ResolveEnumType(string enumName)
        {
            if (string.IsNullOrWhiteSpace(enumName)) return null;
            string cacheKey = "enum|" + enumName;
            if (TypeByName.TryGetValue(cacheKey, out var cached)) return cached;

            var resolved = GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == enumName && t.IsEnum);

            TypeByName[cacheKey] = resolved;
            return resolved;
        }

        internal static Assembly GetAssemblyByName(string assemblyName)
        {
            return GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
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
