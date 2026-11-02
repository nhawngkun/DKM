---
name: matrix-unity-tool-list
description: List all Feeder MCP tools registered in the connected Unity Editor instance, including disabled ones (see `enabled`); only a core set is on by default, so use this to find a tool, then enable it with tool-set-enabled-state. Optional regex filter matches against tool name, description, and argument names/descriptions. Use the `includeDescription` / `includeInputs` toggles to control the response size.
---

# Tool / List

List all Feeder MCP tools registered in the connected Unity Editor instance. Optionally filter by regex across tool names, descriptions, and arguments.

## Inputs

- `regexSearch` (optional) — case-insensitive regex with a 200ms execution-timeout guard. Invalid regex throws an `ArgumentException`. Matches name → description → input names → input descriptions.
- `includeDescription` (default `false`) — populate each tool's `Description` field.
- `includeInputs` (default `None`) — one of `None`, `Inputs` (names only), `InputsWithDescription` (names + descriptions).

## Behavior

Iterates `MatrixPluginEditor.Instance.Tools.GetAllTools()`, evaluates the filter (if any), and projects each surviving tool into a `ToolInfoData` honoring the verbosity toggles.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "regexSearch": "value",
  "includeDescription": {},
  "includeInputs": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `regexSearch` | `string` | No | Regex pattern to filter tools. Matches against tool name, description, and argument names and descriptions. |
| `includeDescription` | `any` | No | Include tool descriptions in the result. Default: false |
| `includeInputs` | `any` | No | Include input arguments in the result. Default: None |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "regexSearch": {
      "type": "string"
    },
    "includeDescription": {
      "$ref": "#/$defs/System.Boolean"
    },
    "includeInputs": {
      "$ref": "#/$defs/Feeder.MCP.Editor.API.Tool_Tool-InputRequest"
    }
  },
  "$defs": {
    "System.Boolean": {
      "type": "boolean"
    },
    "Feeder.MCP.Editor.API.Tool_Tool-InputRequest": {
      "type": "string",
      "enum": [
        "None",
        "Inputs",
        "InputsWithDescription"
      ],
      "description": "Specifies what to include for tool input arguments."
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
      "$ref": "#/$defs/Feeder.MCP.Data.ToolInfoData-1"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.ToolInfoData": {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Tool name."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the tool is enabled. A disabled tool is registered but cannot be called until tool-set-enabled-state turns it on."
        },
        "description": {
          "type": "string",
          "description": "Tool description."
        },
        "inputs": {
          "$ref": "#/$defs/Feeder.MCP.Data.ToolInputData-1",
          "description": "Tool input arguments."
        }
      },
      "required": [
        "enabled"
      ],
      "description": "MCP tool information."
    },
    "Feeder.MCP.Data.ToolInputData-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.ToolInputData",
        "description": "MCP tool input argument."
      }
    },
    "Feeder.MCP.Data.ToolInputData": {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Argument name."
        },
        "description": {
          "type": "string",
          "description": "Argument description."
        }
      },
      "description": "MCP tool input argument."
    },
    "Feeder.MCP.Data.ToolInfoData-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.ToolInfoData",
        "description": "MCP tool information."
      }
    }
  },
  "required": [
    "result"
  ]
}
```

