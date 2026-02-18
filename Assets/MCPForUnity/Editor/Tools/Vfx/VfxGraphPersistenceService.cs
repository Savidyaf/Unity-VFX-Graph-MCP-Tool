using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphPersistenceService
    {
        internal static void Persist(UnityEngine.Object resource)
        {
            if (resource != null)
            {
                EditorUtility.SetDirty(resource);
            }

            AssetDatabase.SaveAssets();
        }

        internal static bool TryInvalidate(ScriptableObject model, string causeName)
        {
            if (model == null) return false;

            try
            {
                var invalidationCauseType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == "InvalidationCause" && t.IsEnum);

                if (invalidationCauseType == null) return false;

                object cause;
                try { cause = Enum.Parse(invalidationCauseType, causeName, true); }
                catch { return false; }

                var invalidateMethods = model.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "Invalidate")
                    .ToArray();

                var invalidateMethod = invalidateMethods.FirstOrDefault(m =>
                    m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == invalidationCauseType);

                if (invalidateMethod == null) return false;

                var parms = invalidateMethod.GetParameters();
                if (parms.Length == 1)
                    invalidateMethod.Invoke(model, new object[] { cause });
                else if (parms.Length >= 2)
                    invalidateMethod.Invoke(model, new object[] { cause, model });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
