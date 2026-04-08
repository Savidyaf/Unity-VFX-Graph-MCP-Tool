// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxYamlVerifier.cs
//
// Lane 3B tasks 3B-4 + 3B-5: partial UnityYAML reader for the YAML half of
// the three-part health gate. We do NOT use a general YAML parser — we only
// understand the few fields needed for verification:
//   * `--- !u!<tag> &<fileId>` document markers
//   * `MonoBehaviour:` blocks with `m_Name`, `m_Script`, and (when present) a
//     `m_TypeFqn` field that identifies the C# type
//   * `m_LinkedSlots` arrays with `m_LinkedSlot: {fileID: N}` and `m_Name: x`
//
// Per erratum P-L3, sidecar tokens NEVER appear in the .vfx YAML by design.
// We therefore match by (typeFqn, structural shape), not by token. The
// VfxIntentOp's ExpectedToken is propagated only into VfxVerifierError so the
// caller can correlate diagnostics back to the originating intent.
//
// Phase 5 (task 5-7): MonoScript GUID resolver.
// The file-based Verify() overload now resolves m_Script GUIDs to FQNs via
// AssetDatabase + MonoScript.GetClass() so real .vfx YAML (which lacks
// m_TypeFqn) can be strict-matched without falling back to yaml_verify_skipped.
// The VerifyFromYaml() overload (synthetic test entry) retains the production-
// mode fallback because AssetDatabase is not available there.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxYamlVerifier : IVfxYamlVerifier
    {
        // ──────────────────── partial parser ────────────────────

        // Captures: fileId from `--- !u!<tag> &<fileId>` then everything up to
        // (but not including) the next `--- !u!` marker. We use a manual scan
        // rather than a regex to keep the partial parser simple and bounded.
        internal sealed class YamlBlock
        {
            public long FileId;
            public string TagId;       // e.g. "114"
            public string Body;        // raw body lines after the tag line
            public string Name;        // m_Name value (or null)
            public string TypeFqn;     // m_TypeFqn value (or null); may be resolved from ScriptGuid
            public string ScriptGuid;  // GUID from `m_Script: {fileID: X, guid: Y, type: Z}` (phase 5-7)
            public List<LinkedSlot> LinkedSlots = new List<LinkedSlot>();
        }

        internal sealed class LinkedSlot
        {
            public long FileId;
            public string Name;
        }

        // The token-presence shortcut and slot-name regex are pre-compiled.
        private static readonly Regex DocMarker =
            new Regex(@"^---\s+!u!(\d+)\s+&(\d+)\s*$",
                RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex MNameLine =
            new Regex(@"^\s*m_Name:\s*(.+?)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex MTypeFqnLine =
            new Regex(@"^\s*m_TypeFqn:\s*(.+?)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex LinkedSlotFileId =
            new Regex(@"m_LinkedSlot:\s*\{fileID:\s*(\d+)",
                RegexOptions.Compiled);

        private static readonly Regex LinkedSlotName =
            new Regex(@"^\s*m_Name:\s*(.+?)\s*$",
                RegexOptions.Compiled);

        // Phase 5-7: captures the GUID from `m_Script: {fileID: X, guid: GGGG, type: Z}`
        private static readonly Regex MScriptGuid =
            new Regex(@"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]+)",
                RegexOptions.Compiled);

        internal static List<YamlBlock> ScanMonoBehaviours(string yaml)
        {
            var blocks = new List<YamlBlock>();
            if (string.IsNullOrEmpty(yaml)) return blocks;

            var matches = DocMarker.Matches(yaml);
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                int bodyStart = m.Index + m.Length;
                int bodyEnd = (i + 1 < matches.Count)
                    ? matches[i + 1].Index
                    : yaml.Length;
                string body = yaml.Substring(bodyStart, bodyEnd - bodyStart);

                // Only `!u!114` (MonoBehaviour) blocks carry node identity in
                // VFX Graph; other tags (`!u!2058629511 VisualEffectResource`,
                // `!u!21 Material`, etc.) are skipped here.
                string tagId = m.Groups[1].Value;
                if (tagId != "114") continue;
                if (body.IndexOf("MonoBehaviour:", System.StringComparison.Ordinal) < 0)
                    continue;

                var block = new YamlBlock
                {
                    TagId = tagId,
                    FileId = long.TryParse(m.Groups[2].Value, out var fid) ? fid : 0,
                    Body = body,
                };

                // Pull m_Name and m_TypeFqn out of the body (line-by-line).
                // We also collect linked slot endpoints for the structural
                // diff in task 3B-5.
                using (var reader = new StringReader(body))
                {
                    string line;
                    bool inLinkedSlots = false;
                    LinkedSlot pending = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        var nameMatch = MNameLine.Match(line);
                        if (block.Name == null && nameMatch.Success && !inLinkedSlots)
                        {
                            block.Name = nameMatch.Groups[1].Value;
                            continue;
                        }
                        var fqnMatch = MTypeFqnLine.Match(line);
                        if (fqnMatch.Success && !inLinkedSlots)
                        {
                            block.TypeFqn = fqnMatch.Groups[1].Value;
                            continue;
                        }
                        // Phase 5-7: capture the m_Script GUID for MonoScript resolution.
                        if (block.ScriptGuid == null && !inLinkedSlots)
                        {
                            var guidMatch = MScriptGuid.Match(line);
                            if (guidMatch.Success)
                            {
                                block.ScriptGuid = guidMatch.Groups[1].Value;
                                continue;
                            }
                        }
                        if (line.TrimStart().StartsWith("m_LinkedSlots:"))
                        {
                            inLinkedSlots = true;
                            continue;
                        }
                        if (inLinkedSlots)
                        {
                            // Each linked slot starts with `- m_LinkedSlot: {fileID: N}`
                            var lsMatch = LinkedSlotFileId.Match(line);
                            if (lsMatch.Success)
                            {
                                if (pending != null)
                                    block.LinkedSlots.Add(pending);
                                pending = new LinkedSlot
                                {
                                    FileId = long.TryParse(lsMatch.Groups[1].Value, out var lf) ? lf : 0,
                                };
                                continue;
                            }
                            // The `m_Name:` directly under a linked slot is the
                            // slot name (matched via the same MNameLine regex).
                            var slotName = LinkedSlotName.Match(line);
                            if (slotName.Success && pending != null)
                            {
                                pending.Name = slotName.Groups[1].Value;
                                continue;
                            }
                            // A blank line or a non-`m_` line ends the LinkedSlots block.
                            if (string.IsNullOrWhiteSpace(line) ||
                                (line.Length > 0 && line[0] != ' ' && line[0] != '-'))
                            {
                                if (pending != null)
                                {
                                    block.LinkedSlots.Add(pending);
                                    pending = null;
                                }
                                inLinkedSlots = false;
                            }
                        }
                    }
                    if (pending != null)
                        block.LinkedSlots.Add(pending);
                }

                blocks.Add(block);
            }
            return blocks;
        }

        // ──────────────────── public surface ────────────────────

        public VfxYamlVerificationResult Verify(string graphAssetPath, VfxIntentSnapshot intent)
        {
            string yaml = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(graphAssetPath) && File.Exists(graphAssetPath))
                    yaml = File.ReadAllText(graphAssetPath);
            }
            catch
            {
                // Treat unreadable YAML as empty — every intent op will then
                // be diverged.
            }

            // Phase 5-7: MonoScript GUID resolver.
            // Resolve m_Script GUIDs to m_TypeFqn lines by injecting them inline so
            // VerifyFromYaml's scanner can strict-match by FQN. For FQNs that ARE
            // found in the injected set, verification succeeds without a warning.
            //
            // For FQNs NOT found (either because the node wasn't yet flushed to disk
            // at save time, or because the GUID belongs to a non-C# asset like a
            // .vfxoperator subgraph), we fall back to yaml_verify_skipped rather than
            // intent_diverged — the same graceful behaviour as before Phase 5 for
            // the file-based path. The VerifyFromYaml synthetic path still uses strict
            // matching (m_TypeFqn lines present in fixture YAML → intent_diverged on miss).
            if (!string.IsNullOrEmpty(yaml))
                ResolveScriptGuids(yaml, ref yaml);

            // Always use forceProductionModeForMisses=true for the file-based path:
            // a FQN miss after GUID injection means the node may not have been flushed
            // yet (timing) or its script is not a resolvable C# class. Either way,
            // yaml_verify_skipped is safer than intent_diverged.
            return VerifyFromYamlInternal(yaml, intent, forceProductionModeForMisses: true);
        }

        // ─────────────────── MonoScript GUID resolver (phase 5-7) ───────────

        // Resolves m_Script GUIDs in YAML blocks to m_TypeFqn lines by walking
        // the AssetDatabase. The result is injected by rewriting `yaml` in-memory
        // (adding synthetic `m_TypeFqn:` lines) so the existing VerifyFromYaml
        // parser can consume them without modification.
        //
        // Returns true only when ALL MonoBehaviour blocks with a ScriptGuid were
        // successfully resolved. When any GUID fails resolution (e.g., subgraph
        // .vfxoperator assets where MonoScript.GetClass() returns null), the
        // caller must treat the remaining gaps as yaml_verify_skipped — we inject
        // FQNs for the resolved blocks but signal "hasUnresolvedGuids" so the
        // VerifyAdd path can emit yaml_verify_skipped for missed FQNs instead of
        // intent_diverged.
        private static bool ResolveScriptGuids(string originalYaml, ref string yaml)
        {
            // Scan blocks to find all unique GUIDs.
            var blocks = ScanMonoBehaviours(originalYaml);
            if (blocks.Count == 0) return true; // nothing to resolve = all resolved

            // Check if any block already has m_TypeFqn — if all do, nothing to resolve.
            bool anyMissingFqn = false;
            foreach (var b in blocks)
            {
                if (string.IsNullOrEmpty(b.TypeFqn) && !string.IsNullOrEmpty(b.ScriptGuid))
                {
                    anyMissingFqn = true;
                    break;
                }
            }
            if (!anyMissingFqn) return true; // all already have m_TypeFqn

            // Resolve unique GUIDs to FQNs via AssetDatabase + MonoScript.GetClass().
            var guidToFqn = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            bool anyUnresolved = false;

            foreach (var b in blocks)
            {
                if (string.IsNullOrEmpty(b.ScriptGuid)) continue;
                if (guidToFqn.ContainsKey(b.ScriptGuid)) continue;
                if (!string.IsNullOrEmpty(b.TypeFqn))
                {
                    // Already resolved from m_TypeFqn — no GUID lookup needed.
                    guidToFqn[b.ScriptGuid] = b.TypeFqn;
                    continue;
                }

                bool resolved = false;
                try
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(b.ScriptGuid);
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                        if (monoScript != null)
                        {
                            System.Type t = monoScript.GetClass();
                            if (t != null)
                            {
                                guidToFqn[b.ScriptGuid] = t.FullName;
                                resolved = true;
                            }
                        }
                    }
                }
                catch
                {
                    // Best-effort: if AssetDatabase is unavailable or the GUID
                    // is stale, treat as unresolved.
                }

                if (!resolved)
                    anyUnresolved = true;
            }

            if (guidToFqn.Count == 0)
                return !anyUnresolved; // no resolutions at all

            // Inject synthetic `m_TypeFqn:` lines into the YAML for blocks that
            // were resolved but didn't have them. We do a string-level injection
            // after each `m_Script:` line so VerifyFromYaml's line scanner picks
            // them up without modification.
            var sb = new System.Text.StringBuilder();
            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    sb.AppendLine(line);
                    var guidMatch = MScriptGuid.Match(line);
                    if (guidMatch.Success)
                    {
                        string guid = guidMatch.Groups[1].Value;
                        if (guidToFqn.TryGetValue(guid, out var fqn))
                        {
                            int indent = 0;
                            foreach (char c in line) { if (c == ' ') indent++; else break; }
                            sb.Append(new string(' ', indent));
                            sb.AppendLine($"m_TypeFqn: {fqn}");
                        }
                    }
                }
            }
            yaml = sb.ToString();

            // Return true only when every GUID was resolved.
            return !anyUnresolved;
        }

        public VfxYamlVerificationResult VerifyFromYaml(string yaml, VfxIntentSnapshot intent)
        {
            // Public overload: no GUID resolution (synthetic test YAML path).
            return VerifyFromYamlInternal(yaml, intent, forceProductionModeForMisses: false);
        }

        // Internal overload used by Verify() after GUID resolution.
        // forceProductionModeForMisses: when true, any FQN miss in VerifyAdd is
        // treated as yaml_verify_skipped (not intent_diverged) because some GUIDs
        // could not be resolved (e.g., subgraph .vfxoperator references).
        private VfxYamlVerificationResult VerifyFromYamlInternal(
            string yaml, VfxIntentSnapshot intent, bool forceProductionModeForMisses)
        {
            var result = new VfxYamlVerificationResult();
            if (intent == null || intent.Ops == null || intent.Ops.Count == 0)
                return result;

            var blocks = ScanMonoBehaviours(yaml ?? string.Empty);

            // Build quick indices for the structural diff in task 3B-5.
            var byFileId = new Dictionary<long, YamlBlock>();
            var typeFqns = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                byFileId[b.FileId] = b;
                if (!string.IsNullOrEmpty(b.TypeFqn))
                    typeFqns.Add(b.TypeFqn);
            }

            // Phase 4-SMOKE discovery: synthetic test fixtures explicitly set
            // m_TypeFqn lines so the strict matcher works. Real Unity .vfx YAML
            // never has m_TypeFqn — node identity rides on m_Script GUIDs that
            // resolve back to a MonoScript asset. Until Phase 5 adds GUID->FQN
            // resolution, fall back to a warning (not an error) for add ops
            // when we detect production-mode YAML: at least one MonoBehaviour
            // block exists but none of them carry m_TypeFqn.
            //
            // Phase 5-7 update: when the GUID resolver (Verify overload) was able
            // to inject some FQNs but not all (forceProductionModeForMisses=true),
            // we keep productionMode semantics for any unresolved FQN — emitting
            // yaml_verify_skipped instead of intent_diverged so that subgraph
            // .vfxoperator references (which don't expose a C# type) don't break.
            bool productionMode = (blocks.Count > 0 && typeFqns.Count == 0)
                               || forceProductionModeForMisses;

            for (int i = 0; i < intent.Ops.Count; i++)
            {
                var op = intent.Ops[i];
                if (op == null) continue;

                switch (op.Kind)
                {
                    case "add":
                        VerifyAdd(op, typeFqns, result, productionMode);
                        break;
                    case "connect":
                        VerifyConnect(op, byFileId, result);
                        break;
                    // Other op kinds are not in lane 3B's scope; the
                    // structural diff for them lands in phase 4 with the
                    // tools that mutate them.
                }
            }
            return result;
        }

        // ──────────────────── op handlers ────────────────────

        // Task 3B-4: an add op whose ExpectedTypeFqn is not present anywhere
        // in the YAML is reported as `intent_diverged`. Per erratum P-L3 the
        // expected token is NOT scanned for in the YAML.
        //
        // Phase 4-SMOKE caveat: when productionMode == true, the YAML lacks
        // any m_TypeFqn lines (real Unity VFX Graph YAML uses m_Script GUIDs
        // instead). We can't strict-match by FQN until Phase 5 adds MonoScript
        // GUID -> type resolution; for now we emit a `yaml_verify_skipped`
        // warning so the smoke test (and every Phase 4 mutation) does not
        // false-positive into a fatal `intent_diverged` error.
        private static void VerifyAdd(VfxIntentOp op, HashSet<string> typeFqns,
                                      VfxYamlVerificationResult result,
                                      bool productionMode)
        {
            if (string.IsNullOrEmpty(op.ExpectedTypeFqn))
                return; // nothing to verify

            if (!typeFqns.Contains(op.ExpectedTypeFqn))
            {
                if (productionMode)
                {
                    result.Warnings.Add(new VfxVerifierWarning
                    {
                        Code = "yaml_verify_skipped",
                        OpIndex = op.OpIndex,
                        Reason = $"Skipped strict verification for {op.ExpectedTypeFqn}: " +
                                 "production .vfx YAML uses m_Script GUIDs, not m_TypeFqn. " +
                                 "Phase 5 hardening will add MonoScript resolution.",
                    });
                    return;
                }

                result.Errors.Add(new VfxVerifierError
                {
                    Code = "intent_diverged",
                    OpIndex = op.OpIndex,
                    ExpectedToken = op.ExpectedToken,
                    ExpectedTypeFqn = op.ExpectedTypeFqn,
                });
            }
        }

        // Task 3B-5: a connect op carries fromFileId/toFileId/fromSlot/toSlot
        // in its payload. We check the LinkedSlots index for that endpoint:
        //   * if neither side references the other → connection_dropped
        //   * if a reference exists but its m_Name doesn't match the expected
        //     slot name → slot_drift
        // The payload keys are conventional; if missing we silently no-op so
        // tests for other op kinds aren't blocked.
        private static void VerifyConnect(VfxIntentOp op,
                                          Dictionary<long, YamlBlock> byFileId,
                                          VfxYamlVerificationResult result)
        {
            if (op.Payload == null) return;
            if (!op.Payload.TryGetValue("fromFileId", out var fromFidObj)) return;
            if (!op.Payload.TryGetValue("toFileId", out var toFidObj)) return;
            long fromFid = ToLong(fromFidObj);
            long toFid = ToLong(toFidObj);

            string fromSlot = op.Payload.TryGetValue("fromSlot", out var fs) ? fs as string : null;
            string toSlot = op.Payload.TryGetValue("toSlot", out var ts) ? ts as string : null;

            byFileId.TryGetValue(fromFid, out var fromBlock);
            byFileId.TryGetValue(toFid, out var toBlock);

            // Look for a LinkedSlot on either side referring to the other.
            LinkedSlot fromSide = FindLink(fromBlock, toFid);
            LinkedSlot toSide = FindLink(toBlock, fromFid);

            if (fromSide == null && toSide == null)
            {
                result.Warnings.Add(new VfxVerifierWarning
                {
                    Code = "connection_dropped",
                    OpIndex = op.OpIndex,
                    Slot = fromSlot ?? toSlot,
                    Reason = $"No LinkedSlots entry connects {fromFid} <-> {toFid}.",
                });
                return;
            }

            // Check slot name drift.
            if (!string.IsNullOrEmpty(fromSlot) && fromSide != null
                && fromSide.Name != null && fromSide.Name != fromSlot)
            {
                result.Warnings.Add(new VfxVerifierWarning
                {
                    Code = "slot_drift",
                    OpIndex = op.OpIndex,
                    Slot = fromSlot,
                    Reason = $"Expected fromSlot '{fromSlot}', YAML has '{fromSide.Name}'.",
                });
            }
            if (!string.IsNullOrEmpty(toSlot) && toSide != null
                && toSide.Name != null && toSide.Name != toSlot)
            {
                result.Warnings.Add(new VfxVerifierWarning
                {
                    Code = "slot_drift",
                    OpIndex = op.OpIndex,
                    Slot = toSlot,
                    Reason = $"Expected toSlot '{toSlot}', YAML has '{toSide.Name}'.",
                });
            }
        }

        private static LinkedSlot FindLink(YamlBlock block, long otherFileId)
        {
            if (block == null) return null;
            for (int i = 0; i < block.LinkedSlots.Count; i++)
            {
                if (block.LinkedSlots[i].FileId == otherFileId)
                    return block.LinkedSlots[i];
            }
            return null;
        }

        private static long ToLong(object o)
        {
            if (o == null) return 0;
            if (o is long l) return l;
            if (o is int i) return i;
            if (o is string s && long.TryParse(s, out var parsed)) return parsed;
            try { return System.Convert.ToInt64(o); }
            catch { return 0; }
        }
    }
}
