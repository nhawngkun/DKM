---
name: matrix-rql-mutate
description: "Change many objects, components and assets in one query: `objects | where name startswith \"Enemy\" | set Rigidbody.mass = 2`. Also `call` methods, `add` components, `destroy` objects and create them with `new(...)`. All-or-nothing, one undo step, optional dry run."
---

# Query / Change

# Query / Change

Select targets with the same pipeline as 'rql-query', then end with one or more changing stages.

## Examples

```
find("Player") | set position = [0, 1, 0], name = "Hero"
objects | where name startswith "Enemy" | set Rigidbody.mass = Rigidbody.mass * 2
objects | where tag == "Pickup" | set active = false
find("Lamp") | set Light.color = "#ffcc88", Light.intensity = 2, Light.type = "Spot"
find("Crate") | set transform.localScale.y = 2
asset("Assets/Materials/Red.mat") | set $["_BaseColor"] = color(1, 0, 0)
new("Spawn", "Cube", "Level/Props") | set position = [2, 0, 5] | add Rigidbody | set mass = 3
objects | where name == "Door" | call transform.SetParent(find("House").transform)
objects | where name matches "^Temp_" | destroy
```

## Stages that change things

- `set PATH = EXPR, ...` writes members. Paths reach into structs (`transform.position.x`), components (`Light.intensity`), shader properties (`$["_BaseColor"]`, `sharedMaterial["_Color"]`) and array elements (`sharedMaterials[0]`). Every right-hand side is computed from the values before the change.
- `call Method(args)` or `call member.Method(args)` runs a method on each target.
- `add Type` adds a component to each GameObject; the next stages work on the new components.
- `destroy` removes GameObjects or components (not assets, not Transforms).
- `new("Name"[, "Cube"][, parent])` as the source creates a GameObject (in the open prefab when prefab mode is on).

## Safety

- **All-or-nothing**: every target is checked before anything changes; if one fails, nothing changes and the error lists each problem. If a later stage fails, the earlier stages are rolled back.
- **One undo step** named 'Matrix query' (Edit > Undo reverts the whole query).
- `dryRun=true` runs the query, reports every change as `target | action | old | new`, then reverts it. `call` is not run in a dry run.
- `maxTargets` (default 100) refuses queries that would touch more targets; narrow with `where` or raise it on purpose.
- Scenes and assets are marked dirty, not saved, unless `save=true`.

## Values

Numbers convert to the member's type (`3` to float, `1` to an enum value). Enums take names (`"Spot"`, flags `"A|B"`). Vectors take `[x, y, z]` or `vec(x, y, z)`; rotations take euler `[x, y, z]`; colors take `"#rrggbb"`, names or `color(r, g, b[, a])`. Object fields take `find("Path")`, `asset("Assets/...")`, or a path string. Lists fill arrays.

Use 'rql-schema' with a type name to see which members can be written.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "query": "value",
  "dryRun": false,
  "maxTargets": 0,
  "save": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | The query, e.g. find("Player") | set position = [0, 1, 0]. Stages: set PATH = EXPR, call Method(args), add Type, destroy; source new("Name"[, "Cube"][, parent]). |
| `dryRun` | `boolean` | No | true: report every change (target | action | old | new), then revert it. Nothing stays changed. |
| `maxTargets` | `integer` | No | Refuse when a changing stage would touch more targets than this (default 100). |
| `save` | `boolean` | No | true: save the touched scenes, prefab and assets after the change. Default false: only marked dirty. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    },
    "dryRun": {
      "type": "boolean"
    },
    "maxTargets": {
      "type": "integer"
    },
    "save": {
      "type": "boolean"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return structured output.

