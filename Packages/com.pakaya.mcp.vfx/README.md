# Pakaya MCP VFX Tools

`com.pakaya.mcp.vfx` is a URP-first VFX Graph extension package for MCP for Unity.

## Status

- Canonical implementation lives in `Assets/MCPForUnity/Editor/Tools/Vfx` for direct compatibility with Unity MCP source layout.
- This package is optional scaffolding and metadata only in this repository variant.

## Tooling model

- Custom tools are registered via `[McpForUnityTool(...)]`.
- Primary graph entrypoint: `manage_vfx_graph`.
- Legacy mixed entrypoint: `manage_vfx` with `vfx_*` actions.

## Pipeline support

- Guaranteed support: URP.
- Non-URP pipelines return structured `unsupported_pipeline` errors for graph operations.

## Installation

1. Open Unity Package Manager.
2. Add package from disk (embedded package) or Git URL to this package path.
3. Ensure `com.unity.visualeffectgraph` and `com.unity.render-pipelines.universal` are present.
4. Start MCP for Unity and reconnect your client so custom tools refresh.
