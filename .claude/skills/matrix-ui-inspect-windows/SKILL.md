---
name: matrix-ui-inspect-windows
description: "List every EditorWindow currently alive in the Unity Editor — type, title, rect, focus, dock state, and whether it has a UI Toolkit element tree. Entry point for the ui-inspect tool family: pick a window here, then drill in with 'ui-inspect-tree'."
---

# UI / Inspect Windows

Lists all open EditorWindows so the AI can pick one to inspect.

## Output

For each window: full type name (use as `windowName` in the other ui-inspect tools), tab title, desktop rect, focus/dock state, and `RootChildCount` — when it is 0 the window is pure IMGUI and its content cannot be traversed by the UI inspector.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{}
```

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

This tool takes no input parameters.

### Input JSON Schema

```json
{
  "type": "object",
  "additionalProperties": false
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/Feeder.MCP.Data.UiWindowsResult"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.UiWindowInfo-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.UiWindowInfo"
      }
    },
    "Feeder.MCP.Data.UiWindowInfo": {
      "type": "object",
      "properties": {
        "TypeName": {
          "type": "string",
          "description": "Full type name of the EditorWindow class. Use it (or the short class name) as 'windowName' in the other ui-inspect tools."
        },
        "Title": {
          "type": "string",
          "description": "Window title (tab text)."
        },
        "X": {
          "type": "number",
          "description": "Window position X on the desktop."
        },
        "Y": {
          "type": "number",
          "description": "Window position Y on the desktop."
        },
        "Width": {
          "type": "number",
          "description": "Window width in pixels."
        },
        "Height": {
          "type": "number",
          "description": "Window height in pixels."
        },
        "Focused": {
          "type": "boolean",
          "description": "True when this window currently has keyboard focus."
        },
        "Docked": {
          "type": "boolean",
          "description": "True when the window is docked in the editor layout."
        },
        "RootChildCount": {
          "type": "integer",
          "description": "Number of direct children under rootVisualElement. 0 usually means a pure-IMGUI window whose content the UI inspector cannot traverse."
        }
      },
      "required": [
        "X",
        "Y",
        "Width",
        "Height",
        "Focused",
        "Docked",
        "RootChildCount"
      ]
    },
    "Feeder.MCP.Data.UiWindowsResult": {
      "type": "object",
      "properties": {
        "Windows": {
          "$ref": "#/$defs/Feeder.MCP.Data.UiWindowInfo-1",
          "description": "All EditorWindow instances currently alive in the editor."
        },
        "Count": {
          "type": "integer",
          "description": "Total number of windows returned."
        }
      },
      "required": [
        "Count"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

