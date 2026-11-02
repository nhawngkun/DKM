---
name: matrix-assets-refresh
description: Refresh the Unity AssetDatabase. Use after files were added or updated outside of the Unity API, or to force script recompilation when a '.cs' file changed. Returns a processing/success response and waits for compilation when triggered.
---

# Assets / Refresh

Refreshes the AssetDatabase. Use it if any file was added or updated in the project outside of Unity API. Use it if need to force scripts recompilation when '.cs' file changed.

## Inputs

- `options` — `ImportAssetOptions` flag (default `ForceSynchronousImport`).
- `requestId` — required for processing-mode responses; the tool returns `Processing` immediately and delivers a follow-up completion when compilation finishes.

## Behavior

Runs `AssetDatabase.Refresh(options)`. If `EditorApplication.isCompiling` is true after the refresh, schedules a post-compilation notification and returns a `Processing` response. If compilation already failed (`EditorUtility.scriptCompilationFailed`), returns `Success` with the compilation error details. Otherwise returns a plain `Success`.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "options": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `options` | `any` | No | Asset import options. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "options": {
      "$ref": "#/$defs/UnityEditor.ImportAssetOptions"
    }
  },
  "$defs": {
    "UnityEditor.ImportAssetOptions": {
      "type": "string",
      "enum": [
        "Default",
        "ForceUpdate",
        "ForceSynchronousImport",
        "ImportRecursive",
        "DontDownloadFromCacheServer",
        "ForceUncompressedImport"
      ]
    }
  }
}
```

## Output

This tool does not return structured output.

