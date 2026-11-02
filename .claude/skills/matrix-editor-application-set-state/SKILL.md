---
name: matrix-editor-application-set-state
description: Start / stop / pause the Unity Editor 'playmode'. Use 'editor-application-get-state' to inspect the current state first. Throws if the project currently has compilation errors.
---

# Editor / Application / Set State

Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use 'editor-application-get-state' tool to get the current state first.

## Inputs

- `isPlaying` (default `false`) — sets `EditorApplication.isPlaying`.
- `isPaused` (default `false`) — sets `EditorApplication.isPaused`.

## Behavior

Refuses any state change while `EditorUtility.scriptCompilationFailed` is true — instead throws with the compilation error details so the caller can fix them first. Entering or leaving play mode answers only once the editor has reached the new state, domain reload included, so the next call can rely on it. Returns the `EditorStatsData` snapshot taken in the new state.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "isPlaying": false,
  "isPaused": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `isPlaying` | `boolean` | No | If true, the 'playmode' will be started. If false, the 'playmode' will be stopped. |
| `isPaused` | `boolean` | No | If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "isPlaying": {
      "type": "boolean"
    },
    "isPaused": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return structured output.

