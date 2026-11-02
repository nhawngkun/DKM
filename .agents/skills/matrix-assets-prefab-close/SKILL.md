---
name: matrix-assets-prefab-close
description: Close the currently opened prefab edit stage. Optionally saves changes back to the prefab asset before closing. Pair with 'assets-prefab-open' to enter the edit mode first.
---

# Assets / Prefab / Close

Close currently opened prefab. Use it when you are in prefab editing mode in Unity Editor. Use 'assets-prefab-open' tool to open a prefab first.

## Inputs

- `save` (default `true`) — when `true`, calls `PrefabUtility.SaveAsPrefabAsset` before exiting the stage; when `false`, the save is skipped. The prefab stage's dirtiness is always cleared at the end, so any unsaved changes are discarded when `save` is `false`.

## Behavior

Throws when no prefab stage is currently open. Returns an `AssetObjectRef` for the closed prefab asset.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "save": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `save` | `boolean` | No | True to save prefab. False to discard changes. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "save": {
      "type": "boolean"
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

