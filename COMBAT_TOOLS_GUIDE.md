# DKM — Hướng dẫn 5 tool Combat & Animation

Phạm vi tài liệu: **chỉ 5 tool được viết/viết lại cho dự án này**

1. `CombatTimingWindow` — cửa sổ chỉnh mốc + preview + build + kiểm tra
2. `CombatLibraries` — định nghĩa 2 asset dữ liệu (`AnimSetLibrary`, `WeaponLibrary`)
3. `LocomotionAnimatorBuilder` — dựng layer `Locomotion`
4. `WeaponComboBuilder` — dựng `WeaponData` + layer `WeaponAttack`
5. `LocoSetPrefixesGenerator` — sinh prefix cho runtime

Đọc mục **10. Cảnh báo** trước khi bấm bất kỳ nút Build nào.

---

## 0. TL;DR

- **Mốc đòn đánh** (`HitTimes`, `KickTimes`, `SlamTime`, `LinkTime`, `MoveCancelTime`) là dữ liệu runtime nằm trong `WeaponData` → sửa xong **không cần build lại**.
- Chỉ khi **clip / import / cấu trúc chuỗi** thay đổi mới phải build. Thứ tự bắt buộc: **build trước → tune mốc sau** (build ghi đè mốc).
- 2 nguồn dữ liệu:
  - `Assets/_Asset/Data/AnimSetLibrary.asset` — bộ anim (prefix + bảng clip theo `LocoState`)
  - `Assets/_Asset/Data/WeaponLibrary.asset` — danh sách vũ khí + chuỗi đòn
- 2 builder dựng Animator từ 2 asset trên: layer `Locomotion` và `WeaponAttack` trong `Dante 1.controller`.
- Cửa sổ làm việc chính: **`Tools/Character Studio`** (gộp Timing Window, Anim Set Clone, Weapon Grip Tool và bảng library vào 1 cửa sổ). Các menu cũ `Tools/Combat/Timing Window`, `Anim Set Clone`, `Weapon Grip Tool` đã bỏ; mục 4.x bên dưới mô tả các panel đó, giờ nằm trong các trang của Studio.

## 0.1 Character Studio — 1 cửa sổ set up nhân vật

Nhân vật = 1 bộ anim trong `AnimSetLibrary` (prefix + moveset + **Prefab**) + các vũ khí trong `WeaponLibrary` có `AnimSetId` trỏ về bộ đó. Chọn nhân vật ở ô trên cùng, làm lần lượt các bước ở cột trái; chấm xanh = xong, vàng/đỏ = còn thiếu.

| Trang | Làm gì | Nguồn |
|---|---|---|
| Tổng quan | Thẻ nhân vật, số liệu, checklist các bước, nhân bản / xoá nhân vật | — |
| Prefab | Gán prefab, kiểm tra component combat (CharacterCombo, WeaponHolder, AnimatorRootMotion, PoseBlend, CombatEffects, MeleeHitDetector / SpellCaster…), nút Thêm / Gán sửa thẳng vào prefab, gán WD_ vào `CharacterCombo.Weapons` | mới |
| Moveset | Bảng state theo nhóm (đứng, bắt đầu chạy, dừng, đổi hướng, nhảy, né, ngồi) + gait + strafe/crouch 8 hướng; ô chọn clip có tìm kiếm, chấm đỏ = clip không có trong thư mục. Tab *Tự map clip* = Anim Set Clone (ghi đè bộ đang chọn) | thay tab *Bộ anim* + AnimSetCloneWindow |
| Vũ khí & Combo | Thông tin vũ khí, chuỗi đòn (ngữ cảnh, thứ tự, các bước), ✨ gợi ý chuỗi từ tên clip (`attack01…`, `combo02_3…`), chọn clip phát có bản `_Inplace` thì tự điền cột root motion | thay tab *Vũ khí* |
| Mốc đánh | Timeline mốc — chỉ hiện vũ khí của nhân vật, preview bằng prefab của nhân vật | CombatTimingWindow tab 1 |
| Cầm vũ khí | Căn vũ khí trên tay theo vũ khí chọn ở thanh trên | WeaponGripTool |
| Âm thanh | Bộ SFX CombatEffects | CombatTimingWindow tab 2 |
| Build | Tình trạng + nút *Build tất cả cho nhân vật* (Sinh prefix → Build Locomotion → Build vũ khí CỦA NHÂN VẬT NÀY), từng bước riêng, Bake Slam+Kick, kiểm tra dữ liệu đòn, mục Nâng cao (build mọi vũ khí, preset Vergil/Trish, nhập library từ code) | thay tab *Build* |

**Nhân vật mới** (nút ＋ trên header): tên + prefix, khuôn (bộ có sẵn), thư mục anim, có giữ clip khuôn cho state thiếu không, vũ khí (Key, loại, model, thư mục đòn, gợi ý chuỗi). Có dòng *Xem trước* số clip map được trước khi bấm Tạo.

`AnimSetLibrary` / `WeaponLibrary` nằm ở `AnimSetLibrary.cs` / `WeaponLibrary.cs` (ScriptableObject phải ở file trùng tên class, nếu không asset mất script và không load được — lỗi cũ khiến `AnimSetLibrary.asset` có `m_Script: {fileID: 0}`).

---

## 1. Bản đồ file & asset

| Đường dẫn | Vai trò |
|---|---|
| `Assets/_Asset/Script/Editor/CombatTimingWindow.cs` | Cửa sổ chính: timeline, preview, âm thanh, bảng dữ liệu, build, kiểm tra |
| `Assets/_Asset/Script/Editor/CombatLibraries.cs` | Định nghĩa `AnimSetLibrary`, `WeaponLibrary` (Editor-only ScriptableObject) |
| `Assets/_Asset/Script/Editor/LocomotionAnimatorBuilder.cs` | Dựng layer `Locomotion`, chuẩn hoá import FBX, setup `Character.prefab` |
| `Assets/_Asset/Script/Editor/WeaponComboBuilder.cs` | Phân tích clip vũ khí, sinh `WeaponData`, dựng layer `WeaponAttack` |
| `Assets/_Asset/Script/Editor/LocoSetPrefixesGenerator.cs` | Sinh file prefix cho runtime |
| `Assets/_Asset/Data/AnimSetLibrary.asset` | Dữ liệu bộ anim (asset do 2 tool trên đọc/ghi) |
| `Assets/_Asset/Data/WeaponLibrary.asset` | Dữ liệu vũ khí |
| `Assets/_Asset/Data/Weapons/WD_*.asset` | `WeaponData` sinh ra từ builder (asset runtime) |
| `Assets/_Asset/Animation/Player/Dante 1.controller` | Animator Controller chứa 2 layer sinh tự động |
| `Assets/_Asset/Script/Contanst/LocoSetPrefixes.Generated.cs` | File **sinh tự động**, đừng sửa tay |

---

## 2. Kiến trúc dữ liệu

```
AnimSetLibrary ──► LocomotionAnimatorBuilder ──► layer "Locomotion" (state KT_/GS_/NV_…)
      │                                                 ▲
      │ Sinh prefix                                     │ CharacterAnim.Play(LocoState)
      ▼                                                 │
LocoSetPrefixes.Generated.cs ──► LocoStates.SetPrefixes─┘

WeaponLibrary ──► WeaponComboBuilder ──► WD_*.asset (WeaponData) + layer "WeaponAttack"
                                                │
                                                ├─ CharacterCombo: crossfade state, HitTimes, nối/huỷ
                                                ├─ MeleeHitDetector: quét lưỡi quanh HitTimes, SlamTime, KickTimes
                                                ├─ WeaponHolder: cách cầm kiếm theo GripPositions/Rotations
                                                └─ CharController: root motion (RootSamples), target assist
```

Nguyên tắc: **state trong Animator không có transition**. Mọi chuyển state do code gọi `CrossFade`, nên sửa tay trong Animator window sẽ bị build sau ghi đè.

---

## 3. Danh mục 5 tool

| Tool | Menu / cách chạy | Làm gì | Ghi ra đâu |
|---|---|---|---|
| **CombatTimingWindow** | `Tools/Combat/Timing Window` | Timeline kéo mốc, preview 3D + bù nhìn, SFX, bảng anim/vũ khí, build, validate | Asset `WeaponData` / 2 library, controller, prefab |
| **CombatLibraries** | Assets ▸ Create ▸ Combat ▸ *Anim Set / Weapon Library* | Định nghĩa kiểu dữ liệu cho 2 library | Asset `.asset` |
| **LocomotionAnimatorBuilder** | `Tools/Combat/Build Locomotion Animator` · `Tools/Combat/Import Anim Sets From Code` | Dựng layer `Locomotion`, chuẩn hoá import, gắn `AnimatorRootMotion` + `PoseBlend` | `Dante 1.controller`, `Character.prefab` |
| **WeaponComboBuilder** | `Tools/Combat/Build Weapon Combos` · `Tools/Combat/Bake Slam And Kick Times` · `Tools/Combat/Import Weapon Library From Code` | Phân tích clip (mũi kiếm, chân, root, grip, pose), sinh `WeaponData`, dựng layer `WeaponAttack` | `WD_*.asset`, `Dante 1.controller` |
| **LocoSetPrefixesGenerator** | `Tools/Combat/Generate Loco Set Prefixes` | Sinh `LocoSetPrefixes.Values` từ library | `LocoSetPrefixes.Generated.cs` |

---

## 4. Chi tiết từng tool

### 4.1 CombatTimingWindow — `Tools/Combat/Timing Window`

4 tab:

| Tab | Dùng để |
|---|---|
| **Mốc đánh** | Chỉnh mốc + preview nhân vật cầm vũ khí, có sàn và bù nhìn |
| **Âm thanh** | Gán bộ SFX vào `CombatEffects` trong scene |
| **Bộ anim & Vũ khí** | Sửa 2 library bằng bảng: nhân vật, vũ khí, chuỗi, bước |
| **Build** | Chạy các builder + kiểm tra dữ liệu |

**Tab Mốc đánh**

- Bố cục: trái = Vũ khí → Chuỗi → Đòn; giữa = preview; phải = thông số + mốc; dưới = timeline.
- Kéo **kim** để tua, kéo **mốc** để chỉnh, chuột phải = menu xoá, `Delete` = xoá (ưu tiên mốc đang trỏ → đang chọn → gần kim).
- `H`/`K`/`S` thêm mốc chém/đá/slam tại kim; `Space` phát/dừng.
- Mốc `nối` (`LinkTime`) và `huỷ` (`MoveCancelTime`) cũng kéo được; có nút `−` `+` `= kim` để nhích từng frame.
- Preview trong cửa sổ: kéo chuột = xoay, lăn = zoom, có `Đảo X` / `Đảo Y` và slider `Độ sáng đèn`.
- Nhân vật preview cầm **đúng vũ khí của `WeaponData`**, cách cầm lấy từ `GripPositions/Rotations`; bù nhìn sáng đỏ khi kim nằm trong cửa sổ quét.
- Kéo kim có thể phát tiếng (toggle `Tiếng khi kéo`), lấy clip từ `CombatEffects`.
- Nếu khung đen: chuyển `Scene view` trong ô Preview.

**Tab Âm thanh**: gán `SwingClips`, `HeavySwingClips`, `ThrustClips`, `HitClips`, `HeavyHitClips`, `SlamClips`, `KickSwingClips`, `KickHitClips` + volume/ngưỡng đòn nặng. Nút **Tự lấp** tìm clip theo tên file trong project.

**Tab Bộ anim & Vũ khí**: xem mục 8 (quy trình C).

**Tab Build**:
- `Build Locomotion Animator`
- `Build Weapon Combos` (ghi đè mốc — xem cảnh báo #1)
- `Bake Slam And Kick Times` (ghi đè slam/đá — xem cảnh báo #2)
- `Kiểm tra toàn bộ`: mốc chưa sort, ngoài 0..1, chồng cửa sổ quét, state thiếu trong Animator, lệch số prefix.

### 4.2 CombatLibraries

Hai ScriptableObject **chỉ tồn tại trong Editor** (script nằm ở thư mục `Editor/` → không dùng được ở runtime, đừng tham chiếu từ code chạy game).

`AnimSetDefinition`: `Prefix`, `DisplayName`, `MovementFolder`, `OneShots` (`State`, `Clip`, `Loop`, `Air`, `Speed`, `FirstFrame`), `Gaits` (`Threshold`, `Clip`), `Strafe`/`Crouch` (`X`, `Y`, `Clip`).

`WeaponDefinition`: `Type`, `Key`, `DisplayName`, `AnimFolder`, `Prefab` (vũ khí gốc để đo mũi kiếm), `Model` (vũ khí trong game), `IdleClip`, `ComboResetTime`, `AnimSetId`, `Chains` (mỗi chain có `Context` + `Steps`: `Clip`, `Root`, `Vertical`).

### 4.3 LocomotionAnimatorBuilder

- Xoá layer `Idle`, `Move`, `Jump`, `Dash`, `Locomotion` cũ; xoá hết parameter; thêm `Gait`, `Move_X`, `Move_Y`; dựng layer `Locomotion` (iKPass bật) với 1 state mỗi `LocoState` + 3 blend tree: `Loop` (1D theo Gait), `Strafe` (2D 8 hướng), `CrouchMove` (2D).
- Chuẩn hoá import từng clip: khoá xoay root, bake độ cao vào pose, chỉ để XZ làm root motion, `loopTime` theo cờ `Loop`, `firstFrame` nếu khai báo.
- Gắn `AnimatorRootMotion` + `PoseBlend` vào `Character.prefab`, bật `applyRootMotion`.
- Lỗi clip: log `Không thấy clip '…'` và **bỏ state đó** (build vẫn chạy) — phải đọc Console.
- **Import Anim Sets From Code**: tạo `AnimSetLibrary` từ bảng hard-code cũ (chỉ dùng 1 lần để migrate).

### 4.4 LocoSetPrefixesGenerator

Sinh `Assets/_Asset/Script/Contanst/LocoSetPrefixes.Generated.cs`:

```csharp
public static class LocoSetPrefixes
{
    public static readonly string[] Values = { "KT_", "GS_", "NV_" };
}
```

Runtime dùng danh sách này để ghép hash state (`KT_Idle`) và kiểm tra `WeaponData.AnimSetId`.
**Thứ tự prefix = thứ tự bộ trong `AnimSetLibrary`** — đây là hợp đồng giữa asset và runtime.

### 4.5 WeaponComboBuilder

Phân tích mỗi clip bằng cách phát thử trên model gốc của pack anim (có xương `ik_hand_gun`):

| Dữ liệu | Cách tính |
|---|---|
| `HitTimes` | Đỉnh tốc độ mũi kiếm (gộp đỉnh cách nhau < 0.15s) |
| `LinkTime` | Sau nhát cuối, khi mũi kiếm chậm < 30% tốc độ đỉnh |
| `MoveCancelTime` | Khi mũi kiếm gần như đứng hẳn (< 12%), luôn ≥ `LinkTime + 0.05` |
| `SlamTime` | Mũi kiếm lao xuống nhanh, xuống thấp, rồi khựng lại |
| `KickTimes` | Bàn chân đạt đỉnh tốc độ, nhấc cao, duỗi xa so với hông |
| `RootSamples` | Hông bản root motion trừ hông bản Inplace (không đọc RootT/RootQ) |
| `GripPositions/Rotations` | Xương `ik_hand_gun` quy về bàn tay Dante, lấy mẫu 30/s |
| `PoseSamples` | Tay/chân/đầu theo hông, dùng để nối đòn khớp tư thế |
| `NextSeamTime` | Frame trong đòn trước trùng tư thế frame đầu đòn sau |
| `Weight` | Chuẩn hoá tốc độ đỉnh theo toàn vũ khí (0..1) |

`Bake Slam And Kick Times`: chỉ tính lại `SlamTime` + `KickTimes`, **không** đụng `HitTimes`, không đụng Animator.

Giữ tay: `Speed`, `FadeIn`, `RootMotionScale` được giữ qua `StateName` khi build lại.

---

## 5. Trường dữ liệu quan trọng trong `WeaponData` / `AttackStep`

| Trường | Ý nghĩa | Ai đọc |
|---|---|---|
| `Clip` | Clip phát trên Animator (bản Inplace/ZeroHeight) | `CharacterCombo` |
| `StateName` | Tên state trong layer `WeaponAttack` | `CharacterCombo` |
| `Speed`, `FadeIn` | Tốc độ phát, thời gian crossfade vào đòn | `CharacterCombo` |
| `HitTimes` | Mốc lưỡi kiếm chém trúng (normalized) | `MeleeHitDetector`, `CharacterCombo` |
| `KickTimes` | Mốc chân đá mạnh nhất | `MeleeHitDetector` |
| `SlamTime` | Mốc kiếm đập đất | `MeleeHitDetector` (tiếng + rung + tia lửa) |
| `LinkTime` | Từ đây bấm sẵn thì ra đòn kế | `CharacterCombo.LinkPoint` |
| `MoveCancelTime` | Từ đây đẩy cần thì cắt animation để chạy (hiệu dụng = `max(value, MoveCancelMinTime 0.75)`) | `CharController` |
| `NextSeamTime` | Frame nối liền mạch với đòn sau (giây) | `CharacterCombo.LinkPoint` |
| `UsePlanar/VerticalRootMotion`, `RootMotionScale` | Bật/tắt root motion, tỉ lệ | `CharController` |
| `RootSamples`, `RootTravel` | Đường dời gốc đã bake | `CharacterCombo`, `CharController` |
| `GripPositions/Rotations` | Cách cầm kiếm theo anim | `WeaponHolder` |
| `PoseSamples` | Tư thế để nối đòn | `CharacterCombo.MatchPose` |
| `Weight`, `IsFinisher` | Độ nặng đòn, đòn chốt | `ComboCameraFeedback` |
| `WeaponData.ComboResetTime` | Thời gian giữ chuỗi sau khi đòn kết thúc | `CharacterCombo` |
| `WeaponData.AnimSetId` | Index bộ anim trong `AnimSetLibrary` | `CharController` → `CharacterAnim.SetWeapon` |

---

## 6. Quy trình A — chỉnh mốc cho vũ khí đã có

1. `Tools/Character Studio` → chọn nhân vật → trang **Mốc đánh**.
2. Chọn vũ khí → chuỗi → đòn ở cột trái.
3. Bật `▶` hoặc `Space` để xem; kéo kim, nghe tiếng, xem bù nhìn sáng đỏ.
4. Chỉnh bằng:
   - `H`/`K`/`S` thêm mốc tại kim, `Delete` xoá, chuột phải = menu.
   - Kéo mốc, hoặc dùng ô số giây bên phải.
   - Kéo vạch **vàng nối** / **xám huỷ**, hoặc dùng `−` `+` `= kim`.
5. Tab **Build** → **Kiểm tra toàn bộ**, sửa hết mục ✖ và ⚠.
6. Play test (mục 11).
7. Commit `WD_*.asset`.

> Không cần build lại Animator khi chỉ đổi mốc.

---

## 7. Quy trình B — thêm vũ khí mới

1. Chuẩn bị: prefab vũ khí gốc của pack anim (có xương để đo mũi kiếm), prefab model dùng trong game, thư mục clip FBX.
2. Tab **Bộ anim & Vũ khí** → mục WeaponLibrary → `＋ Thêm vũ khí`.
3. Điền `Key` (tiền tố state, vd `Yamato`), `DisplayName`, `AnimFolder`, `Prefab`, `Model`, `IdleClip`, `ComboResetTime`, **`AnimSetId`** (bộ anim vũ khí này dùng).
4. Thêm chuỗi: `＋ Chuỗi`, đặt `Name` + `Context` (`Ground`/`Dash`/`Air`/`Launcher`/`Skill`).
5. Thêm bước: `＋ Bước`, chọn `Clip` (bản Inplace/ZeroHeight) và `Root` (bản có di chuyển), tick `Y` cho đòn hất tung.
6. `Build Weapon Combos` → sinh `WD_<Key>.asset` + state trong layer `WeaponAttack`.
7. Gán `WeaponData` mới vào `CharacterCombo.Weapons` trong prefab/scene.
8. Gán SFX (tab **Âm thanh**), tune mốc (quy trình A), play test.

---

## 8. Quy trình C — setup nhân vật mới từ đầu

### Bước 0 — Chuẩn bị

- Model FBX **Humanoid** (avatar đúng), bộ clip movement đặt tên rõ ràng.
- Builder đọc clip **theo tên** trong thư mục → tên phải khớp chính xác.
- **Commit/backup trước khi làm** — các bước build sau ghi đè controller và asset.
- Đóng Play mode; nếu dùng chung Editor thì chỉ một người chạy build.

### Bước 1 — Chuẩn hoá anim

Import FBX ở chế độ Humanoid, bật `Loop Time` cho clip loop, `Bake Into Pose` cho root nếu cần (phần này làm bằng import settings của Unity, không thuộc 5 tool trên). Nếu pose bị lệch, sửa clip trước rồi mới sang bước 2.

### Bước 2 — Khai báo bộ anim

1. `Tools/Character Studio` → **＋ Nhân vật mới** (hoặc trang **Moveset** của nhân vật đang có).
2. Mục AnimSetLibrary: nếu chưa có asset → `Tạo / ghi đè từ code` (một lần) rồi sửa tiếp; nếu đã có → chọn asset.
3. `＋ Thêm bộ anim`, điền:
   - `Prefix`: vd `NV_` (phải **duy nhất**, không trùng `KT_`, `GS_`).
   - `DisplayName`, `MovementFolder` (thư mục clip movement).
4. `＋ State` cho từng `LocoState` cần có: `Idle`, các `IdleTurn*`, `Start*`, `Stop*`, `Turn*`, `Jump*`, `AirJump*`, `Dodge*`, `CrouchEnter`; tick `Loop`/`Air`, đặt `Speed`, `FirstFrame` nếu cần.
5. Khai báo `Gaits` (walk/jog/run/run fast), `Strafe` (8 hướng đi + 8 hướng chạy), `Crouch` (9 hướng). Ô clip có nút `▾` liệt kê clip thật trong thư mục.
6. Bấm **Sinh prefix** → `LocoSetPrefixes.Generated.cs` được ghi lại → Unity compile.
7. Bấm **Build Locomotion** → dựng layer + chuẩn hoá import + setup prefab.

### Bước 3 — Khai báo vũ khí

1. Mục WeaponLibrary: tạo/ghi đè asset nếu chưa có.
2. `＋ Thêm vũ khí`, điền như quy trình B. **`AnimSetId` phải bằng index của bộ anim ở Bước 2** (bộ đầu = 0).
3. `Build Weapon Combos`.

### Bước 4 — Lắp nhân vật vào scene

Trên prefab nhân vật (Animator ở root):

| Component | Cấu hình |
|---|---|
| `Animator` | Avatar Humanoid, Controller = `Dante 1.controller`, `applyRootMotion = true` |
| `AnimatorRootMotion`, `PoseBlend` | Builder tự gắn; nếu thiếu thì build lại Locomotion |
| `CharacterAnim` | `animator`, `towardMove`, fade |
| `CharacterCombo` | `animator`, `Weapons` = các `WD_*`, `AttackLayerName = WeaponAttack` |
| `WeaponHolder` | `animator`, `characterCombo` |
| `MeleeHitDetector` | `characterCombo`, `weaponHolder`, `cameraFeedback`, `effects`, `BladeTrails` |
| `CombatEffects` | `Source` + các bộ clip (tab Âm thanh) |
| `CharController` + KCC motor | Theo rig hiện có |
| `PlayerCharacterInputs`/`ExamplePlayer` | Điều khiển (nếu là player) |

Trong scene: kẻ địch/bù nhìn cần `HitReaction` để nhận đòn.

### Bước 5 — Tune & kiểm tra

1. Tab **Mốc đánh**: chọn vũ khí/đòn, kiểm tra preview có nhân vật + kiếm + bù nhìn.
2. Chỉnh `H/K/S`, `nối`, `huỷ`; nghe tiếng; xem bù nhìn sáng.
3. Tab **Build** → **Kiểm tra toàn bộ**.
4. Play test (mục 11), bật `MeleeHitDetector.LogHits`.
5. Commit: library asset, `WD_*.asset`, controller, prefab, file prefix sinh ra.

### Vergil & Trish — làm sẵn bằng `CharacterSetupTool`

Menu `Tools/Combat/Nhân vật/…` (bảng clip + chuỗi combo nằm trong `Editor/CharacterSetupTool.cs`) chạy trọn Bước 1–3 cho 2 nhân vật:

| | Vergil (`VG_`, `WD_Yamato`) | Trish (`TR_`, `WD_TrishMagic`) |
|---|---|---|
| Pack | `_Animation_Vergil/Animation/Humanoid` | `_Animation_Trish/Animations/Humanoid` |
| Đất | `attack01-03` + 8 chuỗi `combo01..08` | `GroundAtk01-03`, `04-06`, chuỗi rush (bản short) |
| Dash / Air / Launcher | `combo02_1` (Rapid Slash) / `attack_jumpATK` / — | `GroundAtk01_short` / `FlyingAtk01-06` + bản short / `GroundAtk03_short` |
| Skill | `combo08_3` (Judgement Cut), cất–rút kiếm | `buff_Ground` (nổ quanh người) |
| Di chuyển | idle, walk/run 8 hướng, sprint, avoid, roll | idle, walk/run 8 hướng, nhảy = bay lên lơ lửng; dodge/crouch/turn dùng clip Dante |

- Tool sửa luôn đường dẫn pack cũ (`Assets/Dante/`, `Assets/Sword_sheath_AnimSet/`) trong 2 library và chỉ phân tích lại vũ khí của nhân vật đang setup → mốc chỉnh tay của `WD_Rebellion`/`WD_Sparda` được giữ.
- Trish là `WeaponType.Magic`: không có model vũ khí, `MeleeHitDetector` không quét. Gắn thêm `Combat/SpellCaster` (cùng GameObject với `CharacterCombo`): mỗi mốc `HitTimes` bắn 1 đạn phép từ tay đang vung ra trước, đòn chốt nổ lan, Skill nổ quanh người. `CharacterCombat` không lướt áp sát khi dùng vũ khí `Magic`.

---

## 9. Quy trình D — đổi hoặc làm lại clip

Khi clip thay đổi (retime, thay FBX, sửa pose), mọi mốc đã bake **không còn đúng**:

1. Sửa clip/import.
2. `Build Weapon Combos` (phân tích lại toàn bộ) — **ghi đè `HitTimes`/`KickTimes`/`SlamTime`**.
3. `Build Locomotion` nếu clip movement đổi.
4. Vào tab **Mốc đánh** tune lại tay.
5. Kiểm tra + play test.

`Speed` / `FadeIn` / `RootMotionScale` chỉnh tay được giữ lại theo `StateName`; các mốc thì không.

---

## 10. Cảnh báo & nguy cơ

| # | Nguy cơ | Mức | Cách tránh |
|---|---|---|---|
| 1 | `Build Weapon Combos` **ghi đè** `HitTimes`, `KickTimes`, `SlamTime` | Cao | Tune mốc **sau** khi build; muốn giữ mốc tay thì đừng build lại |
| 2 | `Bake Slam And Kick Times` ghi đè `SlamTime` + `KickTimes` (giữ `HitTimes`) | Trung bình | Chỉ chạy khi 2 loại mốc đó chưa chỉnh tay |
| 3 | Build Locomotion/Weapon **xoá và dựng lại layer** trong `Dante 1.controller` | Cao | Mọi chỉnh tay trong Animator window sẽ mất; commit trước, không sửa tay layer sinh tự động |
| 4 | Builder `SaveAndReimport` FBX → Editor đứng vài phút | Trung bình | Không ngắt giữa chừng, không build khi đang Play mode |
| 5 | Nhiều người/AI dùng chung một Editor | Cao | Chỉ chạy build khi không ai đang sửa scene/controller; dùng lock của MCP nếu có |
| 6 | Thêm bộ anim mà quên **Sinh prefix** | Cao | Runtime báo `AnimSetId không hợp lệ`, state không tồn tại. Sinh prefix rồi build lại |
| 7 | Sửa tay `LocoSetPrefixes.Generated.cs` | Trung bình | File sinh tự động, sẽ bị ghi đè; sửa trong `AnimSetLibrary` |
| 8 | Đổi tên/di chuyển thư mục clip hoặc đổi tên clip | Cao | Builder không tìm thấy clip → log error, state bị **bỏ qua âm thầm** |
| 9 | `Key` vũ khí hoặc `Prefix` trùng nhau | Cao | Trùng `StateName`/asset `WD_<Key>` → state đè nhau, dữ liệu lẫn |
| 10 | Đổi độ dài clip sau khi tune mốc | Cao | Mốc là normalized → lệch hết, phải tune lại |
| 11 | `MoveCancelTime` đặt < 0.75 | Thấp | Trong game vẫn chờ 0.75 (`MoveCancelMinTime`) — không phải bug |
| 12 | 2 mốc chém gần nhau hơn `WindowBefore + WindowAfter` (0.12 + 0.08) | Trung bình | Cửa sổ quét chồng nhau, mốc sau bị rút ngắn; giãn mốc hoặc giảm cửa sổ trên `MeleeHitDetector` |
| 13 | `HitTimes` không sort | Cao | `CharacterCombo` chỉ tiến một chiều → mốc lộn không bao giờ bắn. Tool tự sort khi thêm/kéo; nhập tay thì phải kiểm tra |
| 14 | `AnimSetLibrary`/`WeaponLibrary` là **Editor-only** | Trung bình | Không tham chiếu từ code runtime/build; runtime chỉ đọc `WeaponData` + file prefix sinh ra |
| 15 | `WeaponData.AnimSetId` sai index | Cao | Nhân vật dùng nhầm bộ anim/không có state. Kiểm tra sau mỗi lần build |
| 16 | Preview trong cửa sổ bị đen ở HDRP | Thấp | Chuyển `Scene view` trong ô Preview; hoặc tăng `Độ sáng đèn` |
| 17 | Xoá/đổi `Character.prefab` | Trung bình | Builder gắn lại `AnimatorRootMotion` + `PoseBlend`; mất Animator thì build báo lỗi |

---

## 11. Checklist kiểm tra (trước & sau)

**Trước khi build**
- [ ] Đã commit/backup.
- [ ] Không ở Play mode, không ai khác đang sửa controller/scene.
- [ ] Thư mục clip + tên clip khớp bảng trong library.
- [ ] Prefix/Key không trùng.
- [ ] Nếu thêm bộ anim: đã **Sinh prefix**.

**Sau khi build**
- [ ] Console không có error `Không thấy clip`, `thiếu layer`.
- [ ] Tab Build → Kiểm tra toàn bộ: không còn ✖.
- [ ] `WD_*.asset` có `AnimSetId` đúng.
- [ ] Preview hiển thị đúng vũ khí + pose.

**Play test**
- [ ] Bật `MeleeHitDetector.LogHits`, đánh trúng bù nhìn → có log đúng frame.
- [ ] Chém vào không khí: **không** rung/hitstop.
- [ ] Trúng thật: có tiếng impact + rung + hitstop.
- [ ] Bấm đánh liên tục: ra đòn kế đúng nhịp (mốc `nối`).
- [ ] Đẩy cần giữa đòn: cắt được để chạy (sau mốc `huỷ` hiệu dụng 0.75).
- [ ] Đổi vũ khí: animation đổi bộ đúng (`AnimSetId`).

---

## 12. Troubleshooting

| Triệu chứng | Nguyên nhân thường gặp | Xử lý |
|---|---|---|
| Không thấy state/nhân vật không chạy anim | Chưa build layer, hoặc prefix lệch | Sinh prefix → Build Locomotion/Weapon |
| `AnimSetId không hợp lệ` | Thêm bộ anim chưa sinh prefix, hoặc `AnimSetId` > số bộ | Sinh prefix, sửa `AnimSetId` trong WeaponLibrary, build lại |
| Đòn không có va chạm | `HitTimes` rỗng hoặc chưa sort | Tab Mốc đánh kiểm tra + sort |
| Rung/hitstop cả khi chém không khí | Không phải lỗi tool: mốc slam/đá đang bắn | Xem lại `SlamTime`/`KickTimes` |
| Preview đen | HDRP chặn camera preview | Chuyển `Scene view`, hoặc tăng độ sáng đèn |
| Preview không có kiếm | `WeaponData.WeaponPrefab` trống | Gán model trong WeaponLibrary rồi build lại |
| Build rất lâu | `SaveAndReimport` nhiều FBX | Bình thường; chờ, không ngắt |
| Kéo mốc không ăn | Cửa sổ mất focus / sai tab | Click vào timeline; kiểm tra tab **Mốc đánh** |
| Phím tắt không ăn | Đang gõ trong ô số | Bấm ra ngoài rồi thử lại |

---

## 13. Phím tắt & menu nhanh (chỉ 5 tool trên)

| Phím | Việc |
|---|---|
| `H` / `K` / `S` | Thêm mốc chém / đá / slam tại kim |
| `Delete` / `Backspace` | Xoá mốc (đang trỏ → đang chọn → gần kim) |
| `Space` | Phát / dừng |
| Chuột phải timeline | Menu: đặt tại kim, xoá mốc, xoá tất cả |
| `Alt + click` mốc | Xoá nhanh |
| Kéo chuột trong preview | Xoay; lăn = zoom |

| Menu | Việc |
|---|---|
| `Tools/Character Studio` | Cửa sổ chính (gộp mọi tool) |
| `Tools/Combat/Build Locomotion Animator` | Dựng layer Locomotion |
| `Tools/Combat/Build Weapon Combos` | Dựng layer WeaponAttack + `WeaponData` |
| `Tools/Combat/Bake Slam And Kick Times` | Chỉ bake slam + đá |
| `Tools/Combat/Import Anim Sets From Code` | Tạo AnimSetLibrary từ bảng cũ (1 lần) |
| `Tools/Combat/Import Weapon Library From Code` | Tạo WeaponLibrary từ bảng cũ (1 lần) |
| `Tools/Combat/Generate Loco Set Prefixes` | Sinh file prefix |
