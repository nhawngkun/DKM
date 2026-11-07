---
name: matrix-assets-find-built-in
description: Search the built-in assets of the Unity Editor (located at Resources/unity_builtin_extra). Filters by name and/or type; built-in assets have no GUID so GUID-based lookups are not supported.
---

# Assets / Find (Built-in)

Search the built-in assets of the Unity Editor located in the built-in resources: Resources/unity_builtin_extra. Doesn't support GUIDs since built-in assets do not have them.

## Inputs

- `name` (optional) — case-insensitive name fragment. Underscores, hyphens, spaces, and periods delimit search words so partial-word matching works.
- `type` (optional) — restrict results to assets assignable to this type (e.g. `UnityEngine.Texture2D`).
- `maxResults` — cap on returned list size (default 10).

## Ranking

Results are sorted by descending match quality: exact match → substring match → all-words match → partial-words match. Within a rank, results are sorted alphabetically by filename for stable ordering.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "name": "value",
  "type": {},
  "maxResults": 0
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `string` | No | The name of the asset to filter by. |
| `type` | `any` | No | The type of the asset to filter by. |
| `maxResults` | `integer` | No | Maximum number of assets to return. If the number of found assets exceeds this limit, the result will be truncated. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "name": {
      "type": "string"
    },
    "type": {
      "$ref": "#/$defs/System.Type"
    },
    "maxResults": {
      "type": "integer"
    }
  },
  "$defs": {
    "System.Type": {
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
      "$ref": "#/$defs/System.Collections.Generic.List(Feeder.MCP.Data.AssetObjectRef)"
    }
  },
  "$defs": {
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
    },
    "System.Type": {
      "type": "string"
    },
    "System.Collections.Generic.List(Feeder.MCP.Data.AssetObjectRef)": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.AssetObjectRef",
        "description": "Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders."
      }
    }
  },
  "required": [
    "result"
  ]
}
```

