// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Emitters/OverridesEmitter.cs
// Phase 3 task 3A-7: emits Editor/Generated/VfxOverrides.g.cs.
//
// Phase 5: reads Quirks.yaml + Hints.yaml via a minimal hand-rolled parser
// (the YAML files use a simple flat key:value schema, one level of nesting)
// and bakes the entries into static dictionaries in the generated file.
// No external YAML dependency is required.
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpiralingStudio.VfxMcp.Generation
{
    internal sealed class OverridesEmitter
    {
        // ─────────────────────── minimal YAML parser ───────────────────────
        // Only supports the subset used by Quirks.yaml + Hints.yaml:
        //   top-level sections (e.g. "collisions:"), subsections
        //   (e.g. "  Lerp: FQN"), and two-level-deep subsections
        //   (e.g. "  TypeFqn:\n    settingKey: value").
        // Lines starting with # are comments; blank lines are skipped.
        // Values are trimmed; no quoting or escaping is handled beyond that.

        private sealed class QuirksData
        {
            public Dictionary<string, string> Collisions = new Dictionary<string, string>();
            public Dictionary<string, string> Aliases = new Dictionary<string, string>();
            public Dictionary<string, Dictionary<string, string>> Defaults
                = new Dictionary<string, Dictionary<string, string>>();
        }

        private sealed class HintsData
        {
            public Dictionary<string, string> Hints = new Dictionary<string, string>();
        }

        // Returns the indent level (number of leading spaces / 2).
        private static int IndentLevel(string line)
        {
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == ' ') spaces++;
                else break;
            }
            return spaces / 2;
        }

        private static QuirksData ParseQuirks(string yaml)
        {
            var data = new QuirksData();
            if (string.IsNullOrEmpty(yaml)) return data;

            string section = null;
            string subsection = null; // for two-level nesting (defaults.<typeFqn>)

            foreach (var rawLine in yaml.Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                int indent = IndentLevel(line);
                string trimmed = line.TrimStart();

                // Skip empty-map stubs ({})
                if (trimmed == "collisions: {}" || trimmed == "aliases: {}")
                    continue;

                // Top-level section header (indent 0, ends with ':', no value after)
                if (indent == 0 && trimmed.EndsWith(":") && !trimmed.Contains(": "))
                {
                    section = trimmed.TrimEnd(':').Trim();
                    subsection = null;
                    continue;
                }

                if (section == null) continue;

                int colon = trimmed.IndexOf(':');
                if (colon < 0) continue;

                string key = trimmed.Substring(0, colon).Trim();
                string value = trimmed.Substring(colon + 1).Trim();

                // Strip surrounding quotes if present
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);

                switch (section)
                {
                    case "collisions":
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            data.Collisions[key] = value;
                        break;

                    case "aliases":
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            data.Aliases[key] = value;
                        break;

                    case "defaults":
                        if (indent == 1 && string.IsNullOrEmpty(value))
                        {
                            // subsection header (e.g. "  UnityEditor.VFX.VFXInlineOperator:")
                            subsection = key;
                            if (!data.Defaults.ContainsKey(subsection))
                                data.Defaults[subsection] = new Dictionary<string, string>();
                        }
                        else if (indent == 2 && subsection != null && !string.IsNullOrEmpty(value))
                        {
                            data.Defaults[subsection][key] = value;
                        }
                        break;
                }
            }
            return data;
        }

        private static HintsData ParseHints(string yaml)
        {
            var data = new HintsData();
            if (string.IsNullOrEmpty(yaml)) return data;

            bool inHints = false;
            foreach (var rawLine in yaml.Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                string trimmed = line.TrimStart();
                int indent = IndentLevel(line);

                if (indent == 0 && trimmed == "hints:")
                {
                    inHints = true;
                    continue;
                }

                if (!inHints) continue;
                if (indent == 0) { inHints = false; continue; }

                int colon = trimmed.IndexOf(':');
                if (colon < 0) continue;

                string key = trimmed.Substring(0, colon).Trim();
                string value = trimmed.Substring(colon + 1).Trim();

                // Strip surrounding quotes if present
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    data.Hints[key] = value;
            }
            return data;
        }

        // ─────────────────────── emitter ───────────────────────────────────

        private const string QuirksPath =
            "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Quirks.yaml";
        private const string HintsPath =
            "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/Hints.yaml";

        public string Emit(CatalogIR ir)
        {
            // Load YAML data. If files are missing/unreadable we emit empty
            // dictionaries (same behaviour as the phase-3 stub).
            string quirksYaml = string.Empty;
            string hintsYaml = string.Empty;
            try { if (File.Exists(QuirksPath)) quirksYaml = File.ReadAllText(QuirksPath); }
            catch { }
            try { if (File.Exists(HintsPath)) hintsYaml = File.ReadAllText(HintsPath); }
            catch { }

            var quirks = ParseQuirks(quirksYaml);
            var hints = ParseHints(hintsYaml);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Do not edit. Regenerate via Tools/VFX MCP/Regenerate Catalog.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace SpiralingStudio.VfxMcp.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Override layer baked from Quirks.yaml + Hints.yaml.");
            sb.AppendLine("    /// Returns null when no override exists for the given key.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    internal static class VfxOverrides");
            sb.AppendLine("    {");

            // _collisions
            sb.AppendLine("        private static readonly Dictionary<string, string> _collisions =");
            sb.AppendLine("            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("            {");
            foreach (var kv in quirks.Collisions)
                sb.AppendLine($"                {{ \"{EscapeCs(kv.Key)}\", \"{EscapeCs(kv.Value)}\" }},");
            sb.AppendLine("            };");
            sb.AppendLine();

            // _aliases
            sb.AppendLine("        private static readonly Dictionary<string, string> _aliases =");
            sb.AppendLine("            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("            {");
            foreach (var kv in quirks.Aliases)
                sb.AppendLine($"                {{ \"{EscapeCs(kv.Key)}\", \"{EscapeCs(kv.Value)}\" }},");
            sb.AppendLine("            };");
            sb.AppendLine();

            // _hints
            sb.AppendLine("        private static readonly Dictionary<string, string> _hints =");
            sb.AppendLine("            new Dictionary<string, string>(System.StringComparer.Ordinal)");
            sb.AppendLine("            {");
            foreach (var kv in hints.Hints)
                sb.AppendLine($"                {{ \"{EscapeCs(kv.Key)}\", \"{EscapeCs(kv.Value)}\" }},");
            sb.AppendLine("            };");
            sb.AppendLine();

            // Methods
            sb.AppendLine("        /// <summary>Resolve a short-name collision to a single FQN, or null when no override exists.</summary>");
            sb.AppendLine("        public static string ResolveCollision(string shortName)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (shortName == null) return null;");
            sb.AppendLine("            return _collisions.TryGetValue(shortName, out var fqn) ? fqn : null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Look up a hint template for an error code, or null when none is registered.</summary>");
            sb.AppendLine("        public static string GetHint(string errorCode)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (errorCode == null) return null;");
            sb.AppendLine("            return _hints.TryGetValue(errorCode, out var hint) ? hint : null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Look up a quirk-defined alias to its raw catalog entry, or null when none is registered.</summary>");
            sb.AppendLine("        public static string ResolveAlias(string alias)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (alias == null) return null;");
            sb.AppendLine("            return _aliases.TryGetValue(alias, out var raw) ? raw : null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeCs(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
