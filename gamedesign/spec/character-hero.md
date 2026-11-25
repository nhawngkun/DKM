# Nhân vật chính (Hero) - kit quyền cước v1

> **2026-11-24 - D-016 thay kit v1:** chuyển sang Street Brawl (đấm, móc, tạt, đá, túm-ném), anim Spider Fighter 3 trong `Assets/_Asset/Animation/StreetBrawl`, bỏ toàn bộ clip Naraka. Bảng chuỗi + phím hiện hành: `gamedesign/spec/hero-combo-test.md`. Túm-ném: `Combat/HeroGrapple.cs` (nút B / Q, chuỗi Skill "Tum Nem"). Phần dưới đây là lịch sử kit Naraka v1, giữ lại các bài học kỹ thuật (.anim Inplace, IK goal, nhận diện đá).

Trạng thái: **Prototype** (2026-11-24) - dựng để playtest, chưa chốt. Nền: D-013 (reboot), D-014 (pillars). Cơ chế lên đồng / Tứ Phủ / phong ấn quỷ: **chưa thiết kế**, người dùng đang cân nhắc.
Model: `Assets/_Asset/3D/AvatarV10/AvatarV10.fbx` · Prefab: `Assets/_Asset/Prefab/Hero.prefab` · Bộ anim: `HR_` · Vũ khí: `WD_Fist` (`WeaponType.Fist`).
Dựng lại: `Tools/Combat/Nhân vật/Hero (Quyền) - Setup + Build` (hồ sơ `Hero` trong `Editor/CharacterSetupTool.cs`).

## Phong cách

Quyền lướt kiểu võ Trung Hoa (clip Naraka): mỗi đòn kéo thân đi 2-6 m, người chơi "trôi" qua đám địch thay vì đứng đấm tại chỗ (Pillar 2 - Chuyển động không gãy).
Nhịp: chuỗi nhẹ nhanh -> dừng nhịp rẽ sang chuỗi nặng có gối/đá -> hất tung -> không trung. Điểm yếu có chủ đích: tầm ngắn, đòn lướt xa dễ trượt nếu không lock-on.

## Input (giữ D-001: X đánh, Y + hướng skill, RT né, RB lock-on, A nhảy)

| Input | Chuỗi | Clip (Naraka `male_punch_*`) |
|---|---|---|
| X X X | Liên Hoàn Quyền (Ground) | attack_light_01 / 02 / 03 |
| X, dừng nhịp, X X X | Trọng Cước (Ground, rẽ nhánh trễ nhịp) | attack_light_01, attack_heavy_01 / 02 / 03 |
| Sprint / vừa né / lock + tới, X | Thiểm Bộ (Dash) | dodge_attack_light_01 / 02 / 03 |
| Lock + lùi + X | Thăng Long (Launcher, root motion dọc) | crouch_attack_heavy_02 |
| Trên không X X X | Không Trung (Air) | jump_attack_light_01 x2, jump_attack_heavy_01 |
| Y đứng yên | Bạo Phá Quyền (Skill 0) | attack_heavy_soul_03 |
| Y + tới | Phi Long Cước (Skill 1) | sprint_attack_heavy_01 |
| Y + lùi | Hồi Mã Phản Kích (Skill 2) | perfect_dodge_back_01 -> attack_shock_light_01 |
| RT | Né 4 hướng (locomotion `HR_Dodge*`) | flashstep_front / back / left / right_01 |

Di chuyển: idle `male_punch_idle_01`, chạy `male_punch_run_01`, sprint `male_unarmed_sprint_01`. Đi bộ, strafe 8 hướng, nhảy, crouch, turn tạm dùng clip Dante (Naraka không có).

## Kỹ thuật

- Clip đòn là `.anim` Humanoid có root motion. Builder sinh bản `<clip>_Inplace.anim` trong `MartialArts/Naraka/Inplace/` (san phẳng RootT.xz; thêm RootT.y với Air/Launcher), bản gốc dùng làm Root để bake `RootSamples` - giống quy ước FBX Inplace/Root của Dante/Vergil.
  - Bản Inplace phải là **clip mới tạo từ curve, bỏ MotionT/MotionQ, RootT.xz = 0**. `Instantiate` clip gốc rồi sửa RootT không được: Unity giữ motion tính từ RootT cũ, thân bị kéo lùi cả quãng lướt (đo 2026-11-24: lệch 3-12 m). Clip Naraka cũng không bắt đầu ở gốc (RootT.z ~3 m) nên không san về giá trị khung đầu.
- `WeaponType.Fist`: builder đo mốc trúng (`HitTimes`) theo tốc độ 2 nắm tay so với hông, `KickTimes` theo bàn chân như cũ; `MeleeHitDetector` quét cẳng tay -> nắm tay của tay đang vung nhanh hơn (`FistRadius`, `FistLength`).
- Mốc đều do builder tự phân tích: **chỉnh tay ở Character Studio > Mốc đánh** sau khi xem.

## Giá trị khởi điểm để tune

| Thông số | Giá trị | Ghi chú |
|---|---|---|
| ComboResetTime | 0.6 s | |
| FistRadius / FistLength | 0.15 / 0.1 m | `MeleeHitDetector` trên Hero_Model |
| RootMotionScale | 1.0 | đòn nặng lướt 4-6 m: hạ xuống 0.6-0.8 nếu lướt xuyên địch |
| MoveCancelMinTime | 0.75 | chung `CharacterCombo`; Pillar 2 có thể cần hạ |

## Còn thiếu / mở

- Đòn gồng (giữ X) và parry tay không: hệ combo chưa có input giữ nút / parry.
- Clip `_pre` và `_recover` chưa dùng (đòn kết thúc ở cuối clip chính).
- 21 clip Ultimate Fighting (baiya) còn Generic, chưa convert.
- Đòn đá trong clip Naraka nhận diện tự động (KickTimes; luật riêng cho Fist: nhận đá cao, xét độ cao/tầm vươn ở lúc chân duỗi hết sau đỉnh tốc độ, chân đã đặt xuống thì thôi). Quyền cước vừa đấm vừa đá dừng ở min(tầm đấm, KickStopDistance).
- Playtest 2026-11-24 (lần 12): 31 lần trúng, mọi đòn có đánh đều trúng trừ Thiểm Bộ #2 (lần này hụt, các lần trước trúng). Hồi Mã #2 (`attack_shock_light_01`, lướt rồi đá thẳng) trúng 2/2. Hồi Mã #1 còn mốc đấm thừa ở 0.04 (cú lùi).
- "Chân quặp lên" (đã sửa 2026-11-24): clip Naraka .anim không có curve IK goal (LeftFootT/Q...), `PlayerIK` lúc đánh đọc `GetIKPosition` sai -> kéo bàn chân cao hơn hông. Hero đặt `PlayerIK.AttackFootWeight = 0` (tắt hẳn IK ngay khi ra đòn). Locomotion (idle/run Naraka) vẫn bật IK, idle đo đúng; chạy chưa đo riêng.
- Bỏ `flashstep_attack_light_01` và `perfect_dodge_attack_light_01`: lướt bay co chân / bổ nhào từ 2.2 m, không hợp nhân vật chạy đất. Thay bằng `dodge_attack_light_01` (Thiểm Bộ #1) và `attack_shock_light_01` (Hồi Mã #2).

## Prototype (slider cho unity-mechanic)

RootMotionScale từng đòn, Speed từng đòn, FistRadius, WindowBefore/After, DelayedBranchMinPause, MoveCancelMinTime.
