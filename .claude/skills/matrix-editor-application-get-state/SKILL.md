---
name: matrix-editor-application-get-state
description: Return the current state of `UnityEditor.EditorApplication` — playmode, paused state, compilation state, and related flags.
---

# Editor / Application / Get State

Returns available information about 'UnityEditor.EditorApplication'. Use it to get information about the current state of the Unity Editor application. Such as: playmode, paused state, compilation state, etc.

## Behavior

Snapshots Editor state via `EditorStatsData.FromEditor()` on the main thread and returns the result.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "nothing": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/Feeder.MCP.Data.EditorStatsData",
      "description": "Available information about 'UnityEditor.EditorApplication'."
    }
  },
  "$defs": {
    "Feeder.MCP.Data.EditorStatsData": {
      "type": "object",
      "properties": {
        "IsPlaying": {
          "type": "boolean",
          "description": "Whether the Editor is in Play mode."
        },
        "IsPaused": {
          "type": "boolean",
          "description": "Whether the Editor is paused."
        },
        "IsCompiling": {
          "type": "boolean",
          "description": "Is editor currently compiling scripts? (Read Only)"
        },
        "IsPlayingOrWillChangePlaymode": {
          "type": "boolean",
          "description": "Editor application state which is true only when the Editor is currently in or about to enter Play mode. (Read Only)"
        },
        "IsUpdating": {
          "type": "boolean",
          "description": "True if the Editor is currently refreshing the AssetDatabase. (Read Only)"
        },
        "ApplicationContentsPath": {
          "type": "string",
          "description": "Path to the Unity editor contents folder. (Read Only)"
        },
        "ApplicationPath": {
          "type": "string",
          "description": "Gets the path to the Unity Editor application. (Read Only)"
        },
        "TimeSinceStartup": {
          "type": "number",
          "description": "The time since the editor was started. (Read Only)"
        }
      },
      "required": [
        "IsPlaying",
        "IsPaused",
        "IsCompiling",
        "IsPlayingOrWillChangePlaymode",
        "IsUpdating",
        "TimeSinceStartup"
      ],
      "description": "Available information about 'UnityEditor.EditorApplication'."
    }
  },
  "required": [
    "result"
  ]
}
```

