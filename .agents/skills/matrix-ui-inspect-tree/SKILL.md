---
name: matrix-ui-inspect-tree
description: Dump the UI Toolkit element tree of an open EditorWindow as indented text — one line per element with index path, type, #name, .classes, world rect, text preview, and inline layout-issue flags (TEXT-CLIPPED, OVERFLOWS-PARENT, ZERO-SIZE). The DOM-inspector replacement for screenshots.
---

# UI / Inspect Tree

Dumps the live element tree of an EditorWindow, similar to a web DevTools DOM view.

## Inputs

- `windowName` — EditorWindow class name, full type name, or tab title. Use 'ui-inspect-windows' to discover.
- `startElement` — optional subtree start: '#name', '.class', or index path '0.2.1'. Empty = window root.
- `maxDepth` — recursion cap, default 12.
- `includeHidden` — when true also recurses into display:none subtrees.

## Output format

`[indexPath] Type #name .classes (x= y= w= h=) "text" !ISSUE-FLAGS` — pass the index path (or '#name') to 'ui-inspect-element' for full resolved styles.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "windowName": "value",
  "startElement": "value",
  "maxDepth": 0,
  "includeHidden": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `windowName` | `string` | Yes | EditorWindow class name, full type name, or tab title. Use 'ui-inspect-windows' to list them. |
| `startElement` | `string` | No | Optional subtree start: '#elementName', '.className', or hierarchy index path like '0.2.1'. Empty starts at the window root. |
| `maxDepth` | `integer` | No | Maximum recursion depth below the start element. Default 12. |
| `includeHidden` | `boolean` | No | When true, also recurses into display:none subtrees (hidden tabs/panes). Default false. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "windowName": {
      "type": "string"
    },
    "startElement": {
      "type": "string"
    },
    "maxDepth": {
      "type": "integer"
    },
    "includeHidden": {
      "type": "boolean"
    }
  },
  "required": [
    "windowName"
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
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

