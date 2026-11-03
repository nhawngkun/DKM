---
name: matrix-gameobject-duplicate
description: Duplicate a batch of GameObjects in the currently opened Prefab or active Scene. Marks each affected scene as dirty after duplication. Use 'rql-query' to locate the source GameObjects first.
---

# GameObject / Duplicate

Duplicate GameObjects in opened Prefab or in a Scene. Use 'rql-query' tool to find the target GameObjects first.

## Inputs

- `gameObjectRefs` — `GameObjectRefList` of source GameObjects.

## Behavior

Resolves every input ref on the main thread (throwing on any unresolved entry to keep the batch atomic). Sets `Selection.entityIds`/`instanceIDs` to the sources and invokes `Unsupported.DuplicateGameObjectsUsingPasteboard()` (Unity's canonical duplicate routine). Marks every distinct affected scene dirty so the duplicated objects are saved with the scene. Returns refs to the sources (the duplicates are reachable via the post-call `Selection`).

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "gameObjectRefs": {
    "instanceID": 0
  }
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRefs` | `any` | Yes | Array of GameObjects in opened Prefab or in the active Scene. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRefs": {
      "$ref": "#/$defs/Feeder.MCP.Data.GameObjectRefList"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If it is '0' and 'path', 'name', 'assetPath' and 'assetGuid' is not provided, empty or null, then it will be used as 'null'. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample 'character/hand/finger/particle'. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
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
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "System.Type": {
      "type": "string"
    },
    "Feeder.MCP.Data.GameObjectRefList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.GameObjectRef",
        "description": "Find GameObject in opened Prefab or in the active Scene."
      },
      "description": "Array of GameObjects in opened Prefab or in the active Scene."
    }
  },
  "required": [
    "gameObjectRefs"
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
      "$ref": "#/$defs/System.Collections.Generic.List(Feeder.MCP.Data.GameObjectRef)"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If it is '0' and 'path', 'name', 'assetPath' and 'assetGuid' is not provided, empty or null, then it will be used as 'null'. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample 'character/hand/finger/particle'. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
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
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "System.Type": {
      "type": "string"
    },
    "System.Collections.Generic.List(Feeder.MCP.Data.GameObjectRef)": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.GameObjectRef",
        "description": "Find GameObject in opened Prefab or in the active Scene."
      }
    }
  },
  "required": [
    "result"
  ]
}
```

