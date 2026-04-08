// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxConsoleCorrelatorTests.cs
//
// Phase 3 Lane C, task 3C-3. Synthetic console messages are emitted via
// Debug.LogWarning between SnapshotBefore() and CorrelateAfter(...), so the
// test exercises the regex + intent-op lookup without needing a real graph.

using System.Collections.Generic;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxConsoleCorrelatorTests
    {
        [Test]
        public void CorrelateAfter_DroppedSlot_MatchesConnectOp()
        {
            var correlator = new VfxConsoleCorrelator();
            var intent = new VfxIntentSnapshot
            {
                CorrelationId = "test-1",
                Ops = new List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "connect",
                        Payload = new Dictionary<string, object>
                        {
                            { "fromSlot", "color" },
                            { "toSlot", "VfxCorrelatorTest1_uniqueA.x" },
                        },
                    },
                },
            };

            var snapshot = correlator.SnapshotBefore();
            Debug.LogWarning("Remove 1 linked slot(s) that couldn't be deserialized from VfxCorrelatorTest1_uniqueA.x");

            var correlation = correlator.CorrelateAfter(snapshot, intent);

            // Filter to lines that mention our unique marker so concurrent
            // editor logs cannot pollute the assertion.
            var ours = FindCorrelated(correlation, "VfxCorrelatorTest1_uniqueA.x");
            Assert.AreEqual(1, ours.Count, "Expected exactly one correlated warning for our marker.");
            Assert.AreEqual("connection_dropped_at_save", ours[0].Code);
            Assert.AreEqual(0, ours[0].OpIndex);
            StringAssert.Contains("VfxCorrelatorTest1_uniqueA.x", ours[0].RawLine);
        }

        [Test]
        public void CorrelateAfter_UnknownLine_GoesToRawLines()
        {
            var correlator = new VfxConsoleCorrelator();
            var intent = new VfxIntentSnapshot
            {
                CorrelationId = "test-2",
                Ops = new List<VfxIntentOp>(),
            };

            var snapshot = correlator.SnapshotBefore();
            Debug.LogWarning("VfxCorrelatorTest2_random-warning-no-pattern");

            var correlation = correlator.CorrelateAfter(snapshot, intent);

            // The unknown line must land in RawLines, and it must NOT have
            // generated a "connection_dropped_at_save" entry for our marker.
            bool foundRaw = false;
            foreach (var line in correlation.RawLines)
            {
                if (line.Contains("VfxCorrelatorTest2_random-warning-no-pattern"))
                {
                    foundRaw = true;
                    break;
                }
            }
            Assert.IsTrue(foundRaw, "Unknown warning should land in RawLines.");

            foreach (var w in correlation.Correlated)
            {
                Assert.IsFalse(w.RawLine.Contains("VfxCorrelatorTest2_random-warning-no-pattern"),
                    "Unknown warning must not be classified as a correlated warning.");
            }
        }

        [Test]
        public void CorrelateAfter_DroppedSlot_NoMatchingOp_OpIndexIsMinusOne()
        {
            var correlator = new VfxConsoleCorrelator();
            var intent = new VfxIntentSnapshot
            {
                CorrelationId = "test-3",
                Ops = new List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 7,
                        Kind = "connect",
                        Payload = new Dictionary<string, object>
                        {
                            { "toSlot", "VfxCorrelatorTest3_different_slot" },
                        },
                    },
                },
            };

            var snapshot = correlator.SnapshotBefore();
            Debug.LogWarning("Remove 2 linked slot(s) that couldn't be deserialized from VfxCorrelatorTest3_unrelated_field");

            var correlation = correlator.CorrelateAfter(snapshot, intent);

            var ours = FindCorrelated(correlation, "VfxCorrelatorTest3_unrelated_field");
            Assert.AreEqual(1, ours.Count, "Expected exactly one correlated warning for our marker.");
            Assert.AreEqual(-1, ours[0].OpIndex,
                "No intent op contained the dropped slot path; expected -1.");
        }

        private static List<VfxCorrelatedWarning> FindCorrelated(VfxConsoleCorrelation c, string marker)
        {
            var matches = new List<VfxCorrelatedWarning>();
            foreach (var w in c.Correlated)
            {
                if (w.RawLine != null && w.RawLine.Contains(marker))
                    matches.Add(w);
            }
            return matches;
        }
    }
}
