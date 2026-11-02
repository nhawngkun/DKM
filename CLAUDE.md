# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity **6000.3.19f1** (Unity 6) third-person action game prototype (Devil May Cry–style: Dante model, sword combo animations) using **HDRP**. There is no build script, test suite, or linter — work is done in the Unity Editor. The `.csproj`/`DKM.sln` files are Unity-generated (don't edit them by hand); `Library/`, `Temp/`, `Logs/`, `UserSettings/` are ignored.

- Compile check outside the editor (optional): `dotnet build DKM.sln` — only valid after Unity has regenerated the project files; the Editor is the source of truth for compile errors.
- Main scene in build settings: `Assets/OutdoorsScene.unity`; also `Assets/Sence/SetUp.unity`.
- Code comments are mostly in Vietnamese; follow that convention when editing existing files.

## Where the game code lives

Project-owned scripts are only in `Assets/_Asset/Script/` (global namespace, no asmdef → `Assembly-CSharp`). Everything else under `Assets/` is third-party or imported content (KinematicCharacterController, DOTween/Odin in `Plugins/`, art packs like Lordenfel, Erbeilo3D, dungeon-stone1). Avoid modifying third-party code unless needed — with one exception below.

Key packages: Input System (polled directly via `Keyboard.current` / `Gamepad.current`, not via `InputSystem_Actions` callbacks), UniTask, Cinemachine 2.x, Animation Rigging, DOTween, Odin Inspector, and the embedded local package `Packages/core-main-main` (`com.nabagame.core`, namespace `NabaGame.Core.Runtime.*`: EventManager, FSM, FastPool, Singletons, TickableManager, extensions, plus editor tools like an Animator hash generator).

## Architecture

**Character movement (Kinematic Character Controller)**
- `CharController` implements KCC's `ICharacterController`; `KinematicCharacterMotor` calls back into it (`UpdateVelocity`, `UpdateRotation`, etc.). It is adapted from KCC's `ExampleCharacterController` and adds dash (with cooldown/air dash), double jump (`MaxAirJumps`), and orientation switching. It has a `CharacterState` state machine (`TransitionToState` / `OnStateEnter` / `OnStateExit`, currently only `Default`).
- `PlayerCharacterInputs`, `AICharacterInputs`, `CharacterState`, `OrientationMethod`, `BonusOrientationMethod` are declared at the bottom of `CharController.cs` in the global namespace.
- Input is read in **`Assets/KinematicCharacterController/ExampleCharacter/Scripts/ExamplePlayer.cs`** — this third-party file has been modified to drive the project's `CharController` and `CameraController` (keyboard WASD/Space/E dash/F orientation, gamepad). New player inputs go there: add a field to `PlayerCharacterInputs`, fill it in `ExamplePlayer.HandleCharacterInput`, consume it in `CharController.SetInputs`.

**Animation**
- `CharacterAnim` is a thin wrapper over the `Animator`; `CharController` sets its fields (`isMove`, `_moveX/_moveY`) and calls its methods (`Jump`, `Dash`, `OnLanding`…), and `CharacterAnim.Update` pushes values to the Animator each frame.
- All Animator parameter hashes live in `Contanst/AnimatorParameters.cs` — add new parameters there rather than using string names.
- `CharacterCombo` handles melee combos (currently on the `K` key, polled in its own `Update`): randomly picks one of several combos, tracks hit index, queues input, and resets after `ComboResetTime`. It relies on **Animation Events** in the clips (e.g. `OnComboAnimEnd` mid-clip opens the cancel/continue window), and raises `event Action<ComboHitInfo> OnAttackHit` for other systems.

**Camera (mediator pattern)**
- `CameraController` is the single public API for the camera; it owns three independent modules — `CameraFocus` (rotation/look), `CameraFollow` (position, collision via `IgnoredColliders`), `CameraEffects` — and passes data between them. Gameplay code should call `CameraController`, not the modules directly; modules don't reference each other.
- `ComboCameraFeedback` (hitstop via `Time.timeScale`, shake, gamepad rumble) is triggered only by `AnimEvent_...` methods called from Animation Events on attack clips, so effects line up with the impact frame. It intentionally no longer auto-subscribes to `OnAttackHit`.
- Some components (e.g. `CameraForward`) derive from `TickableBehaviour` from the core package's TickableManager instead of using `Update`.

## Working with the Unity Editor via MCP (Matrix Bridge)

The project has the `com.feeder.mcp` package (Matrix Bridge). Its MCP server is `Feeder-MCP` (`.mcp.json`, `http://localhost:25894/mcp`); the Editor side is `Tools/Feeder/Bridge` and must read **ONLINE**. Several AI sessions and people may share the one Editor, so follow these rules:

**When to use MCP vs. files**
- Use files (Read/Edit/Write) for `.cs` code and docs. Use MCP to **look at** and **change** the live Editor: scenes, prefabs, components, the console, play mode.
- Do **not** hand-edit Unity YAML (`.unity`, `.prefab`, `.controller`, `.asset`) while the Editor is open; the Editor can overwrite it or reimport it half-done. Change these through MCP (`rql-mutate`, or a mutating `script-execute` with the Unity API, e.g. `AnimatorController` for Animator transitions or IK Pass). If the Editor is closed or Matrix is offline, YAML edits are allowed. Say which route you took.
- Before changing anything, read it first: `rql-schema` (syntax + a type's members), then `rql-query`. Examples: `objects | where CharController | select name, $path`, `scenes`.

**Editor lock (details: `matrix-shared-editor` skill)**
- Read-only tools need no lock: `editor-application-get-state`, `editor-health`, `editor-lock-status`, `rql-query`, `rql-schema`, `console-get-logs`, `screenshot-game-view`.
- Editor-exclusive work needs a lock first: play mode, anything that compiles (`assets-refresh`, script writes), `scene-open/save`, prefab save, `rql-mutate`, mutating `script-execute`, `console-clear-logs`, `tests-run`, package add/remove, PlayerPrefs/EditorPrefs/`Time.timeScale` changes.
- `editor-lock-acquire` (`owner` = `claude-<task-slug>`, `task` = a few words), then check `rql-query scenes` and the state. If the Editor is playing/compiling and you didn't start it, or a scene is dirty that you didn't touch, release and stop: a person is working.
- Hold the lock for **one burst** (one compile + read the console, or one play session, or one edit batch → save), max 15 minutes, then `editor-lock-release`. Release before thinking, writing code or waiting on the user.
- Never exit someone else's play mode, never answer a dialog you didn't open, never `console-clear-logs`. Read the console only from your claim's start time.
- Leave the Editor as you found it: exit play mode, restore `Time.timeScale`/prefs, no unsaved scenes you dirtied.

**Standard flow after editing C#**
1. Write the `.cs` file → `editor-lock-acquire` → `assets-refresh` → wait for `editor-application-get-state` to show `isCompiling: false`.
2. `console-get-logs` (errors/warnings since the claim). Fix and repeat; release between rounds.
3. When play-mode proof is needed (controls, combo, camera, IK), use the same claim: open `Assets/Sence/SetUp.unity` (or the scene that holds `Player.prefab`), enter play mode, gather proof (`console-get-logs`, `screenshot-game-view`, `rql-query` reading values), exit play mode, release.
4. If the Editor is busy for more than ~10 minutes, write `TestCases/<yyyyMMdd-HHmm>-<slug>.md` (template in the `matrix-shared-editor` skill), report "written, NOT verified", and stop.

**Tools and troubleshooting**
- Only a core set of tools is on by default. Find a tool with `unity-tool-list`, turn it on with `tool-set-enabled-state`, then run `unity-skill-generate` to refresh the skills. Call tools you don't have loaded with `tool-call`.
- A timeout or no response → `editor-health` (reloading/compiling → wait; stalled because of a dialog → tell the user). If it says "Not linked", Matrix is off: say the work is **not verified** and don't guess.
- Report honestly: something done through MCP but not checked again in the console/play mode counts as not verified.
