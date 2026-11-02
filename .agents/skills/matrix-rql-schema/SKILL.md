---
name: matrix-rql-schema
description: Syntax card of the query language plus the component types present in the open scenes; with `typeName`, the members (type, read or write) and methods of that type. Call it before writing a 'rql-query' or 'rql-mutate' query you are unsure of.
---

# Query / Schema

# Query / Schema

- No arguments: the language reference, every function, and the component types in the open scenes with their counts.
- `typeName` (`Light`, `BoxCollider`, `UnityEngine.UI.Image`, a script class, an enum): its members as `member | type | access` (`get set` can be written by `set`) and its public methods; `*` marks methods `rql-query` may call.
- `filter`: a regex on member and method names. `includeInherited`: also list members from Component/MonoBehaviour/Object.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "typeName": "value",
  "filter": "value",
  "includeMethods": false,
  "includeInherited": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `typeName` | `string` | No | Type to describe, e.g. Light, Rigidbody, Material, UnityEngine.UI.Image, or an enum. Empty: the language reference. |
| `filter` | `string` | No | Regex on member and method names, e.g. 'color|intensity'. |
| `includeMethods` | `boolean` | No | List public methods too (default true). |
| `includeInherited` | `boolean` | No | Also list members inherited from Component, Behaviour, MonoBehaviour and Object. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "typeName": {
      "type": "string"
    },
    "filter": {
      "type": "string"
    },
    "includeMethods": {
      "type": "boolean"
    },
    "includeInherited": {
      "type": "boolean"
    }
  }
}
```

## Output

This tool does not return structured output.

