// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/CatalogIR.cs
// Phase 2 task 6: in-memory IR consumed by VfxLibraryWalker and the emitters.
// See docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md (task 6 + erratum P-B5).
using System;
using System.Collections.Generic;

namespace SpiralingStudio.VfxMcp.Generation
{
    /// <summary>
    /// In-memory model of the VFX Graph catalog, built by VfxLibraryWalker
    /// and consumed by the emitters. Pure data; no Unity dependencies inside
    /// the types themselves apart from the System.Type pointer carried on
    /// NodeDescriptor (added per erratum P-B5 so the subgraph split can use
    /// typeof() comparison instead of fragile string-FQN matching).
    /// </summary>
    internal sealed class CatalogIR
    {
        public string VfxGraphPackageVersion { get; set; }
        public List<NodeDescriptor> Operators { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> Blocks { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> Contexts { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> Parameters { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> SubgraphOperators { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> SubgraphBlocks { get; } = new List<NodeDescriptor>();
        public List<NodeDescriptor> SubgraphContexts { get; } = new List<NodeDescriptor>();
        public List<SlotTypeDescriptor> SlotTypes { get; } = new List<SlotTypeDescriptor>();
        public List<AttributeDescriptor> Attributes { get; } = new List<AttributeDescriptor>();
    }

    internal sealed class NodeDescriptor
    {
        // ERRATUM P-B5: carry the live Type so subgraph split + walker use typeof() instead of strings.
        public System.Type ModelType { get; set; }
        public string TypeFQN { get; set; }         // "UnityEditor.VFX.Operator.Lerp"
        public string ShortName { get; set; }        // "Lerp"
        public string Category { get; set; }         // "Math/Basic"
        public string VariantKey { get; set; }       // for variadic overloads; "" if none
        public List<SettingDescriptor> Settings { get; } = new List<SettingDescriptor>();
        public List<SlotBindingDescriptor> InputSlots { get; } = new List<SlotBindingDescriptor>();
        public List<SlotBindingDescriptor> OutputSlots { get; } = new List<SlotBindingDescriptor>();
        public bool IsAbstract { get; set; }         // always false after walker filters
        public bool IsDeprecated { get; set; }       // always false after walker filters
    }

    internal sealed class SettingDescriptor
    {
        public string Name { get; set; }
        public string TypeFQN { get; set; }
        public string DefaultLiteral { get; set; }   // emitted inline; null if no default
        public bool IsHidden { get; set; }           // VFXSettingAttribute.VisibleFlags
    }

    internal sealed class SlotBindingDescriptor
    {
        public string Name { get; set; }
        public string SlotTypeFQN { get; set; }      // references an entry in SlotTypes
        public int Index { get; set; }
    }

    internal sealed class SlotTypeDescriptor
    {
        public string TypeFQN { get; set; }          // leaf or compound
        public bool IsCompound { get; set; }
        public List<SlotChildDescriptor> Children { get; } = new List<SlotChildDescriptor>();
    }

    internal sealed class SlotChildDescriptor
    {
        public string Name { get; set; }             // ".x", ".y", ".z"
        public string ChildTypeFQN { get; set; }
    }

    internal sealed class AttributeDescriptor
    {
        public string Name { get; set; }
        public string ValueTypeFQN { get; set; }
        public bool IsVariadic { get; set; }
    }
}
