---
name: matrix-scene-open
description: Open a Unity scene asset in Single or Additive mode. Returns the post-open list of all opened scenes. Use 'rql-query' to locate the scene asset first.
---

# Scene / Open

Open scene from the project asset file. Use 'rql-query' tool to find the scene asset first.

## Inputs

- `sceneRef` — `AssetObjectRef` pointing at a `SceneAsset`. Throws if the asset cannot be resolved or is not a `SceneAsset`.
- `loadSceneMode` (default `Single`):
  - `Single` — closes the currently opened scenes and opens this one.
  - `Additive` — keeps the currently opened scenes and opens this one alongside them.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "sceneRef": {
    "instanceID": 0
  },
  "loadSceneMode": "Single"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sceneRef` | `any` | Yes | Reference to UnityEngine.Object asset instance. It could be Material, ScriptableObject, Prefab, and any other Asset. Anything located in the Assets and Packages folders. |
| `loadSceneMode` | `string` | No | Open scene mode. Single: closes the current scenes and opens a new one. Additive: keeps the current scene and opens additional one. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sceneRef": {
      "$ref": "#/$defs/Feeder.MCP.Data.AssetObjectRef"
    },
    "loadSceneMode": {
      "type": "string",
      "enum": [
        "Single",
        "Additive",
        "AdditiveWithoutLoading"
      ]
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
    "sceneRef"
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
      "$ref": "#/$defs/Feeder.MCP.Data.SceneDataShallow-1"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.SceneDataShallow": {
      "type": "object",
      "properties": {
        "Name": {
          "type": "string"
        },
        "IsLoaded": {
          "type": "boolean"
        },
        "IsDirty": {
          "type": "boolean"
        },
        "IsSubScene": {
          "type": "boolean"
        },
        "IsValidScene": {
          "type": "boolean",
          "description": "Whether this is a valid Scene. A Scene may be invalid if, for example, you tried to open a Scene that does not exist. In this case, the Scene returned from EditorSceneManager.OpenScene would return False for IsValid."
        },
        "RootCount": {
          "type": "integer"
        },
        "path": {
          "type": "string",
          "description": "Path to the Scene within the project. Starts with 'Assets/'"
        },
        "buildIndex": {
          "type": "integer",
          "description": "Build index of the Scene in the Build Settings."
        },
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If this is '0', then it will be used as 'null'."
        }
      },
      "required": [
        "IsLoaded",
        "IsDirty",
        "IsSubScene",
        "IsValidScene",
        "RootCount",
        "buildIndex",
        "instanceID"
      ],
      "description": "Scene reference. Used to find a Scene."
    },
    "Feeder.MCP.Data.SceneDataShallow-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.SceneDataShallow",
        "description": "Scene reference. Used to find a Scene."
      }
    }
  },
  "required": [
    "result"
  ]
}
```

