# Unity MPC Tool to support VFX Graph changes in URP

Warning : This is a Work In Progress tool with basic functionality tested. All the code here is written by AI tools in a very short time. It will be update as time goes on.
Update : I decided to scrap this approach to edit the YMAL file directly. This tool will be used to train the AI to build the YMAL editor


## What is included

- `manage_vfx` and `manage_vfx_graph` custom tool handlers.
- Reflection-backed VFX Graph editing support.
- Structured tool responses with `error_code` and `tool_version`.
- URP compatibility gating for graph actions.
- EditMode test and CI scaffolding.
- UPM package scaffold at `Packages/com.spiralingstudio.mcp.vfxgraph`.

## Key paths

- Tool source: `Assets/MCPForUnity/Editor/Tools/Vfx`
- Package Path: `Packages/com.spiralingstudio.mcp.vfxgraph`
- Tests: `Assets/MCPForUnity/Editor/Tests/Editor`

## Upstream references

- [unity-mcp repository](https://github.com/CoplayDev/unity-mcp)
- [Custom tool authoring guide](https://raw.githubusercontent.com/CoplayDev/unity-mcp/beta/docs/reference/CUSTOM_TOOLS.md)
