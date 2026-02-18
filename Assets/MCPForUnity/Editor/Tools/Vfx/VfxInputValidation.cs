using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxInputValidation
    {
        internal static bool TryGetRequiredString(JObject @params, string key, out string value, out object error)
        {
            value = @params?[key]?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) { error = null; return true; }
            error = VfxToolContract.Error(
                VfxErrorCodes.ValidationError,
                $"'{key}' is required");
            return false;
        }

        internal static bool TryValidateAssetPath(string path, string requiredExtension, out string normalizedPath, out object error)
        {
            normalizedPath = path?.Trim().Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                error = VfxToolContract.Error(VfxErrorCodes.ValidationError, "asset path is required");
                return false;
            }

            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                !normalizedPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                normalizedPath = "Assets/" + normalizedPath.TrimStart('/');
            }

            if (!string.IsNullOrEmpty(requiredExtension) &&
                !normalizedPath.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath += requiredExtension;
            }

            if (normalizedPath.Contains("../", StringComparison.Ordinal) ||
                normalizedPath.Contains("/..", StringComparison.Ordinal))
            {
                error = VfxToolContract.Error(
                    VfxErrorCodes.ValidationError,
                    "Path traversal is not allowed.",
                    new { path = normalizedPath });
                return false;
            }

            string fileName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = VfxToolContract.Error(
                    VfxErrorCodes.ValidationError,
                    "Invalid asset file name.",
                    new { path = normalizedPath });
                return false;
            }

            error = null;
            return true;
        }
    }
}
