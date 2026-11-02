---
name: matrix-rql-query
description: Read anything in the open scenes, prefabs, assets, console and project with one pipeline query (`objects | where Light.intensity > 1 | select name, $path`). Filters, projects, sorts, groups and counts many objects in one call and answers as a compact text table. Use 'rql-schema' for the syntax and a type's members, 'rql-mutate' to change things.
---

# Query / Read

# Query / Read

One call replaces many find/get calls: pick a source, then chain stages with `|`.

## Examples

```
objects | where Light.intensity > 1 | select name, Light.intensity, $path
objects | where name startswith "Enemy" and active | count
objects | components | group $type
components("Light") | where type == "Point" | sort intensity desc | take 5
find("Player").transform.position
find("Player") | descendants | where not activeSelf | select $path
assets("t:Material", "Assets/Art") | where shader.name contains "Lit" | select name, $path, $["_BaseColor"]
logs | where type == "Error" | take 20
scenes
```

## Language

```
query := source ( '|' stage )*
sources: objects (every GameObject in the open scenes, or the open prefab), roots, selection, scenes, logs, components, prefab,
  assets("t:Material"[, "Assets/Folder"]), asset("Assets/x.mat"), find("Path/Or/Name" | id), obj(id), any expression (find("Player").transform)
  rql-mutate only: new("Name"[, "Cube|Sphere|Capsule|Cylinder|Plane|Quad"[, parent]])
stages: where EXPR | select EXPR [as name], ... | sort EXPR [desc], ... | take N | skip N | first | last | count | reverse
  | distinct [EXPR] | group EXPR (rows: key, count) | sum/avg/min/max [EXPR] | get EXPR (map, flattens lists)
  | components [Type] | children | descendants [depth] | parent | gameobject
  rql-mutate only: set PATH = EXPR, ... | call METHOD(args) | add Type | destroy
expressions: members (name, transform.position.x, Light.intensity: a component by its type on a GameObject),
  index a[0], material["_BaseColor"], go["Rigidbody"], meta $ $index $id $path $type,
  literals 1 2.5 "text" 'text' true false null [1, 2, 3], operators == != < <= > >= and or not ?? + - * / %,
  contains startswith endswith (ignore case) matches (regex) in [..], is Type, is null
GameObject shortcuts: parent active position localPosition rotation eulerAngles localEulerAngles localScale
values to write: numbers, "Enum" or "A|B", [x,y,z] or vec(x,y,z) for vectors, [x,y,z] euler for rotations,
  "#ff8800" or color(1,0.5,0) for colors, objects by find()/asset()/"Assets/path"/"Hierarchy/Path", lists for arrays
missing members read as null with a warning; queries only call reading methods (Get*, Is*, Has*, Find*, To*...)
```

## Output

- `table` (default): a header line, then one line per row with ` | ` between cells. GameObjects show `id | path | active | components`, components `id | type | path | enabled`, assets `id | type | name | path`. `select` picks your own columns.
- `json`: `{"total":N,"items":[...]}` with vectors as arrays and objects as `{id,name,type,path}`.
- Only `limit` rows are printed (default 100); the first line says how many matched and how to page with `skip`/`take`.
- A member that does not exist reads as `null` and adds a line under `warnings:`; check it when a filter matches nothing.

## Tips

- Read `sharedMaterial`, not `material`: reading `material` in edit mode makes a copy of the material.
- `rql-query` only calls reading methods (`GetComponent`, `CompareTag`, `ToString`...). Use 'rql-mutate' for anything else.
- Ids (`$id`) are instance ids: they change after a domain reload, paths do not.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "query": "value",
  "limit": 0,
  "format": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | The query: a source (objects, roots, selection, scenes, logs, components, assets("t:Type"), asset("Assets/..."), find("Path")) followed by stages separated by '|': where, select, sort, take, skip, count, distinct, group, sum/avg/min/max, get, components, children, descendants, parent. |
| `limit` | `integer` | No | Maximum rows printed (default 100, max 2000). The total count is always reported. |
| `format` | `string` | No | table (default, compact) or json. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    },
    "limit": {
      "type": "integer"
    },
    "format": {
      "type": "string"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return structured output.

