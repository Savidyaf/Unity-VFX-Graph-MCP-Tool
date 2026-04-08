// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs
// Phase 2 task 7 + 8: walker over VFXLibrary descriptors.
// Task 8 extends to blocks/contexts/parameters/subgraphs and applies the
// typeof()-based subgraph split per erratum P-B5.
using System.Linq;
using UnityEditor.VFX;

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
        public CatalogIR Walk()
        {
            var ir = new CatalogIR { VfxGraphPackageVersion = "17.4.0" };

            foreach (var d in VFXLibrary.GetOperators())
                ir.Operators.Add(MakeDescriptor(d.modelType, d.category));

            foreach (var d in VFXLibrary.GetBlocks())
                ir.Blocks.Add(MakeDescriptor(d.modelType, d.category));

            foreach (var d in VFXLibrary.GetContexts())
                ir.Contexts.Add(MakeDescriptor(d.modelType, d.category));

            foreach (var d in VFXLibrary.GetParameters())
                ir.Parameters.Add(MakeDescriptor(d.modelType, d.category));

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

            return ir;
        }

        private static NodeDescriptor MakeDescriptor(System.Type modelType, string category)
        {
            return new NodeDescriptor
            {
                ModelType = modelType,          // erratum P-B5: store live Type for typeof()-based split
                TypeFQN = modelType.FullName,
                ShortName = modelType.Name,
                Category = category ?? "",
                VariantKey = "",
            };
        }
    }
}
