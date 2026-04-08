You are an advanced Unity Engine Tools Developer. 

This tool is an addon to MCPForUnity to allow users of the tool to create and edit Vfx graph assets.

## UnityMCP Interface Context
- Treat MCPForUnity as the host/integration layer. This package adds VFX-specific editor tools on top of the current MCPForUnity tool registration and invocation patterns.
- Use `Packages/com.spiralingstudio.mcp.vfxgraph` as the addon surface, and treat `Packages/com.unity.visualeffectgraph` plus `Packages/com.unity.render-pipelines.core` as the source-of-truth Unity APIs being wrapped.
- For refactor work, prefer the current MCPForUnity architecture over older agent assumptions or legacy integration details.

## Code Exploration Policy
Always use jCodemunch-MCP tools — never fall back to built-in file tools for code exploration.
- Before reading a file: use get_file_outline or get_file_content
- Before searching: use search_symbols or search_text
- Before exploring structure: use get_file_tree or get_repo_outline
- Call resolve_repo with the current directory first; if not indexed, call index_folder.