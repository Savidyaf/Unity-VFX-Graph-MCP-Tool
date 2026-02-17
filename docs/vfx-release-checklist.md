# VFX MCP Release Checklist

## Compatibility

- [ ] Unity version validated
- [ ] `com.unity.visualeffectgraph` validated
- [ ] URP compatibility verified

## Quality gates

- [ ] EditMode tests pass
- [ ] Manual graph smoke test pass
- [ ] No debug placeholders in responses

## MCP behavior

- [ ] Tool discovered as custom tool
- [ ] Response schema contains `tool_version`
- [ ] Error codes are stable and documented

## Packaging

- [ ] `Packages/com.pakaya.mcp.vfx/package.json` updated
- [ ] `CHANGELOG.md` updated
- [ ] Installation notes updated
