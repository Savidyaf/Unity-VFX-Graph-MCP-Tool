using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    /// <summary>
    /// Temporary workaround for the broken upstream read_console tool in Unity 6.
    /// Uses the stable Application.logMessageReceived callback instead of LogEntries reflection.
    /// Remove once upstream ReadConsole is fixed for Unity 6 (6000.x).
    /// </summary>
    [InitializeOnLoad]
    internal static class VfxConsoleReader
    {
        private const int BufferCapacity = 500;

        private struct LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public DateTime timestamp;
        }

        private static readonly LogEntry[] _buffer = new LogEntry[BufferCapacity];
        private static int _writeIndex;
        private static int _count;
        private static readonly object _lock = new object();

        static VfxConsoleReader()
        {
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _buffer[_writeIndex] = new LogEntry
                {
                    message = message?.Split('\n')[0] ?? "",
                    stackTrace = stackTrace,
                    type = type,
                    timestamp = DateTime.UtcNow
                };
                _writeIndex = (_writeIndex + 1) % BufferCapacity;
                if (_count < BufferCapacity) _count++;
            }
        }

        internal static List<object> GetEntries(string[] types, int count, string filterText)
        {
            var results = new List<object>();
            HashSet<LogType> allowedTypes = null;

            if (types != null && types.Length > 0)
            {
                allowedTypes = new HashSet<LogType>();
                foreach (var t in types)
                {
                    switch (t?.ToLowerInvariant())
                    {
                        case "error":
                            allowedTypes.Add(LogType.Error);
                            allowedTypes.Add(LogType.Exception);
                            allowedTypes.Add(LogType.Assert);
                            break;
                        case "warning":
                            allowedTypes.Add(LogType.Warning);
                            break;
                        case "log":
                            allowedTypes.Add(LogType.Log);
                            break;
                    }
                }
            }

            lock (_lock)
            {
                int start = _count < BufferCapacity ? 0 : _writeIndex;
                for (int i = _count - 1; i >= 0 && results.Count < count; i--)
                {
                    int idx = (start + i) % BufferCapacity;
                    ref var entry = ref _buffer[idx];

                    if (allowedTypes != null && !allowedTypes.Contains(entry.type))
                        continue;
                    if (!string.IsNullOrEmpty(filterText) &&
                        entry.message.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    results.Add(new
                    {
                        message = entry.message,
                        type = entry.type.ToString(),
                        timestamp = entry.timestamp.ToString("o"),
                        stackTrace = entry.stackTrace?.Split('\n').FirstOrDefault() ?? ""
                    });
                }
            }

            return results;
        }

        internal static object HandleAction(JObject @params)
        {
            var typesToken = @params["types"];
            string[] types = typesToken != null
                ? typesToken.ToObject<string[]>()
                : new[] { "error", "warning" };

            int count = @params["count"]?.ToObject<int>() ?? 10;
            string filterText = @params["filter_text"]?.ToString();

            var entries = GetEntries(types, count, filterText);

            return new
            {
                success = true,
                message = $"Retrieved {entries.Count} log entries",
                data = new
                {
                    entries,
                    total_buffered = _count,
                    buffer_capacity = BufferCapacity,
                    source = "VfxConsoleReader (temporary workaround)"
                }
            };
        }
    }
}
