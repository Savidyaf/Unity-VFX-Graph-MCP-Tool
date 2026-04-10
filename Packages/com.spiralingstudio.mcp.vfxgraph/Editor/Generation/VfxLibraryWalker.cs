// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs
// Phase 2 tasks 7 + 8 + Phase 3 task 3A-1: walker over VFXLibrary descriptors.
// Task 8 extends to blocks/contexts/parameters/subgraphs and applies the
// typeof()-based subgraph split per erratum P-B5.
// Task 3A-1 captures per-node settings and input/output slot bindings on a
// transient template instance, applying erratum P-B2 (unwrap SerializableType
// via the implicit Type operator) and erratum P-H2 (preserve the raw m_*
// field name in SettingDescriptor.Name).
// v0.3.2 F10 catalog lift (Cluster W3-C): adds WalkAttributes() +
// WalkSettings() surfaces so CatalogEmitter can emit Attributes[] and the
// Settings dict. Field-attribute reflection is a build-time activity, not
// runtime dispatch, so it does NOT violate the no-runtime-reflection rule.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// Walks VFXLibrary.Get*() descriptors and emits a CatalogIR.
    /// Uses VFXLibrary (the same source Unity's add-node UI uses) rather
    /// than raw assembly reflection, so abstract/deprecated/test-only types
    /// are automatically filtered.
    /// </summary>
    internal sealed class VfxLibraryWalker
    {
        /// <summary>
        /// Convenience wrapper that walks the catalog and disposes the
        /// transient templates afterwards. Use <see cref="WalkWithTemplates"/>
        /// when you need the live VFXModel templates (e.g. to feed
        /// VfxSlotTreeBuilder).
        /// </summary>
        public CatalogIR Walk()
        {
            var (ir, templates) = WalkWithTemplates();
            foreach (var t in templates)
            {
                if (t != null) Object.DestroyImmediate(t);
            }
            return ir;
        }

        /// <summary>
        /// Walks VFXLibrary descriptors and returns the IR alongside the
        /// transient template VFXModel instances created during the walk so
        /// downstream pipeline stages (slot tree builder) can introspect the
        /// live <see cref="VFXSlot"/> trees. Caller is responsible for
        /// destroying the templates after the slot tree builder runs.
        /// </summary>
        public (CatalogIR ir, List<VFXModel> templates) WalkWithTemplates()
        {
            var ir = new CatalogIR { VfxGraphPackageVersion = "17.4.0" };
            var templates = new List<VFXModel>();

            foreach (var d in VFXLibrary.GetOperators())
                ir.Operators.Add(MakeDescriptor(d.modelType, d.category, templates));

            foreach (var d in VFXLibrary.GetBlocks())
                ir.Blocks.Add(MakeDescriptor(d.modelType, d.category, templates));

            foreach (var d in VFXLibrary.GetContexts())
                ir.Contexts.Add(MakeDescriptor(d.modelType, d.category, templates));

            foreach (var d in VFXLibrary.GetParameters())
                ir.Parameters.Add(MakeDescriptor(d.modelType, d.category, templates));

            // ERRATUM P-B5: subgraphs live in namespace UnityEditor.VFX (NOT
            // .Operator./.Block./.Context.). Use typeof() comparison, not strings.
            // NodeDescriptor carries the live System.Type captured at walk time.
            foreach (var op in ir.Operators.ToArray())
                if (op.ModelType == typeof(UnityEditor.VFX.VFXSubgraphOperator))
                {
                    ir.SubgraphOperators.Add(op);
                    ir.Operators.Remove(op);
                }
            foreach (var blk in ir.Blocks.ToArray())
                if (blk.ModelType == typeof(UnityEditor.VFX.VFXSubgraphBlock))
                {
                    ir.SubgraphBlocks.Add(blk);
                    ir.Blocks.Remove(blk);
                }
            foreach (var ctx in ir.Contexts.ToArray())
                if (ctx.ModelType == typeof(UnityEditor.VFX.VFXSubgraphContext))
                {
                    ir.SubgraphContexts.Add(ctx);
                    ir.Contexts.Remove(ctx);
                }

            return (ir, templates);
        }

        /// <summary>
        /// F10 catalog lift: returns the canonical list of VFX built-in
        /// attribute names sourced from VFXAttributesManager (reachable via
        /// the InternalsVisibleTo grant in the Unity VFX package). Called at
        /// catalog-regen time; caller is the emitter, not runtime dispatch.
        ///
        /// The "all-true" combination is intentional: we want every variadic
        /// wrapper, every component, every read-only and every write-only
        /// built-in on the surface so LLM callers can inspect the full set
        /// (filtering is a caller-side concern).
        /// </summary>
        public static string[] WalkAttributes()
        {
            try
            {
                return VFXAttributesManager
                    .GetBuiltInNamesOrCombination(true, true, true, true)
                    .Distinct(System.StringComparer.Ordinal)
                    .OrderBy(n => n, System.StringComparer.Ordinal)
                    .ToArray();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[VFX MCP] VfxLibraryWalker.WalkAttributes fell back to hardcoded list: {ex.Message}");
                // Hardcoded canonical Unity 6000.4 / VFX Graph 17 fallback —
                // matches the s_BuiltInAttributes list in VFXAttributesManager.
                var fallback = new[]
                {
                    "age", "alive", "alpha", "angleX", "angleY", "angleZ",
                    "angularVelocityX", "angularVelocityY", "angularVelocityZ",
                    "axisX", "axisY", "axisZ",
                    "color", "direction", "eventCount", "lifetime", "mass",
                    "oldPosition", "particleId", "pivotX", "pivotY", "pivotZ",
                    "position", "scaleX", "scaleY", "scaleZ",
                    "seed", "size", "spawnCount", "spawnTime",
                    "targetPosition", "texIndex", "velocity",
                };
                System.Array.Sort(fallback, System.StringComparer.Ordinal);
                return fallback;
            }
        }

        /// <summary>
        /// F10 catalog lift: walks every VFX model type already surfaced by
        /// the catalog (operators + contexts + blocks + parameters) and
        /// reflects their [VFXSetting]-attributed fields. Returns an FQN ->
        /// settings[] map sorted deterministically. Types with zero settings
        /// are omitted. Field-attribute reflection is build-time — no runtime
        /// dispatch involved.
        /// </summary>
        public static Dictionary<string, string[]> WalkSettings(CatalogIR ir)
        {
            var result = new Dictionary<string, string[]>(System.StringComparer.Ordinal);
            if (ir == null) return result;

            IEnumerable<NodeDescriptor> allNodes = ir.Operators
                .Concat(ir.Contexts)
                .Concat(ir.Blocks)
                .Concat(ir.Parameters);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var node in allNodes)
            {
                var t = node.ModelType;
                if (t == null) continue;
                if (result.ContainsKey(node.TypeFQN)) continue;

                var fields = t.GetFields(flags)
                    .Where(f => f.GetCustomAttributes(typeof(VFXSettingAttribute), true).Any())
                    .Select(f => f.Name)
                    .Distinct(System.StringComparer.Ordinal)
                    .OrderBy(n => n, System.StringComparer.Ordinal)
                    .ToArray();

                if (fields.Length > 0)
                    result[node.TypeFQN] = fields;
            }

            return result;
        }

        /// <summary>
        /// Builds a NodeDescriptor for the given model type. Instantiates a
        /// transient template (added to <paramref name="templates"/>) so we
        /// can read VFXModel.GetSettings(...) and IVFXSlotContainer.inputSlots/
        /// outputSlots from the same surface Unity's UI uses. The template
        /// MUST be destroyed by the caller (Walk does this; WalkWithTemplates
        /// hands the list off to the caller).
        /// </summary>
        private static NodeDescriptor MakeDescriptor(System.Type modelType, string category,
                                                     List<VFXModel> templates)
        {
            var desc = new NodeDescriptor
            {
                ModelType = modelType,          // erratum P-B5: store live Type for typeof()-based split
                TypeFQN = modelType.FullName,
                ShortName = modelType.Name,
                Category = category ?? "",
                VariantKey = "",
            };

            VFXModel template;
            try
            {
                template = ScriptableObject.CreateInstance(modelType) as VFXModel;
            }
            catch
            {
                // Some model types may refuse default construction; the
                // descriptor's metadata is still useful, just without the
                // deeper shape information.
                return desc;
            }

            if (template == null) return desc;
            templates.Add(template);

            // ───────────────── Settings ─────────────────
            // ERRATUM P-H2: store the raw C# field name ("m_Type", "m_HLSLCode", …)
            // on SettingDescriptor.Name. The emitter will surface a stripped
            // short alias next to the raw name.
            foreach (var setting in template.GetSettings(listHidden: true))
            {
                object rawValue = null;
                try { rawValue = setting.value; } catch { rawValue = null; }

                // ERRATUM P-B2: setting.value calls FieldInfo.GetValue. For
                // SerializableType-typed fields (e.g. VFXInlineOperator.m_Type)
                // it returns the wrapper object — unwrap it via the implicit
                // Type cast (VFXSerializer.cs:22) so we get the underlying
                // System.Type's FullName. Source path:
                //   Packages/com.unity.visualeffectgraph/Editor/Core/VFXSerializer.cs:15
                string typeFqn;
                if (rawValue is UnityEditor.VFX.SerializableType st)
                {
                    var inner = (System.Type)st;
                    typeFqn = inner?.FullName ?? "System.Object";
                }
                else if (rawValue != null)
                {
                    typeFqn = rawValue.GetType().FullName;
                }
                else
                {
                    typeFqn = setting.field?.FieldType.FullName ?? "System.Object";
                }

                desc.Settings.Add(new SettingDescriptor
                {
                    Name = setting.name,        // raw "m_Type" / "m_HLSLCode" — erratum P-H2
                    TypeFQN = typeFqn,
                    DefaultLiteral = null,
                    IsHidden = (setting.visibility & VFXSettingAttribute.VisibleFlags.InInspector) == 0,
                });
            }

            // ───────────────── Input / output slot bindings ─────────────────
            // We only record the top-level slot binding here (name + type FQN +
            // index). The recursive child decomposition is handled by
            // VfxSlotTreeBuilder which walks live VFXSlot.children.
            if (template is IVFXSlotContainer container)
            {
                int i = 0;
                foreach (var slot in container.inputSlots)
                {
                    desc.InputSlots.Add(new SlotBindingDescriptor
                    {
                        Name = slot.name,
                        SlotTypeFQN = slot.property.type?.FullName ?? "System.Object",
                        Index = i++,
                    });
                }
                i = 0;
                foreach (var slot in container.outputSlots)
                {
                    desc.OutputSlots.Add(new SlotBindingDescriptor
                    {
                        Name = slot.name,
                        SlotTypeFQN = slot.property.type?.FullName ?? "System.Object",
                        Index = i++,
                    });
                }
            }

            return desc;
        }
    }
}
