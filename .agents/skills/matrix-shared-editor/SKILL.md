---
name: matrix-shared-editor
description: "Protocol for a Unity Editor shared by several AI sessions and humans at once: which Matrix tools need exclusive use of the Editor, the editor lock Matrix keeps (editor-lock-acquire / editor-lock-release / editor-lock-status; held by one MCP session for one burst of Editor work - a compile, one play session - and released between bursts, never for a whole task; other sessions' editor-changing tools are refused while it is held; freed on release, when the session closes or after 10 idle minutes; shown to people in the Matrix Bridge window), the state check that catches a human in play mode, and the test-case handoff - when the Editor is busy the author writes TestCases/<stamp>-<slug>.md and stops, and a later verifier session runs the pending cases. Use BEFORE entering play mode, compiling, opening or saving a scene, wiping prefs or save data, clearing the console or running tests, and when asked to run pending test cases."
---

# Shared Editor

One Unity Editor per project, one MCP bridge, many sessions. Play mode, compilation, open scenes,
the console, EditorPrefs, PlayerPrefs and save files belong to the whole Editor, so two sessions that
test at the same time corrupt each other's result - and sometimes each other's assets. This skill
serializes Editor use and turns "the Editor is busy" into a written handoff instead of a collision.

Claim the Editor here before each compile or play session, compile, read the console, play and
collect evidence the way the project's own workflow says, then give the Editor back at once. The lock
is for the Editor, not for your task: while you think, code or wait, other sessions must be able to play.

Matrix keeps the lock. While another session holds it, Matrix refuses your editor-changing tools
and names the holder; read-only tools always run. While nobody holds it, nothing is refused -
claiming first is still your job. People see the holder in the Matrix Bridge window and as a green
frame around the Scene view, and can free a claim from the window. A person who wants the Editor
tells the agents to stop; when the user asks you to stop, release at once and do no more
Editor-exclusive work until they say so. Tools that
are not in your tool list may only be disabled: call `unity-tool-list`, then `tool-set-enabled-state`.

## Rule 1 - What needs the lock

**Editor-exclusive** - claim the lock first:

- enter / exit play mode (`editor-application-set-state`)
- anything that triggers a compile: `script-update-or-create`, `script-delete`, `assets-refresh`
- `script-execute` that mutates anything, or that runs while you are playing
- `scene-open`, `scene-save`, `scene-unload`, prefab stage open/save, `rql-mutate`, `assets-delete`
- wiping or changing PlayerPrefs, EditorPrefs or save files; changing `Time.timeScale`
- `console-clear-logs` (it deletes other sessions' evidence)
- `tests-run`, `package-add`, `package-remove`

**Free** - no lock:

- reading code, writing `.cs` or docs on disk with file tools
- read-only MCP queries: `editor-application-get-state`, `rql-query`, `rql-schema`, `console-get-logs`,
  `*-get-data`, screenshots, `editor-health`, `editor-lock-status`

Matrix decides what it refuses by the tool's read-only mark, so while someone else holds the lock even
a `script-execute` that only reads is refused: read with `rql-query` or `console-get-logs` instead.

A file written on disk still reaches the Editor: Unity refreshes when its window regains focus, and
Hot Reload (if installed) patches a save straight into whoever is playing. Matrix cannot refuse that,
which is why the verifier compares the tree before and after its run (Rule 5).

Never stop, pause or exit someone else's play mode. Never answer a modal dialog you did not open.

### Hold it for one burst, not for the task

A burst is the shortest run of Editor-exclusive calls that another session must not cut into:

- **a compile:** `assets-refresh` (or a script write) -> wait until it stops compiling -> read the console.
- **a play session:** set the preconditions -> enter play mode -> steps and evidence -> exit play mode ->
  restore (Rule 6).
- **an edit-mode batch:** `scene-open` -> `rql-mutate` / mutating `script-execute` -> `scene-save`.

A compile immediately followed by the play session that tests it may share one claim.

Acquire right before the burst and release right after it. **Release before anything that does not
need the Editor**: thinking, reading or writing code and docs, running Blender or another tool, waiting
for a subagent, a reviewer or the user, writing a test case or a report. A review loop, a fix round or a
verifier with several cases claims the lock once per burst, never across the whole loop.

**Cap: 15 minutes per claim**, counted from the acquire. If a burst would run past it, finish the
current step, exit play mode, restore, release, and claim again. Another session may get the Editor in
between; that is the point. Then wait for it or hand off (Rule 4).

Every new claim is a new start: run the Rule 3 check again and read the console only from the new
claim's time. Scenes, prefs and the compiled code may have changed since your last burst.

## Rule 2 - The lock

Three tools, served by the Matrix server itself (they answer even while Unity reloads):

- **`editor-lock-acquire`** with `task` (what the claim is for, a few words) and `owner`
  (`<agent>-<task-slug>`, e.g. `claude-catwalk-score`; people read it in the Editor). Answers
  `acquired ...` plus the Editor state, or an error starting with `busy:` that names the holder, its
  client and session, its task and since when.
- **`editor-lock-release`** - gives the Editor back. Only releases your own claim.
- **`editor-lock-status`** - who holds it, for what, since when, and whether it is you. Read-only.

How the claim behaves:

- It belongs to your MCP session. Subagents that run inside your session share it: never let two of
  them work the Editor at once.
- Every tool call you make counts as activity. A claim with no call from you for **10 minutes** is
  freed (a call still running keeps it), and so is a claim whose MCP session closed. There is no
  heartbeat to send.
- Acquiring again while you hold it starts a new claim (and a new 15-minute cap).
- Acquire answers `busy` while the Editor is in play mode and nobody holds the lock: a person is
  playing, or a session left play mode on. Do not stop it.
- A person can free your claim from the Matrix Bridge window.
- The claim is mirrored to `Temp/agent-editor-lock/lock.json` for sessions still on the lock-file
  scripts. A live lock file written by such a session makes acquire answer `busy` (Matrix cannot tell
  which session it is, so it refuses nobody's tools for it); one with no heartbeat for 20 minutes is
  removed.

Outcomes:

- **Busy** -> the Editor is taken, go to Rule 4. A claim past the 15-minute cap is still live: report
  its owner, task and start time so the user can tell that session to release, or free it from the
  Matrix Bridge window.
- **A tool refused with "Refused '<tool>': held by ..."** -> someone else holds the Editor. Stop all
  Editor-exclusive work. If you thought you held it, your claim was freed (idle, session closed, freed
  by a person): `editor-lock-status` says so. Report it; acquire again only from a clean start (Rule 3).
- **Release answers "your claim was already freed (<reason>)"** -> same: the Editor may have changed
  under you. Treat the burst's evidence as suspect.
- **Release** at the end of every burst - exit play mode, restore what you changed (Rule 6), confirm
  `rql-query` `scenes` reports no scene dirtied by you, then `editor-lock-release`. A session that ends,
  fails or is interrupted releases too.
- **The lock tools are not in your tool list** -> the Matrix server is older than the package. Use the
  lock file by hand ("Older server" at the end), and tell the user to reinstall the server from the
  Matrix Bridge menu.

## Rule 3 - Check the Editor after acquiring

The acquire answer already carries the Editor state, and acquire refuses a play mode nobody claimed.
What it cannot see is unsaved work. Right after acquiring, call `rql-query` `scenes`, and
`editor-application-get-state` when the answer said the Editor is compiling or playing:

- `isPlaying`, `isPaused` or `isCompiling` true when you did not cause it -> a human is using the
  Editor. Release and hand off (Rule 4).
- A dirty scene you did not touch -> someone has unsaved work. Do not `scene-open` in Single mode or
  enter play mode over it; release and hand off.

A check one call earlier is not enough. Every mutating edit-mode `script-execute` still starts with:

```csharp
if (EditorApplication.isPlayingOrWillChangePlaymode) return "abort: play mode";
```

## Rule 4 - Author: finishing a task

When the task needs play-mode proof, try to acquire the lock.

- **Acquired and the Editor is clean** -> verify in bursts (Rule 1): compile and read the console,
  play and collect evidence, release. If a result sends you back to the code, release first, fix, then
  claim again for the next compile and play. Report after the last release.
- **Busy** -> claims are short, so retry a few times over up to 10 minutes. Still busy -> handoff below.
- **Human in the Editor, still busy after retrying, or verification cut short** (focus trap, lost lock)
  -> write the handoff and stop:
  1. Create `TestCases/` at the project root if missing, and make sure `.gitignore` ignores `/TestCases/`.
     Never put test cases under `Assets/` - a new file there triggers an import in the shared Editor.
  2. Write `TestCases/<yyyyMMdd-HHmm>-<slug>.md` from the *Test case template* at the end of this skill.
     One file per task, never append to another session's file. Status `pending`.
  3. Fill `Compiled` honestly: `no` if you did not get a clean console for this exact tree.
  4. Record every touched file with `git hash-object <path>`.
  5. Write steps a session with zero context can run through MCP: the tool, the arguments, the
     `script-execute` snippet or debug button, and the exact log line / state read / screenshot that
     proves it. "Check that it works" is not a step.
  6. Report: **written, NOT verified**, with the path to the test case. Do not wait for the Editor, do
     not compile, do not enter play mode.

If the project root has a `TESTCASE.md`, read it first - it holds that project's reset recipes and traps.

## Rule 5 - Verifier: running pending cases

Triggered by a request like "run the pending test cases".

1. List `TestCases/*.md` with `Status: pending`, oldest first. A `running` case whose owner holds no
   claim (`editor-lock-status`) was abandoned - treat it as `pending` and note that in its run log.
2. Snapshot the tree: `git status --porcelain` and `git hash-object` of every touched file.
3. Acquire the lock (Rule 2) and check the Editor (Rule 3). Busy -> retry as in Rule 4, then report
   and stop.
4. For each case (one claim per case, Rule 1 - release between cases whenever you stop to read, think
   or write, and re-acquire with the Rule 3 check before the next compile or play):
   - Set `Status: running (<owner>, <time>)`. Claim the lock again if you released it.
   - Compare the recorded hashes with the current ones. Different -> the code moved after the case was
     written: read the diff and adapt the steps before running them, and note it in the run log.
   - Compile and read the console. Filter `console-get-logs` by this claim's start time; do not clear
     the console.
   - Set the preconditions, run the steps, collect the evidence, then, when a panel changed, repeat it: open it twice in a row, leave a serialized reference empty, and invoke a callback that was never assigned.
   - **FAIL** -> exit play mode, restore and release the lock while you write the fix, then claim it
     again to re-run; at most **3 fix rounds**, then `Status: blocked`. Fix a file only if
     it is unchanged since your snapshot in step 2 **and** was not modified in the 10 minutes before
     it - otherwise another session may still be editing it: record the failure and the evidence,
     leave the code alone. Every fix follows the project's coding standard, is logged in the run log
     (file + reason) and gets its hash updated in the case header. Do not commit.
   - Before writing **PASS**, compare `git status --porcelain` with the step-2 snapshot plus your own
     fixes. Anything else changed during the run -> the result is suspect; re-run the case once.
   - Clean up (Rule 6) and set `Status: pass`, `fail` or `blocked`, with the evidence.
5. Make sure the lock is released. Report per case: status, the evidence lines, the fixes made, anything not verified.

## Rule 6 - Leave the Editor as you found it

- Exit play mode.
- EditorPrefs: read the value before changing it, restore **that value** afterwards - do not delete a key
  you found set.
- PlayerPrefs / save files: back up before a wipe, restore after. A wipe is never the default.
- `Time.timeScale` back to what it was.
- Open scenes and the active scene as they were; nothing dirtied by you.
- If Enter Play Mode Options disable domain reload, static state survives between play sessions: one
  test leaks into the next. A case that needs first-boot state must say how to reset it.

## Rule 7 - Report honestly

A test case written is not a test run. Say "written, NOT verified" for a handoff, and for a verifier run
list exactly which cases ran, what proved each one, and which were skipped and why.

## Rule 8 - A stuck Editor

When a tool times out, or the Editor stops answering, do not guess. Call `editor-health`: the server
answers it itself, even while the Editor is hung or reloading. Read its first line and the window list.

- **Reloading** or **Busy: compiling / importing** -> wait and call it again; the Editor comes back.
- **Ticking slowly, Editor not focused** -> not a hang; calls are only slower.
- **Main thread stalled ... in flight: none** -> something outside Matrix holds the Editor: a dialog, an
  import, another package. The window list names it. Tell the user what it says and wait for them; do
  not switch off or remove packages of the project, and do not answer a dialog you did not open.
- **Main thread stalled ... Likely cause: <tool>** -> your own call holds it (a long or endless
  `script-execute`). Wait for it, or ask the user before going further.
- **Not linked** -> the Editor is closed or Matrix is off; report the task as not verified.

## Rule 9 - Long jobs

A tool call ends at the project's tool timeout. For work that takes longer:

- `script-execute` with `timeoutSeconds` for code that runs long on purpose (the server caps it, 30
  minutes by default).
- Or start the job inside Unity and come back for the result. Schedule it from `EditorApplication.update`
  (it runs while the Editor is in the background; `EditorApplication.delayCall` does not), remove the
  callback when it ran, and write a marker file under `Temp/` with the job's name and state
  (`started`, `done`, `failed`). Poll the marker, not the connection. While you poll, keep calling
  tools at least every few minutes, or release and claim again, so your claim does not go idle.
- Before you schedule a job again, read its marker and `editor-application-get-state`. A dropped
  connection or a domain reload does not mean the job did not run: never schedule it a second time
  because the link went away.

## Test case template

```markdown
# <Task title>

- **Status:** pending
- **Written:** <yyyy-MM-dd HH:mm> by <owner>
- **Branch @ HEAD:** <branch> @ <short sha> (plus uncommitted changes below)
- **Compiled:** yes, console clean | no - Editor was busy
- **Touched files** (`git hash-object`):
  - `<path>` - `<hash>`

## Why

What changed, in one paragraph, and what it could break. Name the flows a regression would show up in.

## Preconditions

- Scene(s) open, and which one is active:
- Prefs / save state (with how to back up and restore it):
- Config values or debug toggles:
- Anything from the project's `TESTCASE.md` reset recipe that applies:

## Cases

### TC1 - <what is proven>

- **Steps:**
  1. `<mcp tool>` with `<args>` / `script-execute`: `<snippet>` / debug button `<Class.Method>`
- **Expect:** `<exact log line>` / `<state read and value>` / screenshot showing `<what>`
- **Result:** _

### TC2 - <edge case: re-entry, missing reference, empty data, second call>

- **Steps:**
- **Expect:**
- **Result:** _

## Standing passes

Only when a panel changed: open twice in a row, empty serialized reference, unassigned callback.

## Cleanup

What to restore after the run: prefs, save file, timeScale, scenes.

## Not covered

What these cases cannot prove (real SDK, device-only, multi-day timer) - so nobody reads PASS as more
than it is.

## Run log

<!-- verifier appends: time, owner, per-case PASS/FAIL/BLOCKED + evidence, fixes (file + reason) -->
```

## Older server: the lock file by hand

Only when the `editor-lock-*` tools are missing. The lock is then a directory,
`<project>/Temp/agent-editor-lock/`, where `<project>` is the folder holding
`ProjectSettings/ProjectVersion.txt`; creating a directory is atomic, so exactly one of two racing
sessions gets it. No `Temp/` means the Editor is closed: nothing to claim, report the task as not
verified. Inside it, `lock.json`:

```json
{ "owner": "claude-catwalk-score-1530", "task": "catwalk score rounding", "acquired": "2026-09-14T15:30:00+07:00", "heartbeat": 1789367400 }
```

`heartbeat` is Unix seconds; a lock whose heartbeat is older than 1200 s (20 minutes) is stale. Nothing
refuses tools for this lock: it binds only sessions that read this skill. Run the scripts from the
project root, with `OWNER` and `TASK` set first (plain words, no quotes).

POSIX shell (Git Bash on Windows, macOS, Linux):

```sh
lock=Temp/agent-editor-lock
lock_acquire() {
  [ -d Temp ] || { echo "no Temp/: the Editor is closed"; return 1; }
  if mkdir "$lock" 2>/dev/null; then
    printf '{"owner":"%s","task":"%s","acquired":"%s","heartbeat":%s}\n' \
      "$OWNER" "$TASK" "$(date +%Y-%m-%dT%H:%M:%S%z)" "$(date +%s)" > "$lock/lock.json"
    echo acquired
  else
    cat "$lock/lock.json" 2>/dev/null; echo busy; return 1
  fi
}
lock_heartbeat() {
  grep -q "\"owner\":\"$OWNER\"" "$lock/lock.json" 2>/dev/null || { echo "lost the lock"; return 1; }
  sed -i.bak "s/\"heartbeat\":[0-9]*/\"heartbeat\":$(date +%s)/" "$lock/lock.json" && rm -f "$lock/lock.json.bak"
  echo beat
}
lock_release() {
  grep -q "\"owner\":\"$OWNER\"" "$lock/lock.json" 2>/dev/null || { echo "not mine, left alone"; return 1; }
  rm -rf "$lock" && echo released
}
lock_break_stale() {
  beat=$(sed -n 's/.*"heartbeat":\([0-9]*\).*/\1/p' "$lock/lock.json" 2>/dev/null)
  [ -n "$beat" ] && [ $(( $(date +%s) - beat )) -gt 1200 ] || { echo "not stale"; return 1; }
  # delete only if nobody beat in between
  [ "$(sed -n 's/.*"heartbeat":\([0-9]*\).*/\1/p' "$lock/lock.json")" = "$beat" ] && rm -rf "$lock" && echo "stale lock removed"
}
```

PowerShell (Windows PowerShell 5.1 and later):

```powershell
$lock = 'Temp\agent-editor-lock'
function Write-Lock($data) { [IO.File]::WriteAllText((Join-Path (Get-Location) "$lock\lock.json"), ($data | ConvertTo-Json -Compress)) }
function Read-Lock { try { Get-Content "$lock\lock.json" -Raw -ErrorAction Stop | ConvertFrom-Json } catch { $null } }
function Lock-Acquire {
  if (-not (Test-Path Temp)) { 'no Temp/: the Editor is closed'; return }
  try { New-Item -ItemType Directory $lock -ErrorAction Stop | Out-Null } catch { Read-Lock; 'busy'; return }
  Write-Lock ([ordered]@{ owner = $owner; task = $task; acquired = (Get-Date -Format o); heartbeat = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() })
  'acquired'
}
function Lock-Heartbeat {
  $data = Read-Lock
  if ($null -eq $data -or $data.owner -ne $owner) { 'lost the lock'; return }
  $data.heartbeat = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds(); Write-Lock $data; 'beat'
}
function Lock-Release {
  $data = Read-Lock
  if ($null -eq $data -or $data.owner -ne $owner) { 'not mine, left alone'; return }
  Remove-Item $lock -Recurse -Force; 'released'
}
function Lock-BreakStale {
  $data = Read-Lock
  if ($null -eq $data -or [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - $data.heartbeat -le 1200) { 'not stale'; return }
  # delete only if nobody beat in between
  if ((Read-Lock).heartbeat -eq $data.heartbeat) { Remove-Item $lock -Recurse -Force; 'stale lock removed' }
}
```

- **Busy** (the directory exists): read `lock.json`. Live -> Rule 4. A lock directory with no
  `lock.json` is live (its owner is between `mkdir` and the write) unless the directory itself is older
  than 20 minutes. Never delete a live lock.
- **Stale**: re-read `lock.json` right before deleting and delete only if `heartbeat` is unchanged; then
  acquire once.
- **Heartbeat** only inside a burst, before a step that can take minutes; if `owner` is someone else,
  you lost the lock by going stale - stop all Editor-exclusive work and report.
- **Release** at the end of every burst, only if `owner` is still you.
