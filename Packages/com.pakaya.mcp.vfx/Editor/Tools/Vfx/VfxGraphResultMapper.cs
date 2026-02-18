using System;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxGraphResultMapper
    {
        internal static object Wrap(object rawResult, string action)
        {
            if (rawResult == null)
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.UnknownError,
                    $"Action '{action}' returned no result.");
            }

            if (rawResult is JObject jObject)
            {
                bool success = jObject["success"]?.ToObject<bool>() ?? false;
                string message = jObject["message"]?.ToString() ?? (success ? "OK" : "Operation failed");
                var data = jObject["data"];

                if (success)
                {
                    return VfxToolContract.Success(message, data, new { action });
                }

                return VfxToolContract.Error(
                    GuessErrorCode(jObject),
                    message,
                    new { action, raw = jObject });
            }

            // Existing code returns anonymous objects. Convert via JObject for consistent mapping.
            var normalized = JObject.FromObject(rawResult);
            bool resultSuccess = normalized["success"]?.ToObject<bool>() ?? false;
            string resultMessage = normalized["message"]?.ToString() ?? (resultSuccess ? "OK" : "Operation failed");
            JToken resultData = normalized["data"];

            if (resultData == null)
            {
                // Preserve extra fields for legacy callsites in details, not top-level.
                normalized.Remove("success");
                normalized.Remove("message");
                resultData = normalized;
            }

            if (resultSuccess)
            {
                return VfxToolContract.Success(resultMessage, resultData, new { action });
            }

            return VfxToolContract.Error(
                GuessErrorCode(normalized),
                resultMessage,
                new { action, raw = normalized });
        }

        private static string GuessErrorCode(JObject result)
        {
            if (result == null) return VfxErrorCodes.UnknownError;

            string explicit_code = result["error_code"]?.ToString();
            if (!string.IsNullOrEmpty(explicit_code)) return explicit_code;

            string message = result["message"]?.ToString() ?? string.Empty;
            if (message.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0) return VfxErrorCodes.ValidationError;
            if (message.IndexOf("asset", StringComparison.OrdinalIgnoreCase) >= 0 &&
                message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return VfxErrorCodes.AssetNotFound;
            if (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return VfxErrorCodes.NotFound;
            if (message.IndexOf("reflection", StringComparison.OrdinalIgnoreCase) >= 0) return VfxErrorCodes.ReflectionError;
            return VfxErrorCodes.UnknownError;
        }
    }
}
