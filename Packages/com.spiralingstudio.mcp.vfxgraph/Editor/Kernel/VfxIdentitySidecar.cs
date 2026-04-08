// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxIdentitySidecar.cs
//
// Lane 3B task 3B-2: persistent JSON sidecar that maps
// (graphGuid, token) -> { fingerprint, typeFqn, parentFingerprint }.
//
// Erratum P-H3 — schema v2:
//   {
//     "version": 2,
//     "assets": {
//       "<graphGuid>": {
//         "<token>": { "fp": "<hex>", "type": "<fqn>", "parentFp": "<hex>" }
//       }
//     }
//   }
//
// The recovery loop in VfxIdentity needs `type` and `parentFp` to filter
// candidates without ambiguity, so the v2 schema stores them at mint time.
//
// A v1 -> v2 loader is provided so existing files keep working: legacy
// flat entries (Token -> "<hex>") are migrated in-memory with null type
// and zero parent fingerprint. Recovery for those legacy entries only
// works on the strict-match path (per erratum P-H3).
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxIdentitySidecar
    {
        private const int CurrentSchemaVersion = 2;

        // Internal serialization shapes — public surface uses ulong primitives.
        internal sealed class Record
        {
            [JsonProperty("fp")]
            public string Fp;

            [JsonProperty("type")]
            public string Type;

            [JsonProperty("parentFp")]
            public string ParentFp;
        }

        internal sealed class FileShape
        {
            [JsonProperty("version")]
            public int Version;

            [JsonProperty("assets")]
            public Dictionary<string, Dictionary<string, Record>> Assets;
        }

        private readonly string _path;
        private Dictionary<string, Dictionary<string, Record>> _data;
        private bool _dirty;

        public VfxIdentitySidecar(string path = "Library/VfxMcpIdentity.json")
        {
            _path = path;
            Load();
        }

        private void Load()
        {
            _data = new Dictionary<string, Dictionary<string, Record>>();
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
                return;

            string json;
            try
            {
                json = File.ReadAllText(_path);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json))
                return;

            // Try v2 envelope first.
            try
            {
                var shape = JsonConvert.DeserializeObject<FileShape>(json);
                if (shape != null && shape.Version == CurrentSchemaVersion && shape.Assets != null)
                {
                    _data = shape.Assets;
                    return;
                }
            }
            catch
            {
                // Fall through to v1 attempt.
            }

            // v1 fallback: Dictionary<guid, Dictionary<token, "<hex>">>.
            try
            {
                var v1 = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                if (v1 != null)
                {
                    foreach (var asset in v1)
                    {
                        if (asset.Value == null) continue;
                        var inner = new Dictionary<string, Record>();
                        foreach (var entry in asset.Value)
                        {
                            inner[entry.Key] = new Record
                            {
                                Fp = entry.Value,
                                Type = null,      // legacy entries lack type
                                ParentFp = null,  // legacy entries lack parent fp
                            };
                        }
                        _data[asset.Key] = inner;
                    }
                }
            }
            catch
            {
                // Corrupt file — start fresh, do NOT throw on construction.
                _data = new Dictionary<string, Dictionary<string, Record>>();
            }
        }

        public void Put(string graphGuid, string token, ulong fingerprint,
                        string typeFqn = null, ulong parentFingerprint = 0)
        {
            if (!_data.TryGetValue(graphGuid, out var inner))
            {
                inner = new Dictionary<string, Record>();
                _data[graphGuid] = inner;
            }
            inner[token] = new Record
            {
                Fp = fingerprint.ToString("x16"),
                Type = typeFqn,
                ParentFp = parentFingerprint != 0 ? parentFingerprint.ToString("x16") : null,
            };
            _dirty = true;
        }

        public ulong Get(string graphGuid, string token)
        {
            if (!_data.TryGetValue(graphGuid, out var inner)) return 0UL;
            if (!inner.TryGetValue(token, out var rec) || string.IsNullOrEmpty(rec?.Fp)) return 0UL;
            return ulong.Parse(rec.Fp, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public string GetTypeFqn(string graphGuid, string token)
        {
            if (!_data.TryGetValue(graphGuid, out var inner)) return null;
            if (!inner.TryGetValue(token, out var rec)) return null;
            return rec?.Type;
        }

        public ulong GetParentFingerprint(string graphGuid, string token)
        {
            if (!_data.TryGetValue(graphGuid, out var inner)) return 0UL;
            if (!inner.TryGetValue(token, out var rec) || string.IsNullOrEmpty(rec?.ParentFp)) return 0UL;
            return ulong.Parse(rec.ParentFp, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public void Flush()
        {
            if (!_dirty) return;
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var shape = new FileShape { Version = CurrentSchemaVersion, Assets = _data };
            File.WriteAllText(_path, JsonConvert.SerializeObject(shape, Formatting.Indented));
            _dirty = false;
        }
    }
}
