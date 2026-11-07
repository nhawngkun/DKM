---
name: matrix-gameobject-component-list-all
description: List the fully-qualified C# type names of every concrete `UnityEngine.Component` subclass available in the project. Paginated (default 5/page, max 500). Use this to find a valid `componentName` for 'rql-mutate'.
---

# GameObject / Component / List All

List C# class names extended from UnityEngine.Component. Use this to find component type names for 'rql-mutate' tool. Results are paginated to avoid overwhelming responses.

## Inputs

- `search` (optional) — case-insensitive substring filter on type names.
- `page` (default 0, 0-based) — page index.
- `pageSize` (default 5, range 1..500) — items per page.

## Behavior

Enumerates `AllComponentTypes` (every non-abstract subclass of `UnityEngine.Component`), filters by `search` if supplied, then returns a `ComponentListResult` containing the requested page plus `TotalCount` / `TotalPages` so the caller can iterate.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "search": "value",
  "page": 0,
  "pageSize": 0
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `search` | `string` | No | Substring for searching components. Could be empty. |
| `page` | `integer` | No | Page number (0-based). Default is 0. |
| `pageSize` | `integer` | No | Number of items per page. Default is 5. Max is 500. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "search": {
      "type": "string"
    },
    "page": {
      "type": "integer"
    },
    "pageSize": {
      "type": "integer"
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
      "$ref": "#/$defs/Feeder.MCP.Data.ComponentListResult"
    }
  },
  "$defs": {
    "System.String-1": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "Feeder.MCP.Data.ComponentListResult": {
      "type": "object",
      "properties": {
        "Items": {
          "$ref": "#/$defs/System.String-1",
          "description": "Array of component type names for the current page."
        },
        "Page": {
          "type": "integer",
          "description": "Current page number (0-based)."
        },
        "PageSize": {
          "type": "integer",
          "description": "Number of items per page."
        },
        "TotalCount": {
          "type": "integer",
          "description": "Total number of matching components."
        },
        "TotalPages": {
          "type": "integer",
          "description": "Total number of pages available."
        }
      },
      "required": [
        "Page",
        "PageSize",
        "TotalCount",
        "TotalPages"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

