// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleCorrelator.cs
//
// Phase 3 Lane C, task 3C-3. Correlates console output captured between
// SnapshotBefore() and CorrelateAfter(...) against the intent ops the
// transaction recorded, so dropped-slot warnings can be attributed to the
// connect op that caused them.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxConsoleCorrelator : IVfxConsoleCorrelator
    {
        // Matches the well-known Unity warning emitted when an editor save
        // discards slot connections that could not be deserialized. Capture
        // group 1 is the slot path the warning references.
        //
        // Example real text:
        //   "Remove 1 linked slot(s) that couldn't be deserialized from foo.x"
        private static readonly Regex DroppedSlotPattern = new Regex(
            @"Remove (\d+) linked slot\(s\) that couldn't be deserialized from (.+)",
            RegexOptions.Compiled);

        public object SnapshotBefore()
        {
            return VfxConsoleReader.GetHighWaterMark();
        }

        public VfxConsoleCorrelation CorrelateAfter(object snapshot, VfxIntentSnapshot intent)
        {
            var correlation = new VfxConsoleCorrelation();
            var lines = VfxConsoleReader.GetLinesSince(snapshot);

            foreach (var line in lines)
            {
                var match = DroppedSlotPattern.Match(line);
                if (!match.Success)
                {
                    correlation.RawLines.Add(line);
                    continue;
                }

                string slotPath = match.Groups[2].Value.Trim();
                int opIndex = FindMatchingConnectOp(intent, slotPath);

                correlation.Correlated.Add(new VfxCorrelatedWarning
                {
                    Code = "connection_dropped_at_save",
                    OpIndex = opIndex,
                    RawLine = line,
                });
            }

            return correlation;
        }

        private static int FindMatchingConnectOp(VfxIntentSnapshot intent, string slotPath)
        {
            if (intent == null || intent.Ops == null) return -1;

            for (int i = 0; i < intent.Ops.Count; i++)
            {
                var op = intent.Ops[i];
                if (op == null || op.Payload == null) continue;
                if (!string.Equals(op.Kind, "connect", System.StringComparison.Ordinal))
                    continue;

                if (op.Payload.TryGetValue("toSlot", out var rawToSlot) &&
                    rawToSlot is string toSlot &&
                    !string.IsNullOrEmpty(toSlot) &&
                    SlotMatches(slotPath, toSlot))
                {
                    return op.OpIndex;
                }

                if (op.Payload.TryGetValue("fromSlot", out var rawFromSlot) &&
                    rawFromSlot is string fromSlot &&
                    !string.IsNullOrEmpty(fromSlot) &&
                    SlotMatches(slotPath, fromSlot))
                {
                    return op.OpIndex;
                }
            }
            return -1;
        }

        private static bool SlotMatches(string consoleSlotPath, string intentSlotPath)
        {
            // Loose containment match: Unity's warning prints a path that may
            // include a parent prefix, while the intent op stores the slot
            // name as authored by the caller. Match either direction to be
            // tolerant of formatting differences.
            return consoleSlotPath.Contains(intentSlotPath)
                || intentSlotPath.Contains(consoleSlotPath);
        }
    }
}
