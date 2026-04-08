// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxStructuralFingerprint.cs
//
// Lane 3B task 3B-1: structural fingerprint helper used by VfxIdentity to
// derive a stable token for a VFXModel based on its location and shape in the
// graph (parent fingerprint, sibling-index-among-same-type, slot shape, and
// setting shape). NO values are hashed — only the structure — so the same
// node "shape" mints the same token across reload.
//
// Hash: FNV-1a 64-bit (no external dependency, deterministic, fast).
// Token format: prefix + 5 hex chars (top 20 bits of fingerprint).

using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal static class VfxStructuralFingerprint
    {
        // FNV-1a 64-bit constants. http://isthe.com/chongo/tech/comp/fnv/
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Fnv1a64(string input)
        {
            ulong hash = OffsetBasis;
            if (input == null) return hash;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= Prime;
            }
            return hash;
        }

        /// <summary>
        /// Returns prefix + the top 20 bits of <paramref name="fingerprint"/> rendered
        /// as 5 lowercase hex chars (zero-padded). 20 bits → 1,048,576 distinct values
        /// per prefix family, which the spec considers low enough collision risk per
        /// asset.
        /// </summary>
        public static string ToShortToken(string prefix, ulong fingerprint)
        {
            // Top 20 bits → 5 hex chars.
            ulong top = (fingerprint >> (64 - 20)) & 0xFFFFFUL;
            return $"{prefix}{top:x5}";
        }

        /// <summary>
        /// Compute the structural fingerprint for <paramref name="model"/> in the
        /// context of <paramref name="graphGuid"/>. Recursively walks the parent
        /// chain (so a child's fingerprint changes if any ancestor's structure
        /// changes), then mixes in:
        ///   - sibling index among same-typed siblings (NOT raw position in m_Children),
        ///   - slot shape: input slot names + slot value type FQNs (no values),
        ///   - setting shape: setting names + value type FQNs (no values).
        ///
        /// VFX Graph 17.4.0 references:
        ///   VFXModel.GetParent          → Editor/Models/VFXModel.cs:184
        ///   VFXModel.children           → Editor/Models/VFXModel.cs:215
        ///   VFXModel.GetSettings        → Editor/Models/VFXModel.cs:431
        ///   IVFXSlotContainer.inputSlots→ Editor/Models/VFXSlotContainerModel.cs:15
        ///   VFXSlot.name (= property.name) → Editor/Models/Slots/VFXSlot.cs:21
        ///   VFXSlot.property.type       → Editor/Types/VFXProperty.cs:24
        /// </summary>
        public static ulong Compute(string graphGuid, VFXModel model)
        {
            if (model == null) return 0UL;

            // Parent fingerprint (recursive walk upward). Per spec:
            // "parentFingerprint — recursively computed for the parent; top-level
            // contexts use ''" — i.e., the VFXGraph is the recursion stop condition,
            // not a participating parent. Operators/contexts/parameters that hang
            // directly off the graph have parentKey="" (top-level).
            string parentKey = "";
            var parent = model.GetParent();
            if (parent != null && !(parent is VFXGraph))
                parentKey = Compute(graphGuid, parent).ToString("x16");

            // Sibling index among same-typed siblings under the same parent.
            // We deliberately ignore raw m_Children index — only the count of
            // siblings of the same C# type before us. This makes fingerprints
            // stable when an unrelated sibling is removed.
            int siblingIndex = 0;
            if (parent != null)
            {
                int counter = 0;
                var modelType = model.GetType();
                // For top-level nodes, the "parent" used for sibling counting is
                // still the graph (so the same-type-rank is correct), even though
                // the parent fingerprint above is empty. This is intentional and
                // matches spec section "Identity model" example.
                foreach (var sib in parent.children)
                {
                    if (ReferenceEquals(sib, model))
                    {
                        siblingIndex = counter;
                        break;
                    }
                    if (sib.GetType() == modelType)
                        counter++;
                }
            }

            // Slot shape: ordered list of input slot (name : property type FQN).
            // We hash names + types (the schema), never values. We restrict to
            // input slots because output slots are derived from inputs in most
            // operators and would double-count.
            string slotShape = "";
            if (model is IVFXSlotContainer container)
            {
                var parts = new List<string>();
                foreach (var slot in container.inputSlots)
                {
                    string slotTypeFqn = slot?.property.type?.FullName ?? "null";
                    parts.Add((slot?.name ?? "") + ":" + slotTypeFqn);
                }
                slotShape = string.Join(",", parts);
            }

            // Setting shape: ordered list of (setting name : value type FQN).
            // The walker uses the field name (e.g. "m_Type") not the stripped
            // short name; that's per erratum P-H2.
            var settingParts = new List<string>();
            foreach (var setting in model.GetSettings(listHidden: false))
            {
                string typeName;
                try
                {
                    var raw = setting.value;
                    typeName = raw?.GetType().FullName ?? "null";
                }
                catch
                {
                    typeName = "null";
                }
                settingParts.Add(setting.name + ":" + typeName);
            }
            string settingShape = string.Join(",", settingParts);

            string key = $"{graphGuid}|{parentKey}|{model.GetType().FullName}|{siblingIndex}|{slotShape}|{settingShape}";
            return Fnv1a64(key);
        }
    }
}
