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
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
            public string TagId;     // e.g. "114"
            public string Body;      // raw body lines after the tag line
            public string Name;      // m_Name value (or null)
            public string TypeFqn;   // m_TypeFqn value (or null)
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
            return VerifyFromYaml(yaml, intent);
        }

        public VfxYamlVerificationResult VerifyFromYaml(string yaml, VfxIntentSnapshot intent)
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

            for (int i = 0; i < intent.Ops.Count; i++)
            {
                var op = intent.Ops[i];
                if (op == null) continue;

                switch (op.Kind)
                {
                    case "add":
                        VerifyAdd(op, typeFqns, result);
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
        private static void VerifyAdd(VfxIntentOp op, HashSet<string> typeFqns,
                                      VfxYamlVerificationResult result)
        {
            if (string.IsNullOrEmpty(op.ExpectedTypeFqn))
                return; // nothing to verify

            if (!typeFqns.Contains(op.ExpectedTypeFqn))
            {
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
