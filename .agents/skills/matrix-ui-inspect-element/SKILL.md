---
name: matrix-ui-inspect-element
description: "Full DevTools-style detail of one visual element in an open EditorWindow: rects, measured text size, resolved styles (flex, size, margin/padding, overflow, whiteSpace, fontSize…), detected layout issues, ancestor chain, children, and the style sheets in scope for USS source mapping."
---

# UI / Inspect Element

Returns everything about a single element that a web 'Inspect element' panel would show.

## Inputs

- `windowName` — EditorWindow class name, full type name, or tab title.
- `elementQuery` — '#name', '.class' (first match), or hierarchy index path like '0.2.1' (as printed by 'ui-inspect-tree'). Empty inspects the window root.

## Output

Text block with world/content rects, unconstrained measured text size, detected issues, the full resolved style set relevant to layout, the ancestor chain with rects, direct children, and the StyleSheet assets in scope — element names/classes map directly to the UXML/USS files under the package UI folder.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "windowName": "value",
  "elementQuery": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `windowName` | `string` | Yes | EditorWindow class name, full type name, or tab title. Use 'ui-inspect-windows' to list them. |
| `elementQuery` | `string` | No | Element to inspect: '#elementName', '.className', or hierarchy index path like '0.2.1'. Empty inspects the window root. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "windowName": {
      "type": "string"
    },
    "elementQuery": {
      "type": "string"
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

