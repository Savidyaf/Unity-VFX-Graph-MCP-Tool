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

            // The upstream Python MCP host normalizer (models/unity_response.py
            // in coplaydev/unity-mcp) strips inner-payload keys matching
            // {message, error, status, code} to derive its `data` field. An
            // envelope whose only top-level key is `error` therefore arrives on
            // the wire as `data: null` with the error body discarded. The
            // normalizer also has a fast-path early in the function:
            //   if "success" in response: return response
            // which passes the inner JObject through unchanged. We use that
            // fast-path to ship the full error envelope verbatim regardless of
            // the upstream bug — without this `success: false` discriminator,
            // every error envelope from every vfx_* tool is silently lost.
            return new JObject { ["success"] = false, ["error"] = err };
        }

        public object ShapeRead(object payload, bool verbose)
        {
            // Phase 3D pass-through. Phase 4 will add per-action shaping
            // (filter heavy fields in terse mode, expand them in verbose).
            return payload;
        }

        // ── ShapeMutation (Lane B B1 — F5) ─────────────────────────────────
        // Folds per-action mutation metadata into the commit-shaped JObject,
        // eliminating the post-hoc `obj["added"] = ...` anti-pattern that every
        // tool previously duplicated. Verbose-only health rule matches Shape().
        public object ShapeMutation(VfxCommitResult commit, bool verbose, JObject mutationPayload)
        {
            if (commit == null || !commit.Ok)
                return ShapeError(commit?.Error);

            var obj = new JObject();

            // Fold mutation payload first so per-action keys appear at the top.
            if (mutationPayload != null)
            {
                foreach (var prop in mutationPayload.Properties())
                    obj[prop.Name] = prop.Value;
            }

            // Preserve commit.Diffs only if the payload didn't already supply
            // an "added" key (avoid double-shaping).
            if (commit.Diffs != null && commit.Diffs.Count > 0 && obj["added"] == null)
                obj["added"] = JArray.FromObject(commit.Diffs);

            if (commit.Warnings != null && commit.Warnings.Count > 0)
                obj["warnings"] = JArray.FromObject(commit.Warnings);

            // Health is verbose-only — same rule as Shape().
            if (verbose && commit.Health != null)
                obj["health"] = JObject.FromObject(commit.Health);

            return obj;
        }
    }
}
