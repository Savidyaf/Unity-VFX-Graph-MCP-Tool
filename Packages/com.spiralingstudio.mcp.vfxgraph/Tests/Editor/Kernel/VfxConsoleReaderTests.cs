// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxConsoleReaderTests.cs
//
// Phase 3 Lane C, task 3C-2b. Verifies the ring-buffer high-water-mark API.

using System.Linq;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxConsoleReaderTests
    {
        [Test]
        public void GetLinesSince_ReturnsLogsCapturedAfterMark()
        {
            var mark = VfxConsoleReader.GetHighWaterMark();
            Debug.Log("VfxConsoleReaderTests-marker-A");
            Debug.Log("VfxConsoleReaderTests-marker-B");

            var lines = VfxConsoleReader.GetLinesSince(mark);

            Assert.GreaterOrEqual(lines.Count, 2,
                $"Expected at least the two marker logs, got {lines.Count}.");
            Assert.IsTrue(
                lines.Any(l => l.Contains("VfxConsoleReaderTests-marker-A")),
                "Marker A should be present in lines since the mark.");
            Assert.IsTrue(
                lines.Any(l => l.Contains("VfxConsoleReaderTests-marker-B")),
                "Marker B should be present in lines since the mark.");
        }

        [Test]
        public void GetLinesSince_NoMarkerLogs_ReturnsNoneOfOurMarkers()
        {
            // Emit a marker so we can prove it lands BEFORE the snapshot, not
            // after. The editor may emit unrelated background logs after the
            // mark; we only assert that none of them carry our unique tag.
            Debug.Log("VfxConsoleReaderTests-pre-empty-uniqueZ");
            var mark = VfxConsoleReader.GetHighWaterMark();

            var lines = VfxConsoleReader.GetLinesSince(mark);

            foreach (var line in lines)
            {
                Assert.IsFalse(line.Contains("VfxConsoleReaderTests-pre-empty-uniqueZ"),
                    "The pre-mark marker should not appear in the post-mark window.");
            }
        }

        [Test]
        public void GetHighWaterMark_IsMonotonic()
        {
            var first = VfxConsoleReader.GetHighWaterMark();
            Debug.Log("VfxConsoleReaderTests-monotonic-1");
            var second = VfxConsoleReader.GetHighWaterMark();
            Debug.Log("VfxConsoleReaderTests-monotonic-2");
            var third = VfxConsoleReader.GetHighWaterMark();

            Assert.IsInstanceOf<VfxConsoleReader.Mark>(first);
            var f = (VfxConsoleReader.Mark)first;
            var s = (VfxConsoleReader.Mark)second;
            var t = (VfxConsoleReader.Mark)third;
            Assert.Less(f.Sequence, s.Sequence);
            Assert.Less(s.Sequence, t.Sequence);
        }

        [Test]
        public void GetDetailedSince_IncludesTypeAndSequence()
        {
            var mark = VfxConsoleReader.GetHighWaterMark();
            Debug.LogWarning("VfxConsoleReaderTests-detailed-warning");

            var detailed = VfxConsoleReader.GetDetailedSince(mark);

            Assert.GreaterOrEqual(detailed.Count, 1);
            Assert.IsTrue(detailed.Any(e =>
                e.message.Contains("VfxConsoleReaderTests-detailed-warning") &&
                e.type == LogType.Warning &&
                e.sequence > 0));
        }
    }
}
