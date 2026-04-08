// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxTransaction.cs
//
// Phase 3D-2 — IVfxTransaction implementation.
//
// A transaction is the only legal way to mutate a VFX graph through this
// kernel. Begin() takes a console snapshot up front so the correlator can
// later attribute warnings only to lines emitted during this scope. Commit()
// runs the three-part health gate in strict order:
//
//   Part 1: YAML structural diff   (verifier.Verify)
//   Part 2: Compile gate           (compileGate.Compile)
//   Part 3: Console correlation    (correlator.CorrelateAfter)
//
// Any failure in parts 1 or 2 short-circuits with a VfxErrorEnvelope; part 3
// is purely additive (correlated warnings folded into the success result).
// On success, identity sidecar writes are flushed exactly once.
//
// References INTERFACES only (IVfxIdentity, IVfxYamlVerifier, IVfxCompileGate,
// IVfxConsoleCorrelator, IVfxBusyGate) so this file compiles independently
// of lanes 3B/3C's concrete classes. Phase 4 task 4-SETUP wires concretes
// via VfxKernelContainer.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxTransaction : IVfxTransaction
    {
        private readonly IVfxIdentity _identity;
        private readonly IVfxYamlVerifier _verifier;
        private readonly IVfxCompileGate _compileGate;
        private readonly IVfxConsoleCorrelator _correlator;
        private readonly IVfxBusyGate _busyGate;

        public VfxTransaction(
            IVfxIdentity identity,
            IVfxYamlVerifier verifier,
            IVfxCompileGate compileGate,
            IVfxConsoleCorrelator correlator,
            IVfxBusyGate busyGate)
        {
            _identity = identity;
            _verifier = verifier;
            _compileGate = compileGate;
            _correlator = correlator;
            _busyGate = busyGate;
        }

        public VfxTransactionScope Begin(string graphGuid, string graphAssetPath, VfxTransactionScopeKind kind)
        {
            // Always gate on editor idle BEFORE taking the console snapshot,
            // so a busy editor never spuriously consumes ring-buffer capacity.
            _busyGate.EnsureIdle();

            return new Scope(
                graphGuid,
                graphAssetPath,
                kind,
                _identity,
                _verifier,
                _compileGate,
                _correlator);
        }

        private sealed class Scope : VfxTransactionScope
        {
            private readonly string _graphGuid;
            private readonly string _graphAssetPath;
            private readonly VfxTransactionScopeKind _kind;
            private readonly IVfxIdentity _identity;
            private readonly IVfxYamlVerifier _verifier;
            private readonly IVfxCompileGate _compileGate;
            private readonly IVfxConsoleCorrelator _correlator;
            private readonly object _consoleSnapshot;
            private readonly VfxIntentSnapshot _intent;

            private bool _disposed;

            public Scope(
                string graphGuid,
                string graphAssetPath,
                VfxTransactionScopeKind kind,
                IVfxIdentity identity,
                IVfxYamlVerifier verifier,
                IVfxCompileGate compileGate,
                IVfxConsoleCorrelator correlator)
            {
                _graphGuid = graphGuid;
                _graphAssetPath = graphAssetPath;
                _kind = kind;
                _identity = identity;
                _verifier = verifier;
                _compileGate = compileGate;
                _correlator = correlator;

                // Pre-snapshot the console so CorrelateAfter has a stable
                // baseline for "lines added during this transaction."
                _consoleSnapshot = correlator.SnapshotBefore();

                _intent = new VfxIntentSnapshot
                {
                    CorrelationId = Guid.NewGuid().ToString("n").Substring(0, 8),
                    Ops = new List<VfxIntentOp>(),
                };
            }

            public override void Record(VfxIntentOp op)
            {
                if (op == null) return;
                _intent.Ops.Add(op);
            }

            public override VfxCommitResult Commit()
            {
                var result = new VfxCommitResult();

                // Save the asset to disk so the verifier sees the post-mutation YAML.
                // SaveAssetIfDirty is a no-op when the asset is already clean.
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(_graphAssetPath);
                if (asset != null)
                    AssetDatabase.SaveAssetIfDirty(asset);

                // ────────────────── Part 1: YAML structural diff ──────────────────
                var yamlResult = _verifier.Verify(_graphAssetPath, _intent);

                if (yamlResult.Warnings != null)
                {
                    foreach (var w in yamlResult.Warnings)
                        result.Warnings.Add(w);
                }

                if (yamlResult.Errors != null && yamlResult.Errors.Count > 0)
                {
                    result.Ok = false;
                    result.Error = new VfxErrorEnvelope
                    {
                        Code = "intent_diverged",
                        Message = $"{yamlResult.Errors.Count} intent op(s) did not appear in the saved YAML.",
                        Hint = "The graph state on disk diverged from the recorded intent. Re-fetch and retry.",
                        Details = new Dictionary<string, object>
                        {
                            ["errors"] = yamlResult.Errors,
                            ["correlation_id"] = _intent.CorrelationId,
                        },
                    };
                    return result;
                }

                // ────────────────── Part 2: Compile gate ──────────────────
                var compileResult = _compileGate.Compile(_graphAssetPath);

                if (compileResult == null || !compileResult.Ok)
                {
                    result.Ok = false;
                    result.Error = new VfxErrorEnvelope
                    {
                        Code = "compile_error",
                        Message = compileResult != null
                            ? $"{compileResult.Errors?.Count ?? 0} compile error(s)."
                            : "Compile gate returned no result.",
                        Hint = "Inspect the compile errors and revert offending ops via vfx_node.discard.",
                        Details = new Dictionary<string, object>
                        {
                            ["errors"] = compileResult?.Errors,
                            ["correlation_id"] = _intent.CorrelationId,
                        },
                    };
                    return result;
                }

                // ────────────────── Part 3: Console correlation ──────────────────
                var correlation = _correlator.CorrelateAfter(_consoleSnapshot, _intent);

                if (correlation?.Correlated != null)
                {
                    foreach (var c in correlation.Correlated)
                        result.Warnings.Add(c);
                }

                // Flush sidecar writes once per successful commit.
                _identity.Flush();

                result.Ok = true;
                result.Health = new VfxHealthReport
                {
                    YamlDiff = (yamlResult.Warnings != null && yamlResult.Warnings.Count > 0) ? "warnings" : "clean",
                    Compile = "ok",
                    Console = (correlation?.Correlated != null && correlation.Correlated.Count > 0) ? "warnings" : "ok",
                    CompileMs = compileResult.DurationMs,
                };
                return result;
            }

            public override void Dispose()
            {
                // Commit() does the real work; Dispose() is intentionally a no-op
                // so accidentally double-disposing inside a `using` block is harmless.
                _disposed = true;
            }
        }
    }
}
