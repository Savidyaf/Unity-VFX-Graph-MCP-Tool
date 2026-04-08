// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContracts.cs
//
// LOCKED. Do not modify in phase 3/4 subagent lanes. Changes require
// main-agent approval and a new contracts commit.
//
// Phase 3 lanes (3A/3B/3C/3D) compile against this file. Phase 4 tools
// likewise consume the same interface surface. Erratum P-L4: error
// envelope types live here, not in a separate VfxErrorEnvelope.cs.

using System;
using System.Collections.Generic;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    // ────────────────────────── Identity ──────────────────────────

    internal interface IVfxIdentity
    {
        string Mint(string graphGuid, VFXModel model);
        VFXModel Resolve(string graphGuid, string token);

        /// <summary>Flush pending sidecar writes to disk.</summary>
        void Flush();
    }

    internal sealed class VfxIdentityRecord
    {
        public string Token;
        public ulong Fingerprint;

        // Erratum P-H3: schema v2 carries type and parent fingerprint so the
        // recovery path can filter candidates without ambiguity.
        public string TypeFqn;
        public ulong ParentFingerprint;
    }

    // ────────────────────────── Verifier ──────────────────────────

    internal interface IVfxYamlVerifier
    {
        VfxYamlVerificationResult Verify(string graphAssetPath, VfxIntentSnapshot intent);
    }

    internal sealed class VfxYamlVerificationResult
    {
        public List<VfxVerifierWarning> Warnings = new List<VfxVerifierWarning>();
        public List<VfxVerifierError> Errors = new List<VfxVerifierError>();
    }

    internal sealed class VfxVerifierWarning
    {
        public string Code;
        public int OpIndex;
        public string Slot;
        public string Reason;
    }

    internal sealed class VfxVerifierError
    {
        public string Code;
        public int OpIndex;
        public string ExpectedToken;
        public string ExpectedTypeFqn;
    }

    // ────────────────────────── Compile gate ──────────────────────────

    internal interface IVfxCompileGate
    {
        VfxCompileResult Compile(string graphAssetPath);
    }

    internal sealed class VfxCompileResult
    {
        public bool Ok;
        public int DurationMs;
        public List<VfxCompileError> Errors = new List<VfxCompileError>();
    }

    internal sealed class VfxCompileError
    {
        public string ModelTypeFqn;
        public string ModelToken;
        public string ErrorId;
        public string Description;
        public string Severity; // "error" | "warning"
    }

    // ────────────────────────── Console correlator ──────────────────────────

    internal interface IVfxConsoleCorrelator
    {
        /// <summary>Record the current console high-water mark before a transaction begins.</summary>
        object SnapshotBefore();

        /// <summary>Correlate console lines since the snapshot against the given intent ops.</summary>
        VfxConsoleCorrelation CorrelateAfter(object snapshot, VfxIntentSnapshot intent);
    }

    internal sealed class VfxConsoleCorrelation
    {
        public List<VfxCorrelatedWarning> Correlated = new List<VfxCorrelatedWarning>();
        public List<string> RawLines = new List<string>();
    }

    internal sealed class VfxCorrelatedWarning
    {
        public string Code;
        public int OpIndex;
        public string RawLine;
    }

    // ────────────────────────── Busy gate ──────────────────────────

    internal interface IVfxBusyGate
    {
        /// <summary>Throws VfxBusyException if the editor is not idle for mutation.</summary>
        void EnsureIdle();
    }

    // ────────────────────────── Intent snapshot ──────────────────────────

    internal sealed class VfxIntentSnapshot
    {
        public string CorrelationId;
        public List<VfxIntentOp> Ops = new List<VfxIntentOp>();
    }

    internal sealed class VfxIntentOp
    {
        public int OpIndex;
        public string Kind;              // "add" | "remove" | "connect" | "disconnect" | "set_setting" | "set_property"
        public string ExpectedToken;
        public string ExpectedTypeFqn;
        public string ParentToken;       // for blocks; null for top-level
        public Dictionary<string, object> Payload;  // arbitrary per-kind metadata
    }

    // ────────────────────────── Transaction ──────────────────────────

    internal interface IVfxTransaction
    {
        VfxTransactionScope Begin(string graphGuid, string graphAssetPath, VfxTransactionScopeKind kind);
    }

    internal enum VfxTransactionScopeKind { SingleCall, Batch, Save }

    internal interface IVfxTransactionScope : IDisposable
    {
        void Record(VfxIntentOp op);
        VfxCommitResult Commit();
    }

    /// <summary>
    /// Abstract base so subagents can subclass without re-implementing IDisposable
    /// boilerplate. Phase 3D's VfxTransaction.Begin returns one of these.
    /// </summary>
    internal abstract class VfxTransactionScope : IVfxTransactionScope
    {
        public abstract void Record(VfxIntentOp op);
        public abstract VfxCommitResult Commit();
        public abstract void Dispose();
    }

    internal sealed class VfxCommitResult
    {
        public bool Ok;
        public List<object> Diffs = new List<object>();    // "added"/"removed"/"modified" entries
        public List<object> Warnings = new List<object>();
        public VfxHealthReport Health;                     // populated on Ok
        public VfxErrorEnvelope Error;                     // populated on !Ok
    }

    internal sealed class VfxHealthReport
    {
        public string YamlDiff;     // "clean" | "warnings" | "errors"
        public string Compile;      // "ok" | "errors"
        public string Console;      // "ok" | "warnings"
        public int CompileMs;
        public long YamlBytes;
    }

    // ────────────────────────── NodeOps ──────────────────────────

    internal interface IVfxNodeOps
    {
        /// <summary>Create an operator and add it to the graph. Returns the minted token.</summary>
        string AddOperator(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Create a context and add it to the graph.</summary>
        string AddContext(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Create a block and add it to the specified parent context.</summary>
        string AddBlock(string graphAssetPath, string parentContextToken, string typeFqn, int index);

        /// <summary>Create a parameter (graph-level exposed property) node.</summary>
        string AddParameter(string graphAssetPath, string typeFqn, UnityEngine.Vector2 pos);

        /// <summary>Add a subgraph reference to the parent graph.</summary>
        string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, UnityEngine.Vector2 pos);

        void RemoveNode(string graphAssetPath, string token);

        void Connect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot);
        void Disconnect(string graphAssetPath, string fromToken, string fromSlot, string toToken, string toSlot);

        void SetSetting(string graphAssetPath, string token, string name, object value);
        void SetProperty(string graphAssetPath, string token, string name, object value);

        object GetSetting(string graphAssetPath, string token, string name);
        object GetProperty(string graphAssetPath, string token, string name);

        void DiscardChanges(string graphAssetPath);
    }

    // ────────────────────────── Response shaper ──────────────────────────

    internal interface IVfxResponseShaper
    {
        object Shape(VfxCommitResult commit, bool verbose);
        object ShapeError(VfxErrorEnvelope error);
        object ShapeRead(object payload, bool verbose);
    }

    // ────────────────────────── Error envelope (erratum P-L4: lives here) ──────────────────────────

    internal sealed class VfxErrorEnvelope
    {
        public string Code;
        public string Message;
        public string Hint;
        public Dictionary<string, object> Details = new Dictionary<string, object>();
        public int? RetryAfterHintMs;
    }

    internal class VfxException : Exception
    {
        public string Code;
        public Dictionary<string, object> Details;

        public VfxException(string code, string message, Dictionary<string, object> details = null)
            : base(message)
        {
            Code = code;
            Details = details ?? new Dictionary<string, object>();
        }
    }

    internal sealed class VfxValidationException : VfxException
    {
        public VfxValidationException(string code, string message, Dictionary<string, object> details = null)
            : base(code, message, details) { }
    }

    internal sealed class VfxIdentityException : VfxException
    {
        public VfxIdentityException(string code, string message, Dictionary<string, object> details = null)
            : base(code, message, details) { }
    }

    internal sealed class VfxBusyException : VfxException
    {
        public int RetryAfterHintMs;

        public VfxBusyException(string code, string message, int retryAfterHintMs,
            Dictionary<string, object> details = null)
            : base(code, message, details)
        {
            RetryAfterHintMs = retryAfterHintMs;
        }
    }
}
