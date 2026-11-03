---
name: matrix-hyper-testing
description: "EXPERIMENTAL. Manage the Hyper Testing clone pool for parallel play testing: status, up, refresh, down, ram. Works only after the user enabled Hyper Testing Mode by hand; agents can never enable it. Read the matrix-hyper-testing-guide skill first."
---

# Hyper Testing / Clone Pool (Experimental)

Manages the clone editors used by Hyper Testing Mode (experimental).

## Inputs

- `action` - `status` (default), `up`, `refresh`, `down`, `purge`, `ram`.
- `count` - number of clones for `up` (capped by HyperTesting/config.json maxClones, the VRAM gate and the commit gate).

## Behavior

`up`, `refresh`, `down` and `purge` start a background job and return at once; poll `status` until the job finishes. `purge` stops the clones and deletes their folders (junctions are unlinked first). `ram` takes one memory sample (RAM, commit, import workers) into HyperTesting/Journal/ram-samples.csv. When the mode is off every action returns a refusal: stop and ask the user, never try to enable the mode yourself.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "action": "value",
  "count": 0
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `string` | No | status | up | refresh | down | purge | ram |
| `count` | `integer` | No | Clone count for 'up'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "action": {
      "type": "string"
    },
    "count": {
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
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

