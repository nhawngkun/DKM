---
name: matrix-ping
description: Lightweight readiness probe. Returns the input `message` echoed back, or `'pong'` when omitted. Useful for CLI health checks and SignalR connectivity smoke tests.
---

# Ping

Lightweight readiness probe. Returns the input message or 'pong' if omitted.

## Inputs

- `message` (optional) — when present, echoed back verbatim.

## Behavior

No I/O, no Unity API calls — pure echo. Ideal for measuring round-trip latency or confirming the MCP transport is alive before invoking a heavier tool.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "message": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `message` | `string` | No | Optional message to echo back. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "message": {
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
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

