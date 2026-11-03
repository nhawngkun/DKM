---
name: matrix-assets-material-create
description: Create a new Material asset with default parameters at a given 'Assets/'-rooted path ending in '.mat'. Creates intermediate folders if missing. Use 'assets-shader-list-all' to find a valid `shaderName`.
---

# Assets / Create Material

Create new material asset with default parameters. Creates folders recursively if they do not exist. Provide proper 'shaderName' - use 'assets-shader-list-all' tool to find available shaders.

## Inputs

- `assetPath` — must start with `Assets/` and end with `.mat`.
- `shaderName` — name resolvable via `UnityEngine.Shader.Find`.

## Behavior

Throws if the path is empty, malformed, or the shader cannot be resolved. Creates a default Material from the resolved shader, saves it, refreshes the AssetDatabase, and returns an `AssetObjectRef` pointing at the new asset.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "assetPath": "value",
  "shaderName": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `assetPath` | `string` | Yes | Asset path. Starts with 'Assets/'. Ends with '.mat'. |
| `shaderName` | `string` | Yes | Name of the shader that need to be used to create the material. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "assetPath": {
      "type": "string"
    },
    "shaderName": {
      "type": "string"
    }
  },
  "required": [
    "assetPath",
    "shaderName"
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
      "$ref": "#/$defs/Feeder.MCP.Data.AssetObjectRef",
      "description": "Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders."
    }
  },
  "$defs": {
    "System.Type": {
      "type": "string"
    },
    "Feeder.MCP.Data.AssetObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If this is '0' and 'assetPath' and 'assetGuid' is not provided, empty or null, then it will be used as 'null'."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with 'Assets/'"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders."
    }
  },
  "required": [
    "result"
  ]
}
```

