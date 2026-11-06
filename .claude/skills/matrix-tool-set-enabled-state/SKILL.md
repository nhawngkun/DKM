---
name: matrix-tool-set-enabled-state
description: Enable or disable MCP tools by name in batch. Persists the change via `MatrixPluginEditor.Instance.Save()` only when at least one tool actually flipped. Returns per-input success flags plus optional operation logs.
---

# Tool / Set Enabled State

Enable or disable MCP tools by name. Allows controlling which tools are available for the AI agent.

## Inputs

- `tools` — array of `ToolToggleInput { Name, Enabled }`. Non-empty.
- `includeLogs` (default `false`) — when true, returns per-step operation logs alongside the success map.

## Behavior

Each entry is resolved against the tool manager's exact-name and case-insensitive lookups. Already-correct state short-circuits as success without writing. The plugin's config is saved once at the end iff at least one tool actually changed state.

## After enabling

The server tells the client its tool list changed, and the client re-lists. If a tool you just enabled is still not in your tool list, call it through the server tool `tool-call` with `name` and `arguments`.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "tools": {},
  "includeLogs": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `tools` | `any` | Yes | Array of tools with their desired enabled state. |
| `includeLogs` | `any` | No | Include operation logs in the result. Default: false |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "tools": {
      "$ref": "#/$defs/Feeder.MCP.Data.ToolToggleInput-1"
    },
    "includeLogs": {
      "$ref": "#/$defs/System.Boolean"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.ToolToggleInput": {
      "type": "object",
      "properties": {
        "Name": {
          "type": "string",
          "description": "Name of the MCP tool to enable or disable."
        },
        "Enabled": {
          "type": "boolean",
          "description": "Whether the tool should be enabled (true) or disabled (false)."
        }
      },
      "required": [
        "Enabled"
      ]
    },
    "Feeder.MCP.Data.ToolToggleInput-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.ToolToggleInput"
      }
    },
    "System.Boolean": {
      "type": "boolean"
    }
  },
  "required": [
    "tools"
  ]
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/Feeder.MCP.Data.ToolToggleResult"
    }
  },
  "$defs": {
    "Feeder.Reflect.Model.Logs": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.Reflect.Model.LogEntry"
      }
    },
    "Feeder.Reflect.Model.LogEntry": {
      "type": "object",
      "properties": {
        "Depth": {
          "type": "integer"
        },
        "Message": {
          "type": "string"
        },
        "Type": {
          "type": "string",
          "enum": [
            "Trace",
            "Debug",
            "Info",
            "Success",
            "Warning",
            "Error",
            "Critical"
          ]
        }
      },
      "required": [
        "Depth",
        "Type"
      ]
    },
    "System.Collections.Generic.Dictionary(System.String,System.Boolean)": {
      "type": "object",
      "additionalProperties": {
        "type": "boolean"
      }
    },
    "Feeder.MCP.Data.ToolToggleResult": {
      "type": "object",
      "properties": {
        "Logs": {
          "$ref": "#/$defs/Feeder.Reflect.Model.Logs",
          "description": "Optional operation logs. Only included when 'includeLogs' is true."
        },
        "Success": {
          "$ref": "#/$defs/System.Collections.Generic.Dictionary(System.String,System.Boolean)",
          "description": "Result of each tool operation. Key: original input name as provided by the caller (case preserved as-is). Value: true if the enable/disable operation completed successfully, false if the name was unknown, ambiguous, or empty."
        }
      }
    }
  },
  "required": [
    "result"
  ]
}
```

