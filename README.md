# VFX MCP WIP

URP-first productionization of custom VFX tools for MCP for Unity.

## What is included

- `manage_vfx` and `manage_vfx_graph` custom tool handlers.
- Reflection-backed VFX Graph editing support.
- Structured tool responses with `error_code` and `tool_version`.
- URP compatibility gating for graph actions.
- EditMode test and CI scaffolding.
- UPM package scaffold at `Packages/com.spiralingstudio.mcp.vfxgraph`.

## Key paths

- Tool source: `Assets/MCPForUnity/Editor/Tools/Vfx`
- Package scaffold: `Packages/com.spiralingstudio.mcp.vfxgraph`
- Tests: `Assets/MCPForUnity/Editor/Tests/Editor`
- CI: `.github/workflows/unity-editmode-tests.yml`

## Upstream references

- [unity-mcp repository](https://github.com/CoplayDev/unity-mcp)
- [Custom tool authoring guide](https://raw.githubusercontent.com/CoplayDev/unity-mcp/beta/docs/reference/CUSTOM_TOOLS.md)
- [Project README](https://raw.githubusercontent.com/CoplayDev/unity-mcp/beta/README.md)
