// VFX MCP addon soft-fork patch — grants the addon's editor assembly access
// to UnityEditor.VFX internal types (VFXLibrary, VFXViewController, VFXModel,
// VFXContext, VFXBlock, VFXOperator, VFXSlot, etc.). Without this line, the
// generator and kernel cannot compile. See:
//   docs/superpowers/specs/2026-04-07-vfx-graph-mcp-redesign.md
//   docs/superpowers/plans/2026-04-07-vfx-graph-mcp-rebuild.md
// (section: Embedded VFX Graph Package Ownership)

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor")]
[assembly: InternalsVisibleTo("com.spiralingstudio.mcp.vfxgraph.Editor.Tests")]
