---
name: matrix-ui-inspect-issues
description: "Automatically scan the UI Toolkit tree of one or all open EditorWindows for layout defects: clipped text (measured vs available size), children overflowing their parent, zero-size elements with content. Returns a structured issue list with index paths ready for 'ui-inspect-element'."
---

# UI / Inspect Issues

One-shot layout linter for editor windows — finds the problems a human would spot on a screenshot.

## Detections

- `TEXT-CLIPPED-X/Y` — text measured size exceeds the element's content rect (wrap and nowrap aware, notes ellipsis).
- `OVERFLOWS-PARENT` — element's world rect extends past its parent (ScrollView content is excluded); reports whether the parent clips it or it spills over siblings.
- `ZERO-SIZE` — visible element with children or text but ~0 width/height.

## Inputs

- `windowName` — optional; empty scans every open window that has a UI Toolkit tree.

display:none subtrees are skipped (their layout is not meaningful). Issue count is capped at 200.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "windowName": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `windowName` | `string` | No | Optional EditorWindow class name, full type name, or tab title. Empty scans all open windows with a UI Toolkit tree. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "windowName": {
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
      "$ref": "#/$defs/Feeder.MCP.Data.UiLayoutIssuesResult"
    }
  },
  "$defs": {
    "Feeder.MCP.Data.UiLayoutIssue-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.MCP.Data.UiLayoutIssue"
      }
    },
    "Feeder.MCP.Data.UiLayoutIssue": {
      "type": "object",
      "properties": {
        "Window": {
          "type": "string",
          "description": "Short class name of the EditorWindow the element belongs to."
        },
        "IndexPath": {
          "type": "string",
          "description": "Hierarchy index path of the element (e.g. '0.2.1'). Pass it as 'elementQuery' to 'ui-inspect-element' for full styles."
        },
        "Element": {
          "type": "string",
          "description": "Element description: Type #name .classes — search these identifiers in the UXML/USS source files."
        },
        "Issue": {
          "type": "string",
          "description": "Issue description, e.g. TEXT-CLIPPED-X, TEXT-CLIPPED-Y, OVERFLOWS-PARENT, ZERO-SIZE — with measured pixel details."
        },
        "Text": {
          "type": "string",
          "description": "Truncated text content when the element is a text element."
        },
        "Rect": {
          "type": "string",
          "description": "World-space rect of the element inside the window (x, y, w, h)."
        }
      }
    },
    "System.String-1": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "Feeder.MCP.Data.UiLayoutIssuesResult": {
      "type": "object",
      "properties": {
        "Issues": {
          "$ref": "#/$defs/Feeder.MCP.Data.UiLayoutIssue-1",
          "description": "Detected layout issues, capped at 200 entries."
        },
        "WindowsScanned": {
          "$ref": "#/$defs/System.String-1",
          "description": "Short class names of the windows that were scanned."
        },
        "ElementsScanned": {
          "type": "integer",
          "description": "Total number of visual elements visited during the scan."
        },
        "Note": {
          "type": "string",
          "description": "Extra notes about the scan (caps hit, windows skipped, etc.)."
        }
      },
      "required": [
        "ElementsScanned"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

