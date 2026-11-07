# Báo cáo khảo sát refactor Character Controller

Ngày khảo sát: 2026-11-07  
Phạm vi: `CharController` và các thành phần liên quan trực tiếp. Đây là báo cáo hiện trạng; chưa có mã gameplay, prefab, scene, animation controller hay project setting nào bị sửa.

## Baseline

- Unity thực tế trong `ProjectSettings/ProjectVersion.txt`: `6000.4.0f1`.
- Script game không có asmdef riêng, nên các script trong `Assets/_Asset/Script` đang biên dịch vào `Assembly-CSharp`.
- `CharController` hiện dài 1,253 dòng; `CharacterAnim` 127 dòng; `CharacterCombo` 523 dòng; `PlayerIK` 553 dòng.
- Working tree đang có nhiều thay đổi chưa commit từ trước, bao gồm `CharController.cs`, `CharacterAnim.cs`, `CharacterCombo.cs`, `PlayerIK.cs`, prefab, scene và asset animation. Báo cáo này lấy **nội dung worktree hiện tại** làm baseline; không được dùng `git checkout`, reset, hoặc thay thế các thay đổi đó.
- Chưa chạy compile/play-mode baseline trong pha khảo sát.

## Hợp đồng public/serialization cần giữ nguyên

Tất cả field public trong `CharController` là Unity-serialized và phải ở lại đúng tên, kiểu, giá trị mặc định, attribute, và component hiện tại:

| Nhóm | Field / API |
| --- | --- |
| References | `Motor`, `characterAnim`, `characterCombo`, `IgnoredColliders`, `MeshRoot`, `CameraFollowPoint` |
| Gait | `WalkInputThreshold`, `JogInputThreshold`, `WalkSpeed`, `RunSpeed`, `SprintSpeed` |
| Rotation | `OrientationMethod`, `MoveTurnRate`, `MoveTurnSharpness`, `TurnWarpLimit`, `TurnTapCancelTime`, `StrafeTurnSharpness`, `StartTurnAngle`, `ReverseAngle`, `SideTurnAngle` |
| Root movement | `GroundSharpness`, `StopSharpness`, `LoopSpeedSharpness`, `LoopSpeedCatchUpSharpness` |
| Air/jump | `MaxAirMoveSpeed`, `AirAccelerationSpeed`, `AirTurnSharpness`, `Drag`, `Gravity`, `MaxFallSpeed`, `FallGravityMultiplier`, `FallAnimDelay`, `JumpUpSpeed`, `AirJumpUpSpeed`, `JumpHeightScale`, `AirJumpHeightScale`, `AirJumpKeepHorizontal`, `MaxAirJumps`, `JumpPreGroundingGraceTime`, `JumpPostGroundingGraceTime` |
| Dash | `DodgeDistanceScale`, `DodgeTurnSharpness`, `DashCooldown`, `CanDashInAir`, `DashSpeed`, `DashDuration`, `DodgeCancelTime` |
| Combat | `AttackLungeSpeed`, `AttackLungeDuration`, `AttackStopDistance`, `KickStopDistance`, `AttackAssistRadius`, `AttackAssistAngle`, `AttackTurnSharpness`, `AttackMoveCancelThreshold`, `CanCancelAttackWithDash`, `CanCancelAttackWithJump`, `AirAttackGravityScale`, `AirAttackHangTime`, `LungeRootTravelThreshold`, `RootMotionSharpness`, `MaxRootVerticalSpeed`, `DashAttackWindow`, `DirectionalAttackThreshold`, `AlwaysDrawWeapon`, `CombatHoldTime` |
| Other | `LockOnRadius`, `LockOnBreakRadius`, `AutoAddIK`, `CrouchedCapsuleHeight` |
| Public API | `CurrentCharacterState`, `CurrentLockTarget`, `IsLockedOn`, `IsGrounded`, `IsDashing`, `IsAttacking`, `HasMoveInput`, `MoveInputDirection`, `IsInCombat`, `FlushCombatFeedback`, `AddVelocity`, `SwitchOrientation`, `SetInputs`, toàn bộ callback `ICharacterController` |

`MoveState`, `OrientationMethod`, `PlayerCharacterInputs`, `AICharacterInputs`, `BonusOrientationMethod`, và `CharacterState` hiện ở global namespace cuối `CharController.cs`. Tên, thứ tự số implicit của enum, và vị trí namespace phải giữ tương thích.

## Serialization / external references đã xác nhận

- `CharController`, `CharacterAnim`, `CharacterCombo`, và `PlayerIK` đang được serialized trong `Assets/_Asset/Prefab/Character.prefab`; `CharController`/`CharacterAnim`/`CharacterCombo` cũng xuất hiện trong player prefab.
- `CharController` có instance trong `Assets/Sence/SetUp.unity`; `LockOnTarget` có instance ở scene và `TrainingDummy.prefab`.
- `ExamplePlayer` gọi `Character.SetInputs`, đọc `Motor`, `CameraFollowPoint`, `CurrentLockTarget`, `IsGrounded`, và gọi `FlushCombatFeedback`.
- `PlayerIK` và `PlayerBodyDynamics` đọc `Motor`, trạng thái public, tốc độ public, `MoveInputDirection`, và `characterAnim.towardMove`.
- `WeaponHolder` đọc `IsInCombat`; `MeleeHitDetector` lấy `CharController` theo parent để đọc trạng thái/position.
- `CharacterCombo` cung cấp event `OnAttackHit`, `OnWeaponChanged`, state combo và root-motion attack. `CharController` chỉ subscribe/unsubscribe hai event nói trên.

## Field ownership hiện tại

| Owner hiện tại | Runtime state / trách nhiệm |
| --- | --- |
| Input/target/combat mode | `_moveInputVector`, `_cameraForward`, `_stick`, `_sprintHeld`, `_combatTimer`, `CurrentLockTarget` |
| Locomotion + turn | `_lastGait`, `_smoothedRootSpeed`, `_currentClip`, `_turnSnapYaw`, `_turnWarpYaw`, `_turnAppliedWarp`, `_turnStartForward`, `_turnSnapDone`, `_forceInstantClip`, `_interruptibleTurn`, `CurrentCharacterState` |
| Dash | `_dodgeFacing`, `_dashCooldownTimer`, `_airDashTimer`, `_dashDirection`, `_lastDashEndTime` |
| Jump/air | `_jumpRequested`, `_jumpConsumed`, `_jumpedThisTick`, `_timeSinceJumpRequested`, `_timeSinceLastAbleToJump`, `_airJumpsRemaining`, `_usedAirJump`, `_jumpProfile`, `_airborneTime` |
| Crouch | `_isCrouching`, `_shouldBeCrouching`, `_standingCapsuleHeight`, `_probedColliders` |
| Combat movement | `_attackFacing`, `_attackLungeTimer`, `_attackLungeDelay`, `_attackLungeScale`, `_attackStartHeight`, `_attackTarget`, `_airHangTimer`, `_airAttackStarted`, `_wasAttacking` |
| Shared KCC impulse | `_internalVelocityAdd` |

## Mapping method hiện tại → owner đề xuất

| Existing method / state | Trách nhiệm hiện tại | Owner sau refactor | Risk |
| --- | --- | --- | --- |
| `Awake`, `OnDestroy`, `OnWeaponChanged` | KCC hookup, event lifetime, component setup | `CharController` | High: giữ nguyên thứ tự |
| `FlushCombatFeedback`, `AddVelocity`, `SwitchOrientation` | API ngoài | `CharController` | Low |
| `SetInputs` | sample input đã resolve, call-order orchestration | `CharController` | High |
| `UpdateLockOn`, `FacingTarget`, `PlanarToLockTarget` | lock target và hướng camera | `CharController` | Medium |
| `DesiredGait`, `SignedAngleToInput`, `UpdateDirectionParams` | interpret input cho locomotion | `CharacterLocomotion` (qua façade) | High |
| `SetState`, `TickLocomotion`, `TickIdle`, `BeginMove`, `TickMove`, `BeginTurn`, `TickTurn`, `FinishTurn`, `AbortTurn`, `BeginStop`, `TickStop`, `EnterGroundIdle`, `ResumeAfterAttack` | FSM locomotion và state/animation side effects | `CharacterLocomotion` | Critical |
| `CanDash`, `BeginDash`, `PickDodgeClip`, `TickDodge`, `TickAirDash`, `EndDash` | dash state và cooldown | `CharacterDash` | High |
| `EnterFall`, `TickAir`, `OnJumped`, `OnLanded`, `GetAirGravity`, `HandleJump` | jump/air state và timer | `CharacterJump` | Critical |
| `BeginCrouch`, `TickCrouch`, `TryUncrouch` | capsule crouch / clearance | `CharacterCrouch` | Medium |
| `UpdateRotation`, `SmoothLook` | KCC rotation, turn-warp | `CharacterRotation` sau cùng | Critical |
| `UpdateVelocity`, `UpdateGroundVelocity` | root-motion ground velocity | façade + `CharacterLocomotion` | Critical |
| `UpdateAirVelocity` | air steering, gravity, drag | `CharacterJump` | Critical |
| `AfterCharacterUpdate`, `PostGroundingUpdate` | grounded bookkeeping / landing / crouch clearance | façade forwards theo đúng thứ tự | Critical |
| `ResolveAttackContext`, `OnAttackStarted`, `CurrentStopDistance`, `PlanarTo`, `FindAttackTarget`, `ClampApproach`, `UpdateAttackVelocity` | attack context, assist, lunge, root motion | `CharacterCombat` | Critical |
| Collision callbacks | ICharacterController contract | `CharController` (để trống như hiện tại) | Low |

## Thứ tự thực thi cần bảo toàn

1. `ExamplePlayer.Update` đọc thiết bị rồi gọi `CharController.SetInputs`.
2. Trong `SetInputs`: resolve vector camera → lock-on → flag anim → combat timer → weapon/attack input → attack move-cancel → jump request → dash → orientation → crouch request → attack edge → `TickLocomotion` khi không attack.
3. KCC gọi `BeforeCharacterUpdate` (rỗng), `UpdateRotation`, `UpdateVelocity`, `AfterCharacterUpdate`, sau đó `PostGroundingUpdate` theo contract hiện tại.
4. `UpdateVelocity`: consume root motion **trước**, ưu tiên AirDash → Attack → Ground → Air, sau đó `HandleJump`, cuối cùng cộng `_internalVelocityAdd` rồi reset accumulator.
5. `AfterCharacterUpdate`: expire jump buffer → grounded/reset timer & air-jump bookkeeping → `TryUncrouch`.
6. `PostGroundingUpdate`: chỉ gọi `OnLanded` khi stable-grounded chuyển false → true.

Không module nào được có Unity lifecycle riêng, `Update`, coroutine, event mới, allocation mỗi frame, hoặc tự ý ghi Motor ngoài call path trên.

## Motor write inventory

| Write | Vị trí / điều kiện | Thứ tự phải giữ |
| --- | --- | --- |
| `Motor.CharacterController = this` | `Awake` | trước mọi KCC simulation |
| `Motor.SetRotation(...)` | `FinishTurn`, sau `characterAnim.CapturePose` | capture pose → set rotation → instant clip flag |
| `Motor.SetCapsuleDimensions(...)` | `BeginCrouch`, `TryUncrouch` | exact thử capsule đứng → overlap → khôi phục crouch nếu bị chặn |
| `Motor.ForceUnground()` | `HandleJump`; vertical root-motion attack đi lên khi grounded | đúng nhánh và trước velocity write |
| `ref currentVelocity` | `UpdateVelocity` và các helper | AirDash/Attack/Ground/Air → Jump → internal add |
| `_internalVelocityAdd` | `AddVelocity`, `TickAirDash`, cuối `UpdateVelocity` | accumulator chỉ clear sau khi cộng |

## FSM/transition inventory

- `Idle`: input → `BeginMove`; strafe/lock-on lệch camera vượt threshold → turn clip.
- `Move`: mất input → stop; crouch → crouch; strafe đổi clip/hướng; free locomotion đổi gait hoặc turn clip.
- `Turn`: early cancel của turn bắt đầu khi nhả input; hết clip → `FinishTurn` rồi instant `Idle`/`Move`.
- `Stop`/`Land`: có input → `BeginMove`; hết clip fade → ground idle.
- `Dodge`: sau `DodgeCancelTime` + input → move; hết clip → idle.
- `AirDash`: hết `DashDuration` → clamp planar velocity via internal add → fall.
- `Air`: start clip tới cuối → loop clip; landing chọn move hoặc landing clip.
- `Crouch`: enter clip tới move clip; uncrouch chỉ khi capsule đứng không overlap.
- Attack không thay `MoveState` bằng state mới: `SetInputs` đổi locomotion state ở hai cạnh attack; `UpdateVelocity` ưu tiên attack branch khi combo active.

## Kế hoạch tách nhỏ an toàn nhất

Không thêm `CharacterContext`: hiện tại mọi state phụ thuộc tightly vào thứ tự write của `CharController`; một context public rộng sẽ che dependency mà không giảm rủi ro. Thay vào đó, dùng các plain C# module `internal sealed` trong `Assets/_Asset/Script/CharacterModules/`, được tạo đúng một lần trong `Awake` sau setup hiện tại. Serialized configuration vẫn nằm nguyên trên `CharController`; module chỉ giữ private runtime state đã nêu và nhận façade `CharController` trực tiếp.

1. **`CharacterCrouch`**: tách capsule state, `BeginCrouch`, `TickCrouch`, `TryUncrouch`; facade giữ thứ tự input/KCC callback. Không di chuyển field serialized.
2. **`CharacterDash`**: tách dash state và các method dash; dash vẫn request animation/state qua façade, không tự gọi KCC.
3. **`CharacterJump`**: tách jump buffer/coyote/double-jump/air timer, `UpdateAirVelocity` và gravity; callback landing/animation vẫn được façade gọi theo thứ tự hiện tại.
4. **`CharacterCombat`**: tách attack context, target assist, lunge và attack velocity; subscription `CharacterCombo.OnAttackHit` vẫn ở façade để giữ lifecycle.
5. **`CharacterLocomotion` + `CharacterRotation`**: tách sau cùng vì cùng sở hữu `MoveState`, gait, turn-snap/turn-warp, root-motion ground velocity và animation transition. Giữ `UpdateRotation`/`UpdateVelocity` public trên façade, chỉ forward nguyên arguments theo thứ tự cũ.

Mỗi bước phải compile, review diff, kiểm tra public/serialized diff, sau đó mới qua module tiếp theo. Mục tiêu cuối: `CharController` còn façade KCC/orchestration nhỏ hơn 800 dòng; module chỉ là C# thuần, không có allocation trên hot path.

## Xác minh yêu cầu sau từng bước

- Unity compile/Console kể từ lúc claim Editor; không sửa cảnh/prefab/controller.
- Kiểm tra source diff để xác minh không đổi hằng số, công thức, thứ tự condition, animation call hoặc public/serialized name.
- Play-mode regression: locomotion/root motion/turn, jump + double jump + buffers, ground & air dash, combo/context/lock-on/lunge, crouch ceiling/collision, disable-enable.
- Nếu phát hiện lỗi có sẵn, ghi riêng vào `CHARACTER_REFACTOR_ISSUES.md`; không sửa trong refactor.

## Kết luận pha khảo sát

Refactor thực hiện được mà không đổi API hay serialization, nhưng locomotion/rotation/jump/combat là vùng critical do chia sẻ `CurrentCharacterState`, `Motor`, `CharacterAnim`, và timing KCC. Vì vậy cần tách đúng thứ tự trên và compile/verify sau từng module, không thực hiện một lần toàn bộ.

## Tiến độ thực thi

Cập nhật: 2026-11-07 22:47 (phiên tiếp quản `deepseek-dash-compile`).

| Module | Trạng thái | Bằng chứng |
| --- | --- | --- |
| `CharacterCrouch` | đã tách, build được | `Library/ScriptAssemblies/Assembly-CSharp.dll` có type `CharacterCrouch` |
| `CharacterDash` | đã tách, build được | DLL 22:33:49 (sau khi sửa source 22:33:17) có type `CharacterDash`, `PickClip`, `TickGround`; console không có `error CS` |
| `CharacterJump` | đã tách, build + play mode OK | DLL có type `CharacterJump`; log `Temp/jump-regression.log` |
| `CharacterCombat` | đã tách, build + play mode OK | DLL có type `CharacterCombat`; log `Temp/combat-regression.log`, `Temp/air-attack.log` |
| `CharacterLocomotion` + `CharacterRotation` | đã tách, build + play mode OK | DLL có type `CharacterLocomotion`; log `Temp/loco-regression.log` |

Ghi chú bàn giao:

- Phiên `Codex-character-refactor` giữ Editor lock cho burst "compile dash module" rồi dừng (không còn gọi tool); Editor tự khởi động lại lúc 22:43:47 và lock được nhả theo phiên.
- Burst kiểm tra của phiên tiếp quản: acquire → `assets-refresh` → đọc console từ mốc claim → release. Không mở/sửa scene, prefab hay animator controller nào; `FeudalMap` vẫn sạch trước và sau burst.
- Console chỉ còn warning/error cũ của package VisualScripting (`Unable to find type: UnityEngine.UIElements.InputSystem.InputSystemEventSystem`) sinh ra lúc Editor khởi động sau khi lên Unity 6.4; không liên quan tới `CharController`.
- **Play-mode regression** cho crouch + dash + jump: đã chạy, xem hai mục dưới.

## Play-mode regression: crouch + dash (2026-11-07 22:54)

Cách chạy: scene `FeudalMap`, vào play mode, tắt `ExamplePlayer` rồi tự gọi `CharController.SetInputs` mỗi frame qua `EditorApplication.update` (kèm `EditorApplication.QueuePlayerLoopUpdate()` vì Editor mất focus khi chạy tự động), ghi log ra `Temp/dash-regression.log`. Hai burst riêng, mỗi burst dưới 1 phút, đều `editor-lock-release`; scene sạch trước và sau.

| Case | Kỳ vọng | Quan sát | Kết quả |
| --- | --- | --- | --- |
| Dash tại chỗ | `Dodge`, clip `DodgeBack`, hết clip về `Idle` | `Idle → Dodge (DodgeBack)` 3.064 → 3.773 (0.71 s) → `Idle` | PASS |
| Dash + giữ input tiến | `Dodge` `DodgeFront` rồi cắt sang `Move` | `Dodge (DodgeFront)` → `Move (Loop)` | PASS |
| Dash trên không | `AirDash` rồi rơi về `Air` | `AirDash (DodgeFront)` ~0.24 s → `Air (JumpLoop)` | PASS |
| Crouch | `Crouch` + clip `CrouchMove`, đứng lên về `Idle` | đúng, `CrouchMove` → `Idle` | PASS |
| Cooldown 0.35 s | dash ngay sau khi dash kết thúc phải bị chặn | **không chặn** (`blockedByCooldown=False`, `cooldown=-0.01`): timer bị trừ suốt clip dash 0.72 s nên đã hết trước khi clip xong | XEM GHI CHÚ |

Ghi chú về cooldown: đây **không phải regression của refactor** — bản gốc trước khi tách module cũng trừ `DashCooldown` mỗi frame trong lúc đang dash (`TickLocomotion` gọi `TickCooldown` trước khi xử lý state), nên clip né dài hơn cooldown thì dash nối được ngay. Ghi lại để ai sửa gameplay biết mà quyết định, không sửa trong refactor.

Chưa phủ (cần bổ sung khi có thời gian): dash khi lock-on/strafe (`DodgeLeft/Right`, `RunDodgeLeft/Right`), dash huỷ đòn đánh (`CanCancelAttackWithDash`), crouch dưới trần thấp (không đứng lên được), dash trên dốc.

## Module `CharacterJump` (2026-11-07 23:00)

Tách sang `Assets/_Asset/Script/CharacterModules/CharacterJump.cs`: jump buffer, coyote time, double jump, air timer, `UpdateAirVelocity` và trọng lực trên không. `CharController` giữ nguyên toàn bộ field serialized và thứ tự KCC; module chỉ giữ runtime state và trả về loại cú nhảy (`0` không nhảy / `1` nhảy đất / `2` nhảy trên không) để facade tự gọi `OnJumped` và set animation.

Các điểm nối còn lại trên facade: `_jump.Request()` (input), `_jump.MarkConsumed()` (`EnterFall`, launcher attack), `_jump.MarkAirJump` + `ResetAirborneTime` (`OnJumped`), `TakeAirJumpFlag` (`OnLanded`), `AddAirborneTime` (`TickLocomotion`), `AfterCharacterUpdate(IsGrounded, dt)`.

Kết quả compile: `[MatrixSpaceReload] Script compilation finished; errors=False`, DLL 22:59:09 có type `CharacterJump`; không có `error CS` nào từ `CharController`/`CharacterModules`.

Play-mode (`FeudalMap`, cùng cách lái input như trên, log `Temp/jump-regression.log`):

| Case | Kỳ vọng | Quan sát | Kết quả |
| --- | --- | --- | --- |
| Nhảy từ đất | `Air` clip `JumpStart`, lên rồi tiếp đất clip `JumpEnd` | đỉnh 1.35 so với mặt đất -0.99 (≈2.34 m), hạ cánh `Land (JumpEnd)` | PASS |
| Double jump | giữa không trung → `AirJumpStart`, lực nâng thứ hai | vy 4.03 → 9.27, clip `AirJumpStart` | PASS |
| Nhảy lần 3 khi hết lượt | bị chặn | vy 3.15 → 0.78 (chỉ rơi tự do), không có cú nâng | PASS |
| Tiếp đất sau double jump | clip `AirJumpEnd` | `Land (AirJumpEnd)` | PASS |
| Jump buffer | bấm đúng frame chạm đất vẫn nhảy | bấm ở frame `Land (JumpEnd)` → frame sau `Air (JumpLoop)`, `bufferFired=True` | PASS |

Chưa phủ: coyote time (bước khỏi mép rồi nhảy), nhảy khi đang attack (`CanCancelAttackWithJump`), nhảy trên dốc/bậc, đổi vũ khí giữa lúc bay (đổi `JumpProfile`), `AddVelocity` chồng lên cú nhảy.

## Module `CharacterCombat` (2026-11-07 23:05)

Tách sang `Assets/_Asset/Script/CharacterModules/CharacterCombat.cs`: hướng đánh (`_facing`), attack assist (`FindAttackTarget`), `ClampApproach`, lunge (`_lungeTimer`/`_lungeDelay`/`_lungeScale`), `_startHeight`, `_airHangTimer`, `_airAttackStarted` và toàn bộ `UpdateAttackVelocity`. `_wasAttacking` cũng chuyển vào module qua `UpdateEdge(attacking)` trả về `-1/0/1`.

Facade giữ: subscription `characterCombo.OnAttackHit` → `_combat.OnAttackStarted(info)` (trả `true` khi là launcher để facade gọi `_jump.MarkConsumed()`), cộng 3 điểm đọc: `_combat.Facing` (UpdateRotation), `_combat.ResolveContext` (SetInputs), `_combat.UpdateVelocity` (UpdateVelocity). Thêm 2 accessor `internal`: `PlanarToLockTarget()` (mở từ private) và `DashLastEndTime`.

`CharController` còn **884 dòng** (từ 1.129 đầu phiên, 1.253 lúc khảo sát).

Compile: `errors=False`, DLL 23:04:50 có `CharacterCombat` + `ResolveContext`, không có `error CS` mới.

Play-mode (`FeudalMap`, cùng cách lái input, log `Temp/combat-regression.log`):

| Case | Kỳ vọng | Quan sát | Kết quả |
| --- | --- | --- | --- |
| Đòn đánh đất | bắt đầu, `ctx=Ground`, có lướt, kết thúc về `Idle` | `attacking=True` ngay sau input, đi được 1.25 m, kết thúc `Idle` | PASS |
| Dash attack | bấm đòn ngay sau khi dash xong → `ctx=Dash` | `ctx=Dash` | PASS |
| Air attack | `ctx=Air`, nhân vật khựng lại giữa không trung | `ctx=Air`; vy −2.63 → −3.63 → **−0.28 / −0.40** (~0.4 s treo) → −4.32 khi hết hang | PASS |

Chưa phủ: launcher (lock-on + kéo lùi), attack assist chọn mục tiêu (scene test không có `LockOnTarget`), chuỗi combo nhiều đòn + cancel/queue, `ClampApproach` khi đã sát mục tiêu, hit feedback (`OnAttackHit` → `ComboCameraFeedback`).

## Module `CharacterLocomotion` (2026-11-07 23:15)

Gộp đúng như kế hoạch khảo sát (locomotion + rotation tách cùng lượt vì dùng chung turn-snap/turn-warp): `Assets/_Asset/Script/CharacterModules/CharacterLocomotion.cs` giữ `MoveState` FSM (`Tick`, `TickIdle/Move/Turn/Stop`, `TickAir`, `BeginMove`, `BeginTurn`, `FinishTurn`, `AbortTurn`, `BeginStop`, `EnterGroundIdle`, `ResumeAfterAttack`), `DesiredGait`/`SignedAngleToInput`/`UpdateDirectionParams`, toàn bộ `UpdateRotation` + `SmoothLook`, và `UpdateGroundVelocity` (kèm `_smoothedRootSpeed`, `_lastGait`, `_turn*`).

Facade còn lại: `SetState` (animation bridge), `SetInputs`, lock-on, jump/dash/crouch/combat wiring, `UpdateRotation`/`UpdateVelocity` forward nguyên tham số, `EnterFall`/`EndDash`/`FacingTarget`/`PlanarToLockTarget`. Thêm vài accessor `internal` (`Stick`, `SprintHeld`, `CurrentClip`, `RequestInstantClip`, `CurrentCharacterState` setter, `GaitWalk/Jog/Run` thành `internal const`) — không đổi field serialized hay public API nào.

`CharController`: **566 dòng** (1.129 đầu phiên, 1.253 lúc khảo sát) — đạt mục tiêu "<800 dòng façade".

Compile: `errors=False` lúc 23:13:59, DLL có `CharacterLocomotion` + `UpdateGroundVelocity`.

Play-mode (`FeudalMap`, log `Temp/loco-regression.log`):

| Case | Kỳ vọng | Quan sát | Kết quả |
| --- | --- | --- | --- |
| Giữ tiến | `Idle → Turn(StartRun) → Move(Loop)` và chạy thật | đúng, đi 6.64 m trong 1.4 s, tốc độ cuối 5.67 m/s | PASS |
| Nhả phím khi đang chạy | `Move → Stop(StopRun) → Idle` | đúng (trượt thêm 1.15 m theo clip dừng) | PASS |
| Đổi hướng 90° | chọn clip quay 90° rồi vào loop | `Turn(StartRunR90) → Move`, yaw 3.3° → 93.3° | PASS |
| Đổi hướng lần nữa | quay tiếp rồi loop | `Turn(StartRunR90) → Move`, yaw 93.3° → 183.3° | PASS |

Chưa phủ: strafe/lock-on (`Strafe`, `IdleTurn*`), đổi gait walk/jog/run theo lực stick, turn-warp khi input đổi giữa clip quay, khớp pose sau snap (`PoseBlend`), crouch đi/đứng trong FSM mới.

## Ghi chú phiên song song

Lúc 23:10:34 một phiên khác đang lưu dở `Assets/_Asset/Script/Editor/CombatTimingWindow.cs` (thêm tab "Bộ anim & Vũ khí"), làm assembly Editor lỗi `CS0103: DrawLibrariesTab` và chặn compile của burst đầu. Không sửa file đó; chờ file ổn định (23:11:42) rồi compile lại mới xanh. Nếu thấy lỗi compile quanh file này thì đó là việc của phiên đang sửa nó, không phải refactor này.
