# Phác thảo: phong cách quyền cước + combo tay không (DRAFT, chưa chốt)

Ngày: 2026-11-24 | Trạng thái: **Draft** - chưa có entry trong `decisions.md`, chưa ai duyệt.
Nguồn clip: `gamedesign/research/martial-arts-anim-inventory.md` (110 clip trong `Assets/_Asset/Animation/MartialArts/`).
Mọi số timing dưới đây là **giá trị khởi điểm để tune**. Mốc trúng là **ước lượng thô** từ muscle curve (lúc cẳng tay duỗi nhanh nhất / đùi nâng cao nhất),
chưa xem clip trong Editor. Chỉnh lại bằng `Tools/Character Studio` > Mốc đánh.

Viết tắt đường dẫn: `N/` = `Assets/_Asset/Animation/MartialArts/Naraka/`, `UF/` = `.../MartialArts/UltimateFighting/`.
Tên clip Naraka bỏ tiền tố `male_punch_` cho gọn (vd `attack_light_01` = `N/Light_Chain/male_punch_attack_light_01.anim`).

---

## 1. Ai dùng bộ quyền này? (cần quyết)

Hiện chưa có quyết định nào về đánh tay không. Ba hướng:

| Phương án | Mô tả | Được | Mất |
|---|---|---|---|
| **A (đề xuất)** | Vũ khí thứ 3 của Dante: "Quyền" (kiểu găng Beowulf của DMC) | Khớp D-002 (đổi vũ khí giữa combo là một kiểu cancel, tăng "đa dạng" cho Style). Dante đã có pipeline `WeaponData`. Không cần thanh tài nguyên mới | Thêm phạm vi cho Dante trong khi D-005 muốn Dante "lên chuẩn" trước; cần 3 skill Y + launcher riêng |
| B | Nhân vật thứ 4 thuần quyền cước | Bản sắc rõ nhất, dùng được parry tay không làm nút B | Ngược D-005 (thứ tự sản xuất), cần thanh tài nguyên, HUD, nhạc riêng (D-001, D-006, D-011) |
| C | Chỉ dùng cho địch (Oni / lính tay không) | Rẻ | D-012 đã chốt Oni là kiếm khách; clip HitReact vẫn dùng được cho địch Humanoid (Hell Executioner) |

Phần còn lại viết theo **phương án A**, chỗ nào khác nếu chọn B thì ghi chú.

## 2. Phong cách: "Lưu Quyền" - quyền lướt, chân tay xen kẽ

- **Fantasy**: võ Trung Hoa tốc độ cao, mỗi đòn kéo thân đi xa (đặc trưng clip Naraka: đòn nhẹ lướt 1.8-3.2 m, đòn nặng 2.8-6.3 m, finisher 9 m).
  Người chơi không đứng tại chỗ đấm mà "trôi" qua đám địch.
- **Nhịp**: chuỗi nhẹ 3 nhát nhanh -> dừng nhịp rẽ sang chuỗi nặng có gối/đá -> gồng lực để lao xa -> hất lên và đập xuống.
- **Khác Big Sword / Katana của Dante**: tầm ngắn nhất (cần áp sát), nhanh nhất khi nối từ né (`dodge_attack_light_01-03`, thiểm bộ 0.9s), mạnh nhất ở đòn gồng.
- **Điểm yếu có chủ đích**: tầm với ngắn; đòn lướt xa dễ trượt qua mục tiêu nếu không lock-on; `_recover` dài nếu không nối tiếp.

## 3. Input (theo D-001, không thêm nút)

D-001 chốt: X đánh thường, Y + hướng ra 3 skill, B cơ chế riêng, RT né, RB lock-on, A nhảy. **Không có nút Heavy hay Kick riêng.**
Đề bài gợi ý light/heavy/kick là 3 nút: làm vậy sẽ phá D-001, nên ở đây "heavy" là nhánh dừng nhịp của X, còn "kick" nằm sẵn trong clip.

| Input | Hành động | Clip |
|---|---|---|
| X X X | Chuỗi nhẹ (C1) | `attack_light_01/02/03` |
| X, dừng nhịp, X X X | Rẽ chuỗi nặng (C2) | `attack_heavy_01/02/03` |
| Giữ X (>= 0.35s) rồi thả | Đòn gồng (C5) | `attack_light_01_charge` -> `attack_hold_light_02` |
| Lock + tới + X | Lao tới (gap-closer) | `flashstep_attack_light_01` |
| Lock + lùi + X | **Launcher** (C3) | `crouch_attack_heavy_02` |
| Đang sprint + X | Đòn lao | `sprint_attack_light_01` |
| Trên không X X, X giữ | Air combo, đập xuống | `jump_attack_light_01` x2, `jump_attack_heavy_01` |
| RT (né) rồi X | Đòn sau né (C4) | `dodge_attack_light_01/02/03` |
| RT đúng nhịp (né hoàn hảo) rồi X | Counter | `perfect_dodge_back_01` -> `perfect_dodge_attack_light_01` |
| Y + đứng yên / tới / lùi | 3 skill | xem mục 5 |
| B | Taunt của Dante (D-002) | thiếu clip taunt tay không, tạm `N/Move/male_punch_emptystep_back_01` |

Nếu chọn phương án B (nhân vật riêng): B = **Parry tay không** `N/Dodge_Parry/male_punch_attack_shock_light_01` (gạt rồi lao 5.7 m phản đòn).

## 4. Các chuỗi combo

Quy ước cột (đơn vị **giây**; `AttackStep` trong DKM lưu normalized = giây / độ dài clip): **Hit** = mốc trúng ước lượng, **Link** = mốc nối đòn kế (giây), **Hủy né** = sớm nhất được RT hủy.
`_pre` (0.2s) là vào tay; có thể ghép vào bước hoặc bỏ để nhanh hơn (xem mục 6).

### C1 - Liên Hoàn Quyền: X X X (chuỗi cơ bản, 3 nhát)

| Bước | Clip | Dài | Hit | Link | Hủy né | Lướt | Ghi chú |
|---|---|---|---|---|---|---|---|
| 1 | `attack_light_01` (bỏ `_pre`) | 0.80 | 0.13 | 0.35 | 0.18 | 1.8 m | đòn mở nhanh nhất |
| 2 | `attack_light_02_pre` + `attack_light_02` | 0.20 + 1.03 | 0.17, 0.37 | 0.55 | sau hit 2 | 3.2 m | 2 nhịp tay |
| 3 | `attack_light_03` | 1.03 | 0.55 | 0.80 | 0.65 | 3.0 m | kết chuỗi, `Weight` 0.6 |
| kết | `attack_light_03_recover` | dùng 0.5s đầu | - | - | ngay | - | cắt clip hoặc `MoveCancelTime` |

### C2 - Trọng Cước: X, (dừng nhịp 0.25-0.6s), X X X

Rẽ từ bước 1 của C1 (theo quy tắc "dừng nhịp rẽ một nhánh cố định" trong `spec/input.md`).

| Bước | Clip | Dài | Hit | Link | Lướt | Ghi chú |
|---|---|---|---|---|---|---|
| 1 | `attack_light_01` | 0.80 | 0.13 | - | 1.8 m | như C1 |
| 2 | `attack_heavy_01_pre` + `attack_heavy_01` | 0.20 + 0.63 | 0.20 | 0.40 | 2.8 m | đoán có gối phải |
| 3 | `attack_heavy_02_pre` + `attack_heavy_02` | 0.20 + 0.80 | 0.27 | 0.50 | 4.1 m | đoán gối/đạp trái |
| 4 | `attack_heavy_03` | 1.17 | 0.40 | 0.85 | 6.3 m | đá xoay (đoán), **finisher chuỗi**, `IsFinisher` |
| kết | `attack_heavy_03_recover` | 0.6s đầu | - | - | - | |

### C3 - Thăng Long + Không Trung: Lock + lùi + X, rồi A/X trên không

| Bước | Clip | Dài | Hit | Ghi chú |
|---|---|---|---|---|
| 1 | `crouch_attack_heavy_02_pre` + `crouch_attack_heavy_02` | 0.20 + 0.63 | 0.10-0.20 | bật cao (RootT.y +4.7): `UseVerticalRootMotion = true`, `RootMotionScale` Y khoảng 0.5 để không bay quá mục tiêu (lỗi nền #4 trong `spec/input.md`). Mục tiêu nhận `N/HitReact/male_hurt_fly_up_01` |
| 2 | `crouch_attack_heavy_02_recover` | 0.23 | - | kết thúc trên không, cửa nối air 0.0-0.23 |
| 3-4 | `jump_attack_light_01` x2 (lần 2 bỏ `_pre`) | 0.63 | 0.10, 0.37 | gần như tại chỗ; giới hạn 2 lần mỗi lần bay |
| 5a | `jump_attack_heavy_01_pre` + `jump_attack_heavy_01` + `_recover` | 0.47 + 0.20 + 0.97 | 0.13 | đập mục tiêu xuống: mục tiêu `male_hurt_fly_land_back_01` -> `male_all_getup_back_battle` |
| 5b (thay 5a) | `shouji_attack_01` -> `shouji_attack_01_land` -> `_land_recover` | 0.63 + 0.67 + 1.47 | 0.17 (chân), land 0.10 | plunge lao xuống, có `SlamTime` khi chạm đất |
| rơi | `jump_down_loop_01` -> `jump_down_to_land_01` | 0.8 loop / 0.8 | - | |

Bản tiêu tài nguyên (nếu cần launcher mạnh hơn): `slide_attack_heavy_soul_01` (lướt 6.7 m rồi bật +4.9).

### C4 - Thiểm Bộ Phản Kích: RT, X, X, X (dodge-cancel)

| Bước | Clip | Dài | Hit | Link | Ghi chú |
|---|---|---|---|---|---|
| né | `N/Dodge_Parry/male_punch_flashstep_{front,back,left,right}_01` | 0.90 | - | X từ 0.30 | i-frame 0.05-0.35 (khởi điểm) |
| 1 | `dodge_attack_light_01` | 1.03 | 0.07 (chân), 0.53 | 0.65 | lướt 5 m |
| 2 | `dodge_attack_light_02` | 0.87 | 0.07-0.23 | 0.45 | lướt 4 m |
| 3 | `dodge_attack_light_03` | 0.83 | 0.40 | 0.70 | lướt 5.8 m, kết |

Counter (né hoàn hảo): `perfect_dodge_back_01` (cắt còn ~0.5s, phần sau là bullet-time của game gốc) -> X: `perfect_dodge_attack_light_01` (bật lên, đá ngang - đoán) -> `_recover` 0.5s đầu.
Gap-closer: Lock + tới + X = `flashstep_attack_light_01` (0.63s, hit ~0.10, lướt 4.2 m).

### C5 - Tụ Khí: giữ X / Y + hướng

| Input | Clip | Dài | Hit | Ghi chú |
|---|---|---|---|---|
| giữ X | `N/Charged/male_punch_attack_light_01_charge` (loop) | 5.0 loop | - | gồng tối thiểu 0.35s, tối đa 1.2s |
| thả (cấp 1) | `attack_hold_light_02` -> `_recover` | 1.50 | 0.33-0.63 (có đá 0.63) | lao 7.5 m |
| thả (cấp 2, giữ >= 0.9s) | `attack_hold_light_03` -> `_recover` | 2.33 | 0.17 (chân), 0.47, 0.60, 0.77 | nhiều nhịp, lướt 3.5 m |

### Skill Y + hướng (theo vai trò Dante đã chốt trong `spec/character-dante.md`)

| Hướng | Vai trò (D-002) | Clip | Dài | Hit | Ghi chú |
|---|---|---|---|---|---|
| Đứng yên | quét vùng / đám đông | `N/Finisher/male_punch_attack_hold_light_soul_03` | 2.00 | 0.47 ... 1.40 (nhiều nhịp) | rush tại chỗ |
| Tới | lao xa áp sát | `N/Charged/male_punch_attack_hold_heavy_01` | 1.50 | 0.37, 0.50 (chân), 0.63 | lướt 6.1 m, phá thủ |
| Lùi | đòn chốt nặng (tiêu Style ở SSS) | `N/Finisher/male_punch_attack_heavy_soul_03` | 1.40 | 0.30-0.37 (đá), 0.87 | lướt 9.25 m -> `RootMotionScale` 0.4-0.5 hoặc dừng theo `AttackStopDistance` |

## 5. Hủy đòn (giả định)

- **Né (RT)**: hủy mọi bước C1/C4 từ sau mốc hit + 0.05s; bước nặng (C2 bước 2-4, skill) chỉ sau `LinkTime`. Né thật, không chỉ hủy đòn (lỗi nền #1).
- **Đổi vũ khí** (D-002): ở `LinkTime` của mọi bước như nối đòn; đòn kế mở chuỗi của vũ khí mới.
- **Nhảy (A)**: sau launcher C3 và trong `crouch_attack_heavy_02_recover`; không nhảy-hủy giữa chuỗi đất.
- **Di chuyển**: `CharacterCombo.MoveCancelMinTime = 0.75` (normalized) ép mốc hủy-di-chuyển không sớm hơn 75% clip. Với clip có `_recover` tách riêng thì ổn (hủy trong `_recover`); nếu ghép main + recover thành một clip thì 75% sẽ quá muộn. Cần quyết khi dựng `WeaponData`.
- **Buffer**: 0.15-0.20s trước `LinkTime` (theo `spec/input.md`).
- Finisher (skill lùi, `attack_heavy_03`): không hủy cho tới hết hit cuối + 0.15s.

## 6. Độ nhạy và nối clip (giá trị khởi điểm)

| Thông số | Giá trị | Lý do |
|---|---|---|
| Bấm đến trúng, đòn đầu | 0.13s (bỏ `_pre`) / 0.33s (có `_pre`) | mục tiêu `spec/input.md` là 0.20-0.30s: bỏ `_pre` cho bước 1, giữ `_pre` cho bước 2+ để có dáng vào tay |
| `FadeIn` giữa bước | 0.08s (nhẹ), 0.12s (nặng) | clip Naraka tách pre/main nên crossfade ngắn là đủ |
| `RootMotionScale` | 0.6 (nhẹ), 0.5 (nặng), 0.4 (finisher 9 m) | Naraka lướt rất xa so với Big Sword |
| `Speed` | 1.0; bước 1 C1 thử 1.15 | |
| `_recover` | chỉ dùng 0.4-0.6s đầu | cuối clip là trở về idle chậm (1.5-4s) |

## 7. Đụng chạm với quyết định cũ

| Quyết định | Chỗ đụng | Đề xuất |
|---|---|---|
| D-001 (input cố định) | Đề bài gợi ý nút light/heavy/kick riêng | Không thêm nút; heavy = nhánh dừng nhịp, kick nằm trong clip. Nếu muốn nút Kick riêng phải ghi entry mới supersede D-001 |
| D-001 (B là cơ chế riêng) | Parry tay không cần một nút | Phương án A: B vẫn là Taunt, parry chỉ qua né hoàn hảo. Phương án B: B = Parry |
| D-002 (Dante 2 vũ khí) | Thêm vũ khí thứ 3 | Cần entry mới nếu chọn A; Style "đa dạng" có lợi |
| D-003 (Vergil deflect) | `attack_shock_light_01` là parry - dễ làm 2 nhân vật giống nhau | Không dùng parry tay không cho Dante; giữ deflect là của riêng Vergil |
| D-005 (thứ tự: Dante -> địch -> Vergil -> Trish) | Thêm phạm vi | Làm sau khi Dante 2 vũ khí đạt chuẩn và có địch biết đánh |
| D-010 / D-012 (địch) | Oni đã chốt là kiếm khách | HitReact Naraka dùng được cho địch Humanoid (Hell Executioner); Ghost/Undead/Oni là Generic, không dùng được nếu chưa retarget |
| `spec/input.md` lỗi nền #3 | Launcher cần trạng thái airborne cho mục tiêu | C3 phụ thuộc việc này; clip victim có sẵn (`male_hurt_fly_up_01`, `fly_land_back_01`, `getup_*`) |

## 8. Clip còn thiếu

| Thiếu | Ghi chú / tạm thay |
|---|---|
| Taunt tay không (D-002 cần 0.8s và 1.5s) | tạm `emptystep_back_01` (1.4s), cần clip thật |
| Block / guard loop Humanoid | Naraka không có; UF `resist`/`resistback` cần convert |
| Túm + ném (người túm) Humanoid | Naraka chỉ có victim; UF `normal_throw1/2` + `be_thrown_middle` cần convert |
| Hit react trên không (đang bay bị đánh tiếp) | Naraka còn `male_hurt_fly_*` 4 hướng chưa import |
| Ngã sấp + đứng dậy đánh (getup attack) | có `getup_front_battle`, thiếu ngã sấp và getup attack |
| Air dash / né trên không | chưa có |
| Chạy tường / đạp tường đầy đủ | chỉ có `walk_wall_attack_light_01`; controller chưa có trạng thái tường |
| Đòn chỏ riêng | chưa xác định được clip nào là chỏ (cần xem trong Editor) |
| Strafe 8 hướng thế quyền | Naraka `male_punch` không có strafe; dùng strafe hiện có của Dante |

## 9. Rủi ro kỹ thuật

- **Chưa kiểm tra trong Unity Editor**: chưa biết clip retarget lên Dante đẹp hay méo (tỉ lệ Naraka khác Dante), chưa xem đòn nào thật sự là đá/gối.
- Pipeline DKM đọc `AttackStep.Clip` là bản Inplace/ZeroHeight và tự lấy `RootSamples`; các builder "chuẩn hoá import" làm trên FBX. Clip ở đây là `.anim` gốc (không có FBX), nên loop / bake-into-pose phải chỉnh trong Inspector của clip; cần thử `Build Weapon Combos` có nhận `.anim` không.
- Clip YAML do Unity 6000.4 ghi, DKM là 6000.3.19f1: `serializedVersion: 7` trùng với clip có sẵn trong DKM nên dự kiến đọc được, nhưng chưa xác minh.
- Lần đầu mở Editor sẽ import ~95 MB `.anim` (vài phút).
