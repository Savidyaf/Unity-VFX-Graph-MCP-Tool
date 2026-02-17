# VFX Tool Reference

## Tool: `manage_vfx_graph`

### Response contract

All responses include:

- `success` (bool)
- `error_code` (string or null)
- `message` (string)
- `data` (object or null)
- `details` (object or null)
- `tool_version` (string)

### Error codes

- `missing_action`
- `unknown_action`
- `validation_error`
- `unsupported_pipeline`
- `asset_not_found`
- `not_found`
- `reflection_error`
- `internal_exception`
- `unknown_error`

### Canonical actions

- `get_graph_info`
- `add_node`
- `connect_nodes`
- `set_node_property`
- `set_node_setting`
- `get_node_settings`
- `list_node_types`
- `link_contexts`
- `add_block`
- `remove_block`
- `list_block_types`
- `add_property`
- `list_properties`
- `remove_property`
- `set_property_value`
- `set_hlsl_code`
- `create_buffer_helper`
- `link_gpu_event`
- `set_space`

### Aliases

- `graph_add_node` -> `add_node`

### Pipeline contract

`manage_vfx_graph` is URP-first. Non-URP projects receive:

- `success: false`
- `error_code: unsupported_pipeline`
- pipeline details and remediation guidance in `details`
