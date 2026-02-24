using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal static class VfxAttributeAliases
    {
        internal readonly struct BlockAlias
        {
            internal readonly string InternalType;
            internal readonly string Description;
            internal readonly IReadOnlyDictionary<string, string> Settings;

            internal BlockAlias(string internalType, string description, Dictionary<string, string> settings = null)
            {
                InternalType = internalType;
                Description = description;
                Settings = settings;
            }
        }

        internal readonly struct AttributeInfo
        {
            internal readonly string Name;
            internal readonly string Type;
            internal readonly string DefaultValue;
            internal readonly string Category;
            internal readonly bool ReadOnly;
            internal readonly bool Variadic;

            internal AttributeInfo(string name, string type, string defaultValue, string category,
                bool readOnly = false, bool variadic = false)
            {
                Name = name; Type = type; DefaultValue = defaultValue;
                Category = category; ReadOnly = readOnly; Variadic = variadic;
            }
        }

        static readonly string[] WritableAttributes =
        {
            "position", "velocity", "color", "alpha", "size", "scale",
            "lifetime", "age", "angle", "angularVelocity", "mass",
            "direction", "oldPosition", "targetPosition", "pivot",
            "texIndex", "alive"
        };

        static readonly string[] Compositions = { "Overwrite", "Add", "Multiply", "Blend" };

        static Dictionary<string, BlockAlias> _blockAliases;

        static Dictionary<string, BlockAlias> EnsureBlockAliases()
        {
            if (_blockAliases != null) return _blockAliases;
            _blockAliases = new Dictionary<string, BlockAlias>(StringComparer.OrdinalIgnoreCase);

            foreach (var attr in WritableAttributes)
            {
                string cap = Capitalize(attr);
                foreach (var comp in Compositions)
                {
                    string prefix = comp == "Overwrite" ? "Set" : comp;
                    _blockAliases[prefix + cap] = MakeSetAttribute(attr, comp);
                }
                _blockAliases["InheritSource" + cap] = new BlockAlias("SetAttribute",
                    $"Inherit {attr} from source",
                    new Dictionary<string, string> { { "attribute", attr }, { "Composition", "Overwrite" }, { "Source", "Source" } });
            }

            AddDirect("SetPositionShape", "SetPosition_Shape", "Position from shape");
            AddDirect("SetPositionMesh", "SetPosition_Mesh", "Position from mesh surface");
            AddDirect("SetPositionSequential", "SetPosition_Sequential", "Position from sequence");
            AddDirect("SetPositionDepth", "SetPosition_Depth", "Position from depth buffer");
            AddDirect("SetAttributeFromCurve", "AttributeFromCurve", "Attribute from curve/gradient");
            AddDirect("SetAttributeFromMap", "AttributeFromMap", "Attribute from texture map");

            AddDirect("Gravity", "Gravity", "Gravity force");
            AddDirect("LinearDrag", "Drag", "Linear drag");
            AddDirect("Turbulence", "Turbulence", "Turbulence noise force");
            AddDirect("Force", "Force", "Constant force vector");
            AddDirect("ConformToSphere", "ConformToSphere", "Attract to sphere");
            AddDirect("ConformToSDF", "ConformToSignedDistanceField", "Attract to SDF");
            AddDirect("VectorFieldForce", "VectorForceField", "Force from vector field");

            AddDirect("CollisionShape", "CollisionShape", "Shape collision");
            AddDirect("CollisionDepthBuffer", "CollideWithDepthBuffer", "Depth buffer collision");
            AddDirect("KillShape", "KillShape", "Kill on shape contact");
            AddDirect("TriggerShape", "TriggerShape", "Shape collision trigger");

            AddDirect("ConstantSpawnRate", "VFXSpawnerConstantRate", "Constant spawn rate");
            AddDirect("VariableSpawnRate", "VFXSpawnerVariableRate", "Variable spawn rate");
            AddDirect("SpawnBurst", "VFXSpawnerBurst", "Single burst");
            AddDirect("PeriodicBurst", "VFXSpawnerPeriodicBurst", "Periodic bursts");

            AddDirect("TriggerEventOnDie", "TriggerEventOnDie", "GPU event on death");
            AddDirect("TriggerEventRate", "TriggerEventRate", "GPU event at rate");
            AddDirect("TriggerEventAlways", "TriggerEventAlways", "GPU event every frame");

            AddDirect("Orient", "Orient", "Particle orientation");
            AddDirect("FlipbookPlayer", "FlipbookPlayer", "Flipbook animation");
            AddDirect("CameraFade", "CameraFade", "Fade near camera");
            AddDirect("SizeOverLife", "SizeOverLife", "Size over lifetime");
            AddDirect("ColorOverLife", "ColorOverLife", "Color over lifetime");

            AddDirect("ConnectTarget", "ConnectTarget", "Connect particle strips to target");
            AddDirect("SetStripProgress", "SetAttribute",
                "Strip progress attribute");
            _blockAliases["SetStripProgress"] = MakeSetAttribute("stripProgress", "Overwrite");

            return _blockAliases;
        }

        static BlockAlias MakeSetAttribute(string attr, string composition)
        {
            return new BlockAlias("SetAttribute",
                $"{(composition == "Overwrite" ? "Set" : composition)} {attr}",
                new Dictionary<string, string> { { "attribute", attr }, { "Composition", composition } });
        }

        static void AddDirect(string alias, string internalType, string description)
        {
            _blockAliases[alias] = new BlockAlias(internalType, description);
        }

        internal static bool TryResolveBlock(string name, out BlockAlias alias)
        {
            return EnsureBlockAliases().TryGetValue(name, out alias);
        }

        internal static IReadOnlyDictionary<string, BlockAlias> AllBlockAliases => EnsureBlockAliases();

        internal static bool TryResolveGetAttribute(string name, out string attribute)
        {
            attribute = null;
            if (name == null || !name.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = name.Substring(3);
            foreach (var a in AllBuiltInAttributes)
            {
                if (string.Equals(suffix, a.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(suffix, Capitalize(a.Name), StringComparison.OrdinalIgnoreCase))
                {
                    attribute = a.Name;
                    return true;
                }
            }
            return false;
        }

        internal static readonly AttributeInfo[] AllBuiltInAttributes =
        {
            new AttributeInfo("position", "Vector3", "(0,0,0)", "simulation"),
            new AttributeInfo("velocity", "Vector3", "(0,0,0)", "simulation"),
            new AttributeInfo("age", "float", "0", "simulation"),
            new AttributeInfo("lifetime", "float", "0", "simulation"),
            new AttributeInfo("alive", "bool", "true", "simulation"),
            new AttributeInfo("size", "float", "0.1", "simulation"),
            new AttributeInfo("mass", "float", "1", "simulation"),
            new AttributeInfo("direction", "Vector3", "(0,0,1)", "simulation"),
            new AttributeInfo("angle", "Vector3", "(0,0,0)", "simulation", variadic: true),
            new AttributeInfo("angularVelocity", "Vector3", "(0,0,0)", "simulation", variadic: true),
            new AttributeInfo("oldPosition", "Vector3", "(0,0,0)", "simulation"),
            new AttributeInfo("targetPosition", "Vector3", "(0,0,0)", "simulation"),
            new AttributeInfo("color", "Vector3", "(1,1,1)", "rendering"),
            new AttributeInfo("alpha", "float", "1", "rendering"),
            new AttributeInfo("scale", "Vector3", "(1,1,1)", "rendering", variadic: true),
            new AttributeInfo("pivot", "Vector3", "(0,0,0)", "rendering"),
            new AttributeInfo("texIndex", "float", "0", "rendering"),
            new AttributeInfo("axisX", "Vector3", "(1,0,0)", "rendering"),
            new AttributeInfo("axisY", "Vector3", "(0,1,0)", "rendering"),
            new AttributeInfo("axisZ", "Vector3", "(0,0,1)", "rendering"),
            new AttributeInfo("particleId", "uint", "0", "system", readOnly: true),
            new AttributeInfo("seed", "uint", "0", "system", readOnly: true),
            new AttributeInfo("spawnCount", "float", "0", "system", readOnly: true),
            new AttributeInfo("spawnTime", "float", "0", "system", readOnly: true),
            new AttributeInfo("spawnIndex", "uint", "0", "system", readOnly: true),
            new AttributeInfo("particleIndexInStrip", "uint", "0", "system", readOnly: true),
            new AttributeInfo("particleCountInStrip", "uint", "0", "system", readOnly: true),
            new AttributeInfo("stripIndex", "uint", "0", "system", readOnly: true),
            new AttributeInfo("collisionEventCount", "uint", "0", "collision", readOnly: true),
            new AttributeInfo("collisionEventNormal", "Vector3", "(0,0,0)", "collision", readOnly: true),
            new AttributeInfo("collisionEventPosition", "Vector3", "(0,0,0)", "collision", readOnly: true),
            new AttributeInfo("hasCollisionEvent", "bool", "false", "collision", readOnly: true),
        };

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
