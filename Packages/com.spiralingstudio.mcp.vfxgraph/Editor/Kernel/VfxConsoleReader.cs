// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxConsoleReader.cs
//
// Phase 3 Lane C, task 3C-2b. NEW kernel file (not git mv'd from Tools/Vfx/).
// The legacy file at Editor/Tools/Vfx/VfxConsoleReader.cs is removed in
// phase 7 via `git rm`; until then both files coexist and serve different
// callers. The legacy file is the authoritative reference for the ring-buffer
// pattern (its file header explicitly notes that UnityEditor.LogEntries
// reflection is broken on Unity 6 / 6000.x).
//
// ERRATUM P-H1 + A-H3: this file uses Application.logMessageReceived only.
// ZERO reflection — no Type.GetType, no MethodInfo.Invoke, no Assembly walks.
// The phase 5 no-reflection static analyzer enforces this rule against every
// file in Editor/Kernel/ without exclusions.
//
// Identity & monotonicity:
//   * Each captured log entry is stamped with a monotonically-increasing
//     `sequence` (long), independent of the ring buffer's wrap behavior.
//   * Snapshots returned by GetHighWaterMark() are opaque value-type tokens
//     wrapping the next-sequence-to-be-assigned. GetLinesSince(snapshot)
//     returns every retained entry whose sequence >= snapshot.Sequence.
//   * Entries older than BufferCapacity are dropped silently (ring overrun);
//     the high-water mark approach still works because newer entries keep
//     larger sequence numbers.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Kernel
{
    [InitializeOnLoad]
    internal static class VfxConsoleReader
    {
        private const int BufferCapacity = 500;

        private struct LogEntry
        {
            public long sequence;
            public string message;
            public string stackTrace;
            public LogType type;
            public DateTime timestamp;
        }

        /// <summary>
        /// Opaque snapshot token returned by <see cref="GetHighWaterMark"/>.
        /// Wraps the monotonic sequence the next captured log will receive.
        /// </summary>
        internal readonly struct Mark
        {
            public readonly long Sequence;
            public Mark(long sequence) { Sequence = sequence; }
        }

        private static readonly LogEntry[] _buffer = new LogEntry[BufferCapacity];
        private static int _writeIndex;
        private static int _count;
        private static long _nextSequence = 1;
        private static readonly object _lock = new object();

        static VfxConsoleReader()
        {
            // Idempotent across domain reloads — subtract first, then add.
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _buffer[_writeIndex] = new LogEntry
                {
                    sequence = _nextSequence++,
                    message = message ?? string.Empty,
                    stackTrace = stackTrace,
                    type = type,
                    timestamp = DateTime.UtcNow,
                };
                _writeIndex = (_writeIndex + 1) % BufferCapacity;
                if (_count < BufferCapacity) _count++;
            }
        }

        /// <summary>
        /// Returns an opaque token representing the current head of the log
        /// stream. Pass it to <see cref="GetLinesSince"/> later to retrieve
        /// only entries captured after this call.
        /// </summary>
        public static object GetHighWaterMark()
        {
            lock (_lock) return new Mark(_nextSequence);
        }

        /// <summary>
        /// Returns log message strings whose sequence is >= the snapshot's
        /// sequence. Older retained entries are filtered out. Entries that
        /// were evicted from the ring buffer are silently dropped.
        /// </summary>
        public static IReadOnlyList<string> GetLinesSince(object snapshot)
        {
            long since = snapshot is Mark m ? m.Sequence : 0;
            var results = new List<string>();
            lock (_lock)
            {
                int start = _count < BufferCapacity ? 0 : _writeIndex;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % BufferCapacity;
                    ref var entry = ref _buffer[idx];
                    if (entry.sequence >= since)
                        results.Add(entry.message);
                }
            }
            return results;
        }

        /// <summary>
        /// Verbose-mode helper used by vfx_diag.read_console. Returns retained
        /// entries with type, timestamp, and sequence metadata in addition to
        /// the message text. Same since-mark filtering as
        /// <see cref="GetLinesSince"/>.
        /// </summary>
        internal static List<(string message, LogType type, DateTime timestamp, long sequence)>
            GetDetailedSince(object snapshot)
        {
            long since = snapshot is Mark m ? m.Sequence : 0;
            var results = new List<(string, LogType, DateTime, long)>();
            lock (_lock)
            {
                int start = _count < BufferCapacity ? 0 : _writeIndex;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % BufferCapacity;
                    ref var entry = ref _buffer[idx];
                    if (entry.sequence >= since)
                        results.Add((entry.message, entry.type, entry.timestamp, entry.sequence));
                }
            }
            return results;
        }
    }
}
