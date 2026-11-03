---
name: matrix-ui-inspect-pick
description: "Interactive element picker, like the web DevTools inspect cursor: activates pick mode on an open EditorWindow, the user hovers (live highlight) and clicks the misbehaving element, and the tool returns that element's full detail (index path, styles, detected issues) via the deferred requestId result."
---

# UI / Inspect Pick

Lets the USER point at the broken element instead of describing it or sending screenshots.

## Flow

1. Call with `windowName` (and a `requestId`) — the tool returns Processing immediately.
2. The window shows a highlight overlay following the pointer with the hovered element's identity.
3. The user clicks an element (the click is swallowed, no button is triggered). Esc cancels; a timeout (default 60s) also ends the session.
4. The final result — the same detail block as 'ui-inspect-element' — is delivered through the requestId.

Note: avoid triggering script recompilation while a pick session is active; a domain reload discards it.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "windowName": "value",
  "timeoutSeconds": 0
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `windowName` | `string` | Yes | EditorWindow class name, full type name, or tab title. Use 'ui-inspect-windows' to list them. |
| `timeoutSeconds` | `integer` | No | Seconds before pick mode auto-cancels. Default 60, max 600. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "windowName": {
      "type": "string"
    },
    "timeoutSeconds": {
      "type": "integer"
    }
  },
  "required": [
    "windowName"
  ]
}
```

## Output

This tool does not return structured output.

