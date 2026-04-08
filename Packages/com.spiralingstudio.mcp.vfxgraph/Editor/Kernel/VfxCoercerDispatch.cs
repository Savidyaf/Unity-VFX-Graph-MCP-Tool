// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxCoercerDispatch.cs
//
// Phase v0.3.1 F7 — typed coercion dispatch.
//
// Replaces VfxNodeOps.SetSetting's previous System.Convert.ChangeType call,
// which silently failed (or threw) for Vector2/3/4, Color, enums, and other
// VFX-graph setting types. The generated VfxCoercers.g.cs table is the
// source of truth for which target types have native coercion support; this
// helper dispatches based on the field's runtime Type and falls through to
// Convert.ChangeType only when no generated coercer matches.
//
// The generated catalog (VfxCoercers.g.cs) exposes coercers for:
//   float, int, uint, bool, string,
//   UnityEngine.Vector2/Vector3/Vector4, UnityEngine.Color,
//   plus many VFX-struct types (AABox, Transform, Sphere, etc.)
// It does NOT expose enum coercers — this helper handles enums inline by
// name-or-int parse against the target enum type.

using System;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Generated;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal static class VfxCoercerDispatch
    {
        /// <summary>
        /// Coerces <paramref name="value"/> into the runtime <paramref name="targetType"/>
        /// using the generated VfxCoercers catalog first, then an inline enum branch,
        /// then a final System.Convert.ChangeType fallback for types nothing else handles.
        /// </summary>
        public static object Coerce(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType == null) return value;

            // Already the right type — pass through (handles JToken-typed payloads
            // whose inner .NET type is already what the setting field expects).
            if (targetType.IsInstanceOfType(value)) return value;

            // Wrap into JToken for the generated coercers (they take JToken).
            JToken token = value as JToken ?? JToken.FromObject(value);

            // Primitive scalars — generated coercers in VfxCoercers.g.cs.
            if (targetType == typeof(float))   return VfxCoercers.CoerceToFloat(token);
            if (targetType == typeof(int))     return VfxCoercers.CoerceToInt(token);
            if (targetType == typeof(uint))    return VfxCoercers.CoerceToUInt(token);
            if (targetType == typeof(bool))    return VfxCoercers.CoerceToBool(token);
            if (targetType == typeof(string))  return VfxCoercers.CoerceToString(token);

            // Unity math types — generated coercers in VfxCoercers.g.cs.
            if (targetType == typeof(UnityEngine.Vector2)) return VfxCoercers.CoerceToVector2(token);
            if (targetType == typeof(UnityEngine.Vector3)) return VfxCoercers.CoerceToVector3(token);
            if (targetType == typeof(UnityEngine.Vector4)) return VfxCoercers.CoerceToVector4(token);
            if (targetType == typeof(UnityEngine.Color))   return VfxCoercers.CoerceToColor(token);

            // Enums — no generated coercer, handled inline.
            // Enum-by-name first (string token), then enum-by-int.
            if (targetType.IsEnum)
            {
                if (token.Type == JTokenType.String)
                {
                    string s = (string)token;
                    return Enum.Parse(targetType, s, ignoreCase: true);
                }
                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                {
                    return Enum.ToObject(targetType, token.Value<int>());
                }
                throw new InvalidCastException(
                    $"Cannot coerce {token.Type} to enum {targetType.FullName}");
            }

            // Fallback: preserve previous behavior. F7 closes the most common gaps,
            // but any unmatched type still uses Convert.ChangeType (which will throw
            // for types it cannot handle, matching pre-F7 behavior).
            return System.Convert.ChangeType(value, targetType);
        }
    }
}
