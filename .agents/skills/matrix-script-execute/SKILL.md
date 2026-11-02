---
name: matrix-script-execute
description: Compiles and executes C# code dynamically using Roslyn. Supports a full-code mode (default) and a body-only mode — see the skill body for the difference and for how to pass Unity object references as parameters.
---

# Script / Execute

## Modes

- **Full code mode** (default, `isMethodBody=false`): the `csharpCode` argument must define a complete class with a static method (no top-level statements).
- **Body-only mode** (`isMethodBody=true`): provide only the method body statements. The tool auto-generates the usings, class, and method header. When the body returns a value (`return go.name;`), the method returns `object` and the value comes back as the result; otherwise it is `void`. A `return x;` that sits only inside a lambda or local function keeps the method `void`. If some paths return a value, every path must (end with `return null;`).

## Passing Unity objects as parameters

Unity objects (`GameObject`, `Component`, etc.) can be passed as parameters using their `Ref` types (`GameObjectRef`, `ComponentRef`, etc.) or directly by type:

- `UnityEngine.GameObject` — resolves an actual GameObject from value `{"instanceID": N}`, `{"name": "..."}`, or `{"path": "..."}`.
- `UnityEngine.Component` (or any component subtype) — resolves from `{"instanceID": N}`.
- `Feeder.MCP.Data.GameObjectRef` — passes a `GameObjectRef` POCO directly; the method body calls `goRef.FindGameObject()` to resolve it.
- `Feeder.MCP.Data.ComponentRef` — passes a `ComponentRef` POCO.
- `Feeder.MCP.Data.ObjectRef` — passes a base `ObjectRef` POCO.

## Long runs

A call that runs longer than the project's tool timeout fails with a timeout. For code that is slow on purpose (a bake, a long simulation) pass `timeoutSeconds`; the server caps it (30 minutes by default). A domain reload cuts the call off: the error says so, and the code may have run in part.

## Keeping data between calls

Each call loads a fresh copy of your code, so its `static` fields start empty every time. To keep a value until the next call, store it in the domain: `AppDomain.CurrentDomain.SetData("my-key", value)` and `AppDomain.CurrentDomain.GetData("my-key")`. A domain reload (compile, entering play mode) clears it.

## Watching something change over time

The time between two calls includes your own thinking, often seconds, so polling from outside misses fast changes. Record inside Unity instead: register a callback that samples every frame, removes itself after N seconds, and keeps the samples in the domain; read them in a later call.

```csharp
var samples = new System.Collections.Generic.List<string>();
AppDomain.CurrentDomain.SetData("samples", samples);
var until = EditorApplication.timeSinceStartup + 5;
EditorApplication.CallbackFunction sample = null;
sample = () =>
{
    if (EditorApplication.timeSinceStartup > until) { EditorApplication.update -= sample; return; }
    samples.Add($"{Time.time:0.000} {Time.frameCount}");   // what you want to watch
};
EditorApplication.update += sample;
```

`EditorApplication.update` also runs while the editor window is in the background; in play mode `Application.onBeforeRender` runs once per rendered frame. Several agents can share one editor, so use keys of your own and never leave a callback registered.

## How to Call

Call this tool through the MCP client connected to the local Matrix Bridge server.

Example input:
```json
{
  "csharpCode": "value",
  "className": "value",
  "methodName": "value",
  "parameters": {},
  "isMethodBody": false,
  "timeoutSeconds": 0
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `csharpCode` | `string` | Yes | C# code to compile and execute. In full code mode (default, isMethodBody=false): must define a complete class with a static method. Example: 'using UnityEngine; public class Script { public static void Main() { Debug.Log("Hello"); } }'. Do NOT use top-level statements. In body-only mode (isMethodBody=true): provide only the method body statements. The tool auto-generates usings, class, and method header. Example body: 'go.SetActive(false);'. A body may 'return' a value (e.g. 'return go.name;'), which becomes the result. Custom helper classes can still be defined inline in the body-only string after the main logic, but for complex additional class definitions use full code mode instead. |
| `className` | `string` | No | The name of the class containing the method to execute. In body-only mode this becomes the generated class name. |
| `methodName` | `string` | No | The name of the method to execute. Must be a static method. In body-only mode this becomes the generated method name. |
| `parameters` | `any` | No | Serialized parameters to pass to the method. Each entry must specify 'name' and 'typeName'. Supported parameter types include primitives, strings, and Unity object references: - 'UnityEngine.GameObject': resolves an actual GameObject from value '{"instanceID": N}', '{"name": "..."}', or '{"path": "..."}'. - 'UnityEngine.Component' (or any component subtype): resolves from '{"instanceID": N}'. - 'Feeder.MCP.Data.GameObjectRef': passes a GameObjectRef POCO directly;   the method body calls goRef.FindGameObject() to resolve it. - 'Feeder.MCP.Data.ComponentRef': passes a ComponentRef POCO. - 'Feeder.MCP.Data.ObjectRef': passes a base ObjectRef POCO. If the method does not require parameters, leave this empty. |
| `isMethodBody` | `boolean` | No | When true, 'csharpCode' is treated as just the method body. The tool auto-generates standard using directives (System, UnityEngine, Feeder.MCP.Data, Feeder.MCP.Runtime.Extensions, UnityEditor), the class definition, and the method signature (returns object when the body has 'return value;', otherwise void). Parameters from the 'parameters' list are automatically added to the method signature using their typeName and name. When false (default), 'csharpCode' must be a complete C# compilation unit with class and method definitions. |
| `timeoutSeconds` | `integer` | No | Seconds this call may run before it times out. 0 (default) keeps the project's tool timeout. Set it for code that runs long on purpose; the server caps it (30 minutes by default). |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "csharpCode": {
      "type": "string"
    },
    "className": {
      "type": "string"
    },
    "methodName": {
      "type": "string"
    },
    "parameters": {
      "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMemberList"
    },
    "isMethodBody": {
      "type": "boolean"
    },
    "timeoutSeconds": {
      "type": "integer"
    }
  },
  "$defs": {
    "Feeder.Reflect.Model.SerializedMemberList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember"
      }
    },
    "Feeder.Reflect.Model.SerializedMember": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Full type name. Eg: 'System.String', 'System.Int32', 'UnityEngine.Vector3', etc."
        },
        "name": {
          "type": "string",
          "description": "Object name."
        },
        "value": {
          "description": "Value of the object, serialized as a non stringified JSON element. Can be null if the value is not set. Can be default value if the value is an empty object or array json."
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember",
            "description": "Nested field value."
          },
          "description": "Fields of the object, serialized as a list of 'SerializedMember'."
        },
        "props": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember",
            "description": "Nested property value."
          },
          "description": "Properties of the object, serialized as a list of 'SerializedMember'."
        }
      },
      "required": [
        "typeName"
      ],
      "additionalProperties": false
    }
  },
  "required": [
    "csharpCode"
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
      "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember"
    }
  },
  "$defs": {
    "Feeder.Reflect.Model.SerializedMemberList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember"
      }
    },
    "Feeder.Reflect.Model.SerializedMember": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Full type name. Eg: 'System.String', 'System.Int32', 'UnityEngine.Vector3', etc."
        },
        "name": {
          "type": "string",
          "description": "Object name."
        },
        "value": {
          "description": "Value of the object, serialized as a non stringified JSON element. Can be null if the value is not set. Can be default value if the value is an empty object or array json."
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember",
            "description": "Nested field value."
          },
          "description": "Fields of the object, serialized as a list of 'SerializedMember'."
        },
        "props": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.Reflect.Model.SerializedMember",
            "description": "Nested property value."
          },
          "description": "Properties of the object, serialized as a list of 'SerializedMember'."
        }
      },
      "required": [
        "typeName"
      ],
      "additionalProperties": false
    }
  },
  "required": [
    "result"
  ]
}
```

