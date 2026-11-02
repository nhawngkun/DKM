# Graph Report - DKM  (2026-11-01)

## Corpus Check
- Large corpus: 711 files · ~128,562 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 1887 nodes · 4540 edges · 107 communities (51 shown, 56 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 295 edges (avg confidence: 0.71)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- KCC Walkthrough Ladder Controller
- Imports & Namespaces Hub
- Vector/Rect/Camera Extensions
- KCC Collision Callbacks
- KinematicCharacterMotor Core
- DateTime & Number Utils
- Transform Extensions
- Color & Interpolate Processes
- FastPool Editor & Audio Utils
- Editor Reflection Utils
- Core Runtime Namespaces
- System Imports & Validators
- Android Device Utils
- Camera Mediator System
- ICharacterController Grounding
- Character Rotation & Stability
- TickManager & Tickables
- Character Velocity & Movers
- Combo & Camera Feedback
- KCC Before/After Update
- FastPool Core
- Editor Assembly Imports
- String Extensions
- FastPoolManager
- List Extensions
- Process Manager
- PhysicsMover
- Core Editor Menu Items
- FSM Core
- Animator Hash Generator
- Legacy Camera Follow
- Global Define RSP Utils
- KinematicCharacterSystem Sim
- File Utils
- Scene & UA Editor Tools
- GameHelper & JSON
- Core Package Manifest
- Weighted Randomizer
- Example Camera & Player
- Example Character States
- UI & Editor Imports
- Linear Interpolate Processes
- CharacterAnim Wrapper
- Player Input Pipeline
- CharController States
- Singletons & Launchers
- Walkthrough Player Input A
- Walkthrough Controller A
- EventManager Delegates
- FSM Actions
- CountDown Process
- Walkthrough Player Input B
- Walkthrough Rotation
- Walkthrough Player Input C
- Bool Extensions
- CountDown Pause Process
- Timed Process
- Transform Interpolate Process
- Moving Platforms
- Walkthrough Camera Input
- Planet & Teleporter Demo
- Stress Test Manager
- Walkthrough Player Input D
- Walkthrough Player Input E
- Walkthrough Controller B
- Walkthrough Player Input F
- Walkthrough Player Input G
- Walkthrough Controller C
- Walkthrough Player Input H
- Walkthrough Player Input I
- Walkthrough Controller D
- Walkthrough Player Input J
- Walkthrough Player Input K
- Walkthrough Controller E
- Walkthrough Player Input L
- Game Events
- Fade In/Out Processes
- Walkthrough Player Input M
- Walkthrough Player Input N
- Ladder Climbing State
- Walkthrough Player Input O
- Walkthrough Player Input P
- AI Character Inputs
- KCC Registration & Settings
- Charging State Demo
- NoClip State Demo
- Swimming State Demo
- Dictionary Extensions
- Define Symbols Utils
- FSM Action Types
- Process State Enum
- Device Type Utils
- Movement Sweep State
- Pause State Handler
- Runtime Asset Utils
- Pool Notification Types
- Pair Types
- Bonus Orientation Enum
- PhysicsMover State
- Email Validator
- Encryption Utils
- Orientation Enum
- ReadOnly Attribute
- Unity Constant Generator

## God Nodes (most connected - your core abstractions)
1. `KinematicCharacterMotor` - 113 edges
2. `HitStabilityReport` - 74 edges
3. `TransformExtensions` - 61 edges
4. `KinematicCharacterController` - 51 edges
5. `PhysicsMover` - 41 edges
6. `ExampleCharacterController` - 40 edges
7. `ExampleCharacterCamera` - 36 edges
8. `CharController` - 36 edges
9. `MyCharacterController` - 33 edges
10. `ICharacterController` - 32 edges

## Surprising Connections (you probably didn't know these)
- `CameraForward` --inherits--> `TickableBehaviour`  [EXTRACTED]
  Assets/_Asset/Script/CameraScript/CameraFoward.cs → Packages/core-main-main/Runtime/TickableManager/TickableBehaviour.cs
- `CharController` --implements--> `ICharacterController`  [EXTRACTED]
  Assets/_Asset/Script/CharController.cs → Assets/KinematicCharacterController/Core/ICharacterController.cs
- `KinematicCharacterMotor` --references--> `ICharacterController`  [EXTRACTED]
  Assets/KinematicCharacterController/Core/KinematicCharacterMotor.cs → Assets/KinematicCharacterController/Core/ICharacterController.cs
- `ExampleCharacterController` --implements--> `ICharacterController`  [EXTRACTED]
  Assets/KinematicCharacterController/ExampleCharacter/Scripts/ExampleCharacterController.cs → Assets/KinematicCharacterController/Core/ICharacterController.cs
- `MyCharacterController` --implements--> `ICharacterController`  [EXTRACTED]
  Assets/KinematicCharacterController/Walkthrough/10- Multiple movement states setup/Scripts/MyCharacterController.cs → Assets/KinematicCharacterController/Core/ICharacterController.cs

## Import Cycles
- None detected.

## Communities (107 total, 56 thin omitted)

### Community 0 - "KCC Walkthrough Ladder Controller"
Cohesion: 0.05
Nodes (17): MyCharacterController, CurrentCharacterState, MyCharacterController, CurrentCharacterState, MyCharacterController, CurrentCharacterState, ClimbingState, Anchoring (+9 more)

### Community 1 - "Imports & Namespaces Hub"
Cohesion: 0.08
Nodes (19): CharacterState, Default, KinematicCharacterController.Walkthrough.BasicMovement, KinematicCharacterController.Walkthrough.LandingLeavingGround, KinematicCharacterController.Walkthrough.RootMotionExample, KinematicCharacterController.Walkthrough.WallJumping, KinematicCharacterController.Walkthrough.CollisionFiltering, KinematicCharacterController (+11 more)

### Community 2 - "Vector/Rect/Camera Extensions"
Cohesion: 0.05
Nodes (6): CameraExtensions, RectTransformExtensions, VectorExtensions, GuideMask, CameraTools, Vector2Utils

### Community 3 - "KCC Collision Callbacks"
Cohesion: 0.07
Nodes (3): HitStabilityReport, MyCharacterController, MyCharacterController

### Community 4 - "KinematicCharacterMotor Core"
Cohesion: 0.06
Nodes (32): CharacterGroundingReport, CharacterTransientGroundingReport, KinematicCharacterMotor, AttachedRigidbody, AttachedRigidbodyVelocity, CharacterForward, CharacterRight, CharacterTransformToCapsuleBottom (+24 more)

### Community 5 - "DateTime & Number Utils"
Cohesion: 0.06
Nodes (10): DateTimeExtensions, MathUtils, NumberConverter, NumberLengthType, Long, Medium, Short, VeryLong (+2 more)

### Community 7 - "Color & Interpolate Processes"
Cohesion: 0.06
Nodes (11): ShakeCamera, FrameratePanel, ColorExtensions, IPoolable, SpriteFadeProcess, t, TransformInterpolateProcess, TransformScaleProcess (+3 more)

### Community 8 - "FastPool Editor & Audio Utils"
Cohesion: 0.05
Nodes (15): KinematicCharacterMotorEditor, ReadOnlyPropertyDrawer, FrameratePanelEditor, ToolbarExtender, FastPoolEditor, FastPoolManagerEditor, FPUniversalDespawnerEditor, AudioExtensions (+7 more)

### Community 9 - "Editor Reflection Utils"
Cohesion: 0.06
Nodes (9): EditorParameters, Instance, ToolbarCallback, BB_EditorUtils, ReflectionUtils, Assemblies, AssemblyNames, NameSpaceCache (+1 more)

### Community 10 - "Core Runtime Namespaces"
Cohesion: 0.08
Nodes (6): AnimatorParameters, NabaGame.Core.Runtime.Extensions, NabaGame.Core.Runtime.Process, NabaGame.Core.Runtime.Utils, NabaGame.Core.Runtime.FSM, FloatExtensions

### Community 11 - "System Imports & Validators"
Cohesion: 0.08
Nodes (6): UnityToolbarExtender, NabaGame.Core.Runtime.Singleton, NabaGame.Core.Runtime.WeightedRandomization, PrefabAttribute, SingletonAttribute, IPValidator

### Community 12 - "Android Device Utils"
Cohesion: 0.08
Nodes (11): UnbiasedTime, Instance, DisplayMetricsAndroid, Density, DensityDPI, HeightPixels, ScaledDensity, WidthPixels (+3 more)

### Community 13 - "Camera Mediator System"
Cohesion: 0.07
Nodes (10): CameraController, CameraEffects, CameraFocus, AirBlend, CurrentRotation, PlanarDirection, CameraFollow, TargetDistance (+2 more)

### Community 14 - "ICharacterController Grounding"
Cohesion: 0.06
Nodes (5): ICharacterController, MyCharacterController, CurrentCharacterState, MyCharacterController, MyCharacterController

### Community 16 - "TickManager & Tickables"
Cohesion: 0.08
Nodes (9): CameraForward, NabaGame.Core.Runtime.TickManager, Singleton, Instance, ITickable, TickableBehaviour, TickManager, TickableDict (+1 more)

### Community 18 - "Combo & Camera Feedback"
Cohesion: 0.12
Nodes (5): ComboCameraFeedback, FloatUnityEvent, CharacterCombo, ComboHitInfo, Progress01

### Community 20 - "FastPool Core"
Cohesion: 0.10
Nodes (8): MemoryPool, FastPool, Cached, ID, IsValid, Name, IFastPoolItem, WaitUtils

### Community 23 - "FastPoolManager"
Cohesion: 0.20
Nodes (4): FastPoolManager, Instance, Pools, FastPoolExtensions

### Community 25 - "Process Manager"
Cohesion: 0.16
Nodes (7): OnTerminateCallback, Process, Attached, State, ProcessManager, ProcessRythmer, ProcessManager

### Community 26 - "PhysicsMover"
Cohesion: 0.13
Nodes (11): PhysicsMover, AngularVelocity, IndexInCharacterSystem, InitialSimulationPosition, InitialSimulationRotation, InitialTickPosition, InitialTickRotation, Transform (+3 more)

### Community 27 - "Core Editor Menu Items"
Cohesion: 0.13
Nodes (3): AnimatorHashGeneratorWindow, CoreEditorMenuItem, PrefabExtention

### Community 28 - "FSM Core"
Cohesion: 0.19
Nodes (4): FSM, Name, StateActionProcessor, FSMState

### Community 29 - "Animator Hash Generator"
Cohesion: 0.17
Nodes (4): AnimatorHashGeneratorData, Instance, FolderPath, AssetUtils

### Community 35 - "Scene & UA Editor Tools"
Cohesion: 0.15
Nodes (3): SceneSwitchLeftButton, SceneEditor, UAEditor

### Community 37 - "Core Package Manifest"
Cohesion: 0.15
Nodes (13): author, email, name, url, dependencies, com.unity.textmeshpro@3.0.6, description, displayName (+5 more)

### Community 38 - "Weighted Randomizer"
Cohesion: 0.21
Nodes (6): WeightedChance, AdjustedWeight, Value, Weight, WeightedRandomizer, ElementList

### Community 39 - "Example Camera & Player"
Cohesion: 0.17
Nodes (6): ExampleCharacterCamera, FollowTransform, PlanarDirection, TargetDistance, Transform, MyPlayer

### Community 40 - "Example Character States"
Cohesion: 0.29
Nodes (4): CharacterState, Default, ExampleCharacterController, CurrentCharacterState

### Community 42 - "Linear Interpolate Processes"
Cohesion: 0.21
Nodes (4): LinearInterpolateProcess, Current, OnTimeUpdate, TransformMoveXProcess

### Community 46 - "Singletons & Launchers"
Cohesion: 0.22
Nodes (3): PrefabLauncher, SerializedSingleton, Instance

### Community 49 - "EventManager Delegates"
Cohesion: 0.22
Nodes (5): EventDelegate, EventDelegate, EventManager, DelegateLookupCount, Instance

### Community 51 - "CountDown Process"
Cohesion: 0.22
Nodes (3): CountDownProcess, Current, OnTimeUpdate

### Community 57 - "CountDown Pause Process"
Cohesion: 0.25
Nodes (3): CountDownProcessPause, Current, OnTimeUpdate

### Community 58 - "Timed Process"
Cohesion: 0.28
Nodes (3): TimedProcess, t, TimePortion

### Community 59 - "Transform Interpolate Process"
Cohesion: 0.22
Nodes (3): TranformInterpolateProcess, CurrentPosition, Vector3Utils

### Community 60 - "Moving Platforms"
Cohesion: 0.29
Nodes (3): IMoverController, PlayableMover, MyMovingPlatform

### Community 62 - "Planet & Teleporter Demo"
Cohesion: 0.25
Nodes (3): PlanetManager, Teleporter, isBeingTeleportedTo

### Community 77 - "Game Events"
Cohesion: 0.25
Nodes (3): NabaGame.Core.Runtime.EventManager, GameEvent, IEventHandler

### Community 78 - "Fade In/Out Processes"
Cohesion: 0.29
Nodes (4): FadeInProcess, FadeOutProcess, InterpolateProcess, CurrentValue

### Community 81 - "Ladder Climbing State"
Cohesion: 0.29
Nodes (4): CharacterState, Climbing, Default, KinematicCharacterController.Walkthrough.ClimbingLadders

### Community 87 - "Charging State Demo"
Cohesion: 0.33
Nodes (4): CharacterState, Charging, Default, KinematicCharacterController.Walkthrough.ChargingState

### Community 88 - "NoClip State Demo"
Cohesion: 0.33
Nodes (4): CharacterState, Default, NoClip, KinematicCharacterController.Walkthrough.NoClipState

### Community 89 - "Swimming State Demo"
Cohesion: 0.33
Nodes (4): CharacterState, Default, Swimming, KinematicCharacterController.Walkthrough.SwimmingState

### Community 92 - "FSM Action Types"
Cohesion: 0.33
Nodes (6): StateActionType, OnDestroy, OnEnter, OnExit, OnFixUpdate, OnUpdate

### Community 93 - "Process State Enum"
Cohesion: 0.33
Nodes (5): ProcessState, CANCELLED, CREATED, STARTED, TERMINATED

### Community 94 - "Device Type Utils"
Cohesion: 0.40
Nodes (4): DeviceType, Phone, Tablet, DeviceUtils

### Community 95 - "Movement Sweep State"
Cohesion: 0.40
Nodes (5): MovementSweepState, AfterFirstHit, FoundBlockingCorner, FoundBlockingCrease, Initial

### Community 98 - "Pool Notification Types"
Cohesion: 0.40
Nodes (5): PoolItemNotificationType, BroadcastMessage, Interface, None, SendMessage

### Community 99 - "Pair Types"
Cohesion: 0.50
Nodes (4): Pair, Key, Value, PairIntLong

### Community 100 - "Bonus Orientation Enum"
Cohesion: 0.50
Nodes (4): BonusOrientationMethod, None, TowardsGravity, TowardsGroundSlopeAndGravity

### Community 104 - "Orientation Enum"
Cohesion: 0.67
Nodes (3): OrientationMethod, TowardsCamera, TowardsMovement

## Knowledge Gaps
- **148 isolated node(s):** `None`, `Kinematic`, `SimulatedDynamic`, `None`, `Standard` (+143 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 380 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **56 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KinematicCharacterMotor` connect `KinematicCharacterMotor Core` to `KCC Walkthrough Ladder Controller`, `KCC Collision Callbacks`, `Transform Extensions`, `Camera Mediator System`, `ICharacterController Grounding`, `Character Rotation & Stability`, `Character Velocity & Movers`, `KCC Before/After Update`, `KinematicCharacterSystem Sim`, `GameHelper & JSON`, `Example Camera & Player`, `Example Character States`, `CharController States`, `Walkthrough Controller A`, `Walkthrough Rotation`, `Walkthrough Controller B`, `Walkthrough Controller C`, `Walkthrough Controller D`, `Walkthrough Controller E`, `KCC Registration & Settings`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **What connects `None`, `Kinematic`, `SimulatedDynamic` to the rest of the system?**
  _148 weakly-connected nodes found - possible missing edges._
- **Should `KCC Walkthrough Ladder Controller` be split into smaller, more focused modules?**
  _Cohesion score 0.052943354313217325 - nodes in this community are weakly interconnected._
- **Why does `ExampleCharacterCamera` connect `Example Camera & Player` to `Imports & Namespaces Hub`, `Vector/Rect/Camera Extensions`, `KCC Collision Callbacks`, `KinematicCharacterMotor Core`, `Transform Extensions`, `Camera Mediator System`, `Character Velocity & Movers`, `GameHelper & JSON`, `Walkthrough Player Input A`, `Walkthrough Player Input B`, `Walkthrough Player Input C`, `Walkthrough Camera Input`, `Walkthrough Player Input D`, `Walkthrough Player Input E`, `Walkthrough Player Input F`, `Walkthrough Player Input G`, `Walkthrough Player Input H`, `Walkthrough Player Input I`, `Walkthrough Player Input J`, `Walkthrough Player Input K`, `Walkthrough Player Input L`, `Walkthrough Player Input M`, `Walkthrough Player Input N`, `Walkthrough Player Input O`, `Walkthrough Player Input P`?**
  _High betweenness centrality (0.058) - this node is a cross-community bridge._
- **Should `Imports & Namespaces Hub` be split into smaller, more focused modules?**
  _Cohesion score 0.07525150905432595 - nodes in this community are weakly interconnected._
- **Why does `ExampleCharacterController` connect `Example Character States` to `KCC Walkthrough Ladder Controller`, `Imports & Namespaces Hub`, `KCC Collision Callbacks`, `Bonus Orientation Enum`, `KinematicCharacterMotor Core`, `GameHelper & JSON`, `Transform Extensions`, `Orientation Enum`, `Camera Mediator System`, `ICharacterController Grounding`, `Character Rotation & Stability`, `Character Velocity & Movers`, `KCC Before/After Update`, `AI Character Inputs`, `Planet & Teleporter Demo`, `Stress Test Manager`?**
  _High betweenness centrality (0.041) - this node is a cross-community bridge._
- **Should `Vector/Rect/Camera Extensions` be split into smaller, more focused modules?**
  _Cohesion score 0.054244306418219465 - nodes in this community are weakly interconnected._