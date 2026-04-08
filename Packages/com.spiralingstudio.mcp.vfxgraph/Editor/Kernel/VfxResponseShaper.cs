// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxResponseShaper.cs
//
// Phase 3D-1 — IVfxResponseShaper implementation.
//
// Token-savings shaping rule:
//   - Terse mode (verbose=false) MUST omit the `health` key entirely.
//     This is a release-gate criterion (category 17 / spec performance budget).
//   - Verbose mode includes `health` when commit.Health is non-null.
//   - Errored commits delegate to ShapeError so the wire format is identical
//     regardless of which call path produced the failure.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SpiralingStudio.VfxMcp.Generated;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxResponseShaper : IVfxResponseShaper
    {
        public object Shape(VfxCommitResult commit, bool verbose)
        {
            if (commit == null || !commit.Ok)
                return ShapeError(commit?.Error);

            var obj = new JObject();

            if (commit.Diffs != null && commit.Diffs.Count > 0)
                obj["added"] = JArray.FromObject(commit.Diffs);

            if (commit.Warnings != null && commit.Warnings.Count > 0)
                obj["warnings"] = JArray.FromObject(commit.Warnings);

            // Health is verbose-only. The terse path must NOT add this key
            // even when commit.Health is non-null — verified by
            // VfxResponseShaperTests.Shape_Terse_OmitsHealthReport.
            if (verbose && commit.Health != null)
                obj["health"] = JObject.FromObject(commit.Health);

            return obj;
        }

        public object ShapeError(VfxErrorEnvelope error)
        {
            var err = new JObject();

            if (error != null)
            {
                err["code"] = error.Code ?? "unknown_error";
                err["message"] = error.Message ?? "";
                // Phase 5: look up a hint from the override layer when the
                // caller has not supplied one. Caller-supplied hints take
                // precedence (non-null, non-empty wins).
                string hint = error.Hint;
                if (string.IsNullOrEmpty(hint))
                    hint = VfxOverrides.GetHint(error.Code);
                err["hint"] = hint ?? "";

                if (error.Details != null && error.Details.Count > 0)
                    err["details"] = JObject.FromObject(error.Details);

                if (error.RetryAfterHintMs.HasValue)
                    err["retry_after_hint_ms"] = error.RetryAfterHintMs.Value;
            }
            else
            {
                err["code"] = "unknown_error";
                err["message"] = "";
                err["hint"] = "";
            }

            return new JObject { ["error"] = err };
        }

        public object ShapeRead(object payload, bool verbose)
        {
            // Phase 3D pass-through. Phase 4 will add per-action shaping
            // (filter heavy fields in terse mode, expand them in verbose).
            return payload;
        }
    }
}
