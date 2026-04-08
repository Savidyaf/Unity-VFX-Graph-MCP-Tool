// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxSlotTreeBuilder.cs
// Phase 3 task 3A-2: walks compound slot decomposition by descending live
// VFXSlot.children, the same source VFX Graph itself uses to build its slot
// tree (VFXSlot.CreateSub at Models/Slots/VFXSlot.cs:372).
//
// ERRATUM P-B3: do NOT use C# struct field reflection. The walker has
// already instantiated a transient template for every operator/block/
// context (VfxLibraryWalker.WalkWithTemplates), so we descend its live
// IVFXSlotContainer.inputSlots / outputSlots tree, capturing the
// authoritative VFXProperty name + type that VFX Graph itself emitted.
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;
using UnityEngine;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// Recursively records every distinct slot type reachable from the
    /// supplied template models into the IR's <see cref="CatalogIR.SlotTypes"/>
    /// list. Compound types (Vector3, Color, Sphere, Transform, …) are
    /// captured with their decomposed children; leaf types (float, Texture2D,
    /// GraphicsBuffer, primitive numerics) are recorded as leaves.
    /// </summary>
    internal sealed class VfxSlotTreeBuilder
    {
        // Known leaf types — anything else with at least one VFXSlot.children
        // entry is treated as compound. Texture/Mesh/Buffer references are
        // always leaves.
        private static readonly HashSet<System.Type> LeafTypes = new HashSet<System.Type>
        {
            typeof(bool), typeof(int), typeof(uint), typeof(float), typeof(double),
            typeof(string),
            typeof(UnityEngine.Texture2D), typeof(UnityEngine.Texture2DArray),
            typeof(UnityEngine.Texture3D), typeof(UnityEngine.Cubemap),
            typeof(UnityEngine.CubemapArray), typeof(UnityEngine.Mesh),
            typeof(UnityEngine.GraphicsBuffer), typeof(UnityEngine.AnimationCurve),
            typeof(UnityEngine.Gradient),
            typeof(UnityEngine.Matrix4x4),
        };

        /// <summary>
        /// Walks slot trees of every supplied template (operator/block/
        /// context). Each unique slot type is added once.
        /// </summary>
        public void BuildFromTemplates(CatalogIR ir, IEnumerable<VFXModel> templates)
        {
            if (ir == null) return;
            if (templates == null) return;

            var seen = new HashSet<string>();
            foreach (var template in templates)
            {
                if (template is IVFXSlotContainer container)
                {
                    foreach (var slot in container.inputSlots) VisitSlot(ir, slot, seen);
                    foreach (var slot in container.outputSlots) VisitSlot(ir, slot, seen);
                }
            }
        }

        private void VisitSlot(CatalogIR ir, VFXSlot slot, HashSet<string> seen)
        {
            if (slot == null) return;
            var slotType = slot.property.type;
            if (slotType == null) return;
            var typeFqn = slotType.FullName;
            if (typeFqn == null) return;
            if (!seen.Add(typeFqn)) return;

            var desc = new SlotTypeDescriptor { TypeFQN = typeFqn };

            // Leaf detection — primitives, enums, and our explicit allowlist
            // never count as compound regardless of children count.
            if (slotType.IsPrimitive || slotType.IsEnum || LeafTypes.Contains(slotType))
            {
                desc.IsCompound = false;
                ir.SlotTypes.Add(desc);
                return;
            }

            var children = slot.children?.ToList() ?? new List<VFXSlot>();
            if (children.Count == 0)
            {
                // Compound shape with no expansion (e.g. an opaque struct);
                // record as a leaf so the emitter knows there is nothing to
                // decompose.
                desc.IsCompound = false;
                ir.SlotTypes.Add(desc);
                return;
            }

            desc.IsCompound = true;
            foreach (var child in children)
            {
                var childTypeFqn = child.property.type?.FullName ?? "System.Object";
                desc.Children.Add(new SlotChildDescriptor
                {
                    Name = child.name,
                    ChildTypeFQN = childTypeFqn,
                });
                VisitSlot(ir, child, seen);
            }
            ir.SlotTypes.Add(desc);
        }
    }
}
