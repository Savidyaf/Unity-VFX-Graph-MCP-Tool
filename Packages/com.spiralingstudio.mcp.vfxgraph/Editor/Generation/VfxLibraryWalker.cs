// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation/VfxLibraryWalker.cs
// Phase 2 task 7: minimal walker. Populates ir.Operators only.
// Tasks 8 extends this to blocks/contexts/parameters/subgraphs.
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
            var ir = new CatalogIR
            {
                VfxGraphPackageVersion = "17.4.0" // wired from package.json in task 12
            };

            foreach (var descriptor in VFXLibrary.GetOperators())
            {
                var type = descriptor.modelType;
                ir.Operators.Add(new NodeDescriptor
                {
                    ModelType = type,                  // erratum P-B5: store live Type for typeof()-based split (wired in task 8)
                    TypeFQN = type.FullName,
                    ShortName = type.Name,
                    Category = descriptor.category ?? "",
                    VariantKey = "",
                });
            }

            return ir;
        }
    }
}
