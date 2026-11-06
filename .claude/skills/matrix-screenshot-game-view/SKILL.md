---
name: matrix-screenshot-game-view
description: Capture a screenshot of the Unity Editor's Game View by reading its internal render texture directly. Image size matches the current Game View resolution; the tool corrects Y-flip on DirectX / Metal so the output is always upright. Requires an open Game View window.
---

# Screenshot / Game View

Captures a screenshot from the Unity Editor Game View and returns it as an image. Reads the Game View's own render texture directly via the Unity Editor API. The image size matches the current Game View resolution. Returns the image directly for visual inspection by the LLM.

## Behavior

Locates `UnityEditor.GameView`, repaints it, then reflects the `m_RenderTexture` field and reads back via `Texture2D.ReadPixels`. On graphics APIs whose UV origin is top-left (`SystemInfo.graphicsUVStartsAtTop`), the read-back pixels are vertically flipped before encoding so the orientation matches what the user sees. Returns a PNG image with the resolution baked into the caption.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "nothing": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return structured output.

