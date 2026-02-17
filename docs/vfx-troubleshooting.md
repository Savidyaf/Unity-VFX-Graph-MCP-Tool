# VFX MCP Troubleshooting

## Custom tool does not appear in MCP client

1. Confirm tool class is under an `Editor/` folder.
2. Confirm class has `[McpForUnityTool(..., AutoRegister = true)]`.
3. Reconnect MCP client so tool discovery refreshes.
4. If still missing, restart Unity and reconnect.

## `unsupported_pipeline` error

The graph tool currently guarantees URP support.

1. Open `Project Settings > Graphics`.
2. Assign a `UniversalRenderPipelineAsset`.
3. Check quality levels also use URP assets.

## Reflection-related graph failures

If errors mention `reflection_error`:

1. Verify VFX Graph package is installed.
2. Verify Unity + package versions are in expected range.
3. Retry with a minimal graph to isolate node type issues.

## Asset path validation errors

Supported graph asset paths must be under:

- `Assets/...`
- `Packages/...`

Paths with traversal patterns like `../` are rejected.
