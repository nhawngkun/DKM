# Graph Report - Script  (2026-11-06)

## Corpus Check
- Corpus is ~28,140 words - fits in a single context window. You may not need a graph.

## Summary
- 454 nodes · 1056 edges · 19 communities (8 shown, 11 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 32 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Combo Core & Weapon Data
- Lock-On & Coat Body Collision
- Animation Pose Tools
- Camera Controller Modules
- File & Import Hub
- Foot IK & Root Motion
- Coat Verlet Physics
- Legacy Camera Follow
- Weapon Combo Builder
- Combo Hit Feedback
- CharController State API
- CharacterAnim Wrapper
- Coat Cloth Baking
- Idle Breath Generator
- Input & Orientation Types
- Camera Shake UniTask
- Character State Machine

## God Nodes (most connected - your core abstractions)
1. `CharController` - 59 edges
2. `CharacterCombo` - 49 edges
3. `PlayerIK` - 35 edges
4. `CoatPhysics` - 27 edges
5. `WeaponComboBuilder` - 24 edges
6. `ComboCameraFeedback` - 23 edges
7. `WeaponData` - 21 edges
8. `CharacterAnim` - 18 edges
9. `AttackContext` - 18 edges
10. `CameraFocus` - 17 edges

## Surprising Connections (you probably didn't know these)
- `ComboCameraFeedback` --references--> `CharacterCombo`  [EXTRACTED]
  CameraScript/ComboCameraFeedback.cs → CharacterCombo.cs
- `CharController` --references--> `JumpProfile`  [EXTRACTED]
  CharController.cs → Combat/WeaponData.cs
- `WeaponHolder` --references--> `CharController`  [EXTRACTED]
  Combat/WeaponHolder.cs → CharController.cs
- `Rig` --references--> `CharacterCombo`  [EXTRACTED]
  Editor/WeaponComboBuilder.cs → CharacterCombo.cs
- `WeaponDef` --references--> `WeaponType`  [EXTRACTED]
  Editor/WeaponComboBuilder.cs → Combat/WeaponData.cs

## Import Cycles
- None detected.

## Communities (19 total, 11 thin omitted)

### Community 0 - "Combo Core & Weapon Data"
Cohesion: 0.06
Nodes (29): CharacterCombo, CurrentContext, CurrentFade, CurrentStep, CurrentWeapon, FadeInDone, IsAttacking, IsInRecovery (+21 more)

### Community 1 - "Lock-On & Coat Body Collision"
Cohesion: 0.09
Nodes (10): Capsule, CoatBodyCollision, Bound, CoatHipFollow, Link, GroundMode, Air, PinFeet (+2 more)

### Community 3 - "Camera Controller Modules"
Cohesion: 0.08
Nodes (8): CameraController, CameraEffects, CameraFocus, AirBlend, CurrentRotation, PlanarDirection, CameraFollow, TargetDistance

### Community 4 - "File & Import Hub"
Cohesion: 0.08
Nodes (5): CharacterLightRig, AnimatorParameters, GameManager, LockOnTarget, LockPosition

### Community 6 - "Coat Verlet Physics"
Cohesion: 0.13
Nodes (4): CameraForward, Capsule, CoatPhysics, Particle

### Community 8 - "Weapon Combo Builder"
Cohesion: 0.24
Nodes (4): ChainDef, StepDef, WeaponComboBuilder, WeaponDef

### Community 10 - "CharController State API"
Cohesion: 0.12
Nodes (9): CharController, CurrentCharacterState, CurrentLockTarget, HasMoveInput, IsAttacking, IsDashing, IsGrounded, IsInCombat (+1 more)

### Community 15 - "Input & Orientation Types"
Cohesion: 0.17
Nodes (9): AICharacterInputs, BonusOrientationMethod, None, TowardsGravity, TowardsGroundSlopeAndGravity, OrientationMethod, TowardsCamera, TowardsMovement (+1 more)

## Knowledge Gaps
- **41 isolated node(s):** `CurrentRotation`, `AirBlend`, `PlanarDirection`, `TargetDistance`, `CurrentCharacterState` (+36 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 112 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CharController` connect `CharController State API` to `Combo Core & Weapon Data`, `Lock-On & Coat Body Collision`, `Camera Controller Modules`, `File & Import Hub`, `Foot IK & Root Motion`, `Combo Hit Feedback`, `CharacterAnim Wrapper`, `KCC Collision Callbacks`, `Input & Orientation Types`, `Character State Machine`, `Landing Events`?**
  _High betweenness centrality (0.207) - this node is a cross-community bridge._
- **What connects `CurrentRotation`, `AirBlend`, `PlanarDirection` to the rest of the system?**
  _41 weakly-connected nodes found - possible missing edges._
- **Should `Combo Core & Weapon Data` be split into smaller, more focused modules?**
  _Cohesion score 0.06229508196721312 - nodes in this community are weakly interconnected._
- **Why does `CharacterCombo` connect `Combo Core & Weapon Data` to `Lock-On & Coat Body Collision`, `Animation Pose Tools`, `Camera Controller Modules`, `File & Import Hub`, `Foot IK & Root Motion`, `Combo Hit Feedback`, `CharController State API`, `Character State Machine`?**
  _High betweenness centrality (0.175) - this node is a cross-community bridge._
- **Should `Lock-On & Coat Body Collision` be split into smaller, more focused modules?**
  _Cohesion score 0.08527131782945736 - nodes in this community are weakly interconnected._
- **Why does `ComboCameraFeedback` connect `Combo Hit Feedback` to `Combo Core & Weapon Data`, `Camera Controller Modules`, `Idle Breath Generator`, `Legacy Camera Follow`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Should `Animation Pose Tools` be split into smaller, more focused modules?**
  _Cohesion score 0.1166429587482219 - nodes in this community are weakly interconnected._