# Inventory anim võ thuật tay không (MartialArts)

Ngày: 2026-11-24. Trạng thái: **đã copy file, chưa kiểm tra trong Unity Editor** (Matrix/Feeder MCP offline).
Đi kèm: `gamedesign/research/martial-arts-combo-draft.md` (phác thảo combo dựng từ đúng các clip dưới đây).

## Tóm tắt

| Nguồn | Số clip | Rig | Dung lượng | Trạng thái |
|---|---|---|---|---|
| Naraka (`D:\_Reference\Naraka_Split\Naraka_Anim`, bản YAML text của `Naraka_AnimDemo`, cùng GUID) | 89 | Humanoid (muscle curves `RootT/RootQ/MotionT` + "Arm Down-Up"...) | ~69 MB | Dùng được ngay trên avatar Humanoid của Dante/Vergil |
| Ultimate Fighting (`G:\UnityProject\FightingRef`, tướng `hm_baiya` - đánh tay không, găng kim loại) | 21 | Generic, curve transform trên xương `Bip001` (3ds Max Biped) | ~26 MB | **Cần convert sang Humanoid** trước khi dùng |
| **Tổng** | **110** | | **~95 MB** | |

Theo nhóm:

| Nhóm | Naraka | UF | Ghi chú |
|---|---|---|---|
| Idle_Stance | 1 | 1 | |
| Move | 7 | 0 | walk / run / start / stop / quay đầu / sprint / lùi bước |
| Light_Chain | 8 | - | chuỗi nhẹ 3 đòn (pre / main / recover) |
| Heavy_Chain | 8 | - | chuỗi nặng 3 đòn, có gối/đá (đoán từ muscle) |
| Combo_Chain | - | 4 | chuỗi 4 đòn của UF |
| Charged | 8 | - | gồng (loop) + thả |
| Dash_Attack | 11 | 2 | sprint / sau né / thiểm bộ / trượt |
| Launcher_Air | 16 | 2 | 2 launcher, air light/heavy, plunge, wall attack, rơi/tiếp đất |
| Finisher | 5 | 4 | |
| Dodge_Parry | 8 | 2 | thiểm bộ 4 hướng, né hoàn hảo, parry |
| HitReact | 15 | 2 | nhẹ 4 hướng, nặng, stagger, hất, ngã, đứng dậy, choáng, bị parry |
| Grab_Throw | 2 | 4 | Naraka chỉ có phía bị túm; cặp túm-ném đầy đủ chỉ có ở UF |

Nhóm theo **chức năng gameplay** thay vì Punch/Kick/Elbow_Knee: clip Naraka trộn tay và chân trong cùng một đòn, và tôi không xem được hình,
nên "đấm/đá" trong ghi chú là **suy ra từ muscle curves** (đùi `Upper Leg Front-Back` > 1.0 hoặc `In-Out` > 1.5 thì đoán là đá/gối). Cần xem lại trong Editor.

## Cách đọc số liệu

- **Dài (s)**: `m_StopTime - m_StartTime` trong `.anim`.
- **Root motion**: độ dời `MotionT` (Naraka có `m_HasMotionFloatCurves: 1`) từ frame đầu đến frame cuối, theo humanScale = 1 (xấp xỉ mét).
  Nhiều đòn Naraka lướt rất xa (3-9 m): phải để `ClampApproach` / `RootMotionScale` cắt bớt, nếu không người chơi sẽ lướt xuyên địch.
- **Clip `_pre` / main / `_recover`**: Naraka tách mỗi đòn thành vào tay (0.2s) + đòn + hồi thế. `_recover` rất dài (1-4s) vì còn trở về idle; khi nối combo chỉ dùng 0.3-0.6s đầu.
- Naraka **không có Animation Event** nào (`m_Events: []`) - mốc trúng phải đặt bằng `Tools/Character Studio` > Mốc đánh. UF có event gốc trong `FightingRef/Assets/Games/UltimateFighting/_animation_events_backup.csv`; mốc hit/nối ở cột ghi chú lấy từ file đó.
- Naraka còn chứa vài curve Generic thừa (`MotionRoot/gMan Pelvis/LWaistWeapon`... - socket vũ khí của game gốc). Trên Dante các path này không tồn tại, Unity bỏ qua (có thể hiện cảnh báo vàng trong Inspector).

## Bảng clip

| Nhóm | Đường dẫn mới (dưới `Assets/_Asset/Animation/MartialArts/`) | Nguồn | Dài (s) | Loop | Root motion | Rig | Ghi chú |
|---|---|---|---|---|---|---|---|
| Idle_Stance | `Naraka/Idle_Stance/male_punch_idle_01.anim` | Naraka `male_punch/male_punch_idle_01` | 4.00 | Loop | Gần như tại chỗ | Humanoid (muscle) | Thế thủ quyền (stance) loop. Idle mặc định của bộ tay không. |
| Move | `Naraka/Move/male_punch_promenade_01.anim` | Naraka `male_punch/male_punch_promenade_01` | 1.00 | Loop | Có (~1.9 m) | Humanoid (muscle) | Đi bộ thế thủ, loop ~1.85 m/vòng. Walk khi lock-on/áp sát. |
| Move | `Naraka/Move/male_punch_run_01.anim` | Naraka `male_punch/male_punch_run_01` | 0.80 | Loop | Có (~3.0 m) | Humanoid (muscle) | Chạy thế quyền, loop (~3.7 m/s). |
| Move | `Naraka/Move/male_punch_run_start_01.anim` | Naraka `male_punch/male_punch_run_start_01` | 1.20 | - | Có (~4.5 m) | Humanoid (muscle) | Bắt đầu chạy (one-shot, quãng 4.45 m). |
| Move | `Naraka/Move/male_punch_run_shuffle_01.anim` | Naraka `male_punch/male_punch_run_shuffle_01` | 2.27 | - | Có (~0.3 m) | Humanoid (muscle) | Dừng chạy / nhún chân điều chỉnh (run stop). Cắt ngắn được. |
| Move | `Naraka/Move/male_punch_run_turn_back_left_01.anim` | Naraka `male_punch/male_punch_run_turn_back_left_01` | 0.33 | - | Có (~0.5 m) | Humanoid (muscle) | Quay đầu 180° khi chạy (yaw bake vào pose). |
| Move | `Naraka/Move/male_unarmed_sprint_01.anim` | Naraka `male_unarmed/male_unarmed_sprint_01` | 0.53 | Loop | Có (~3.5 m) | Humanoid (muscle) | Chạy nước rút tay không (set unarmed), loop. Đầu vào cho sprint attack. |
| Move | `Naraka/Move/male_punch_emptystep_back_01.anim` | Naraka `male_punch/male_punch_emptystep_back_01` | 1.40 | - | Có (~0.8 m) | Humanoid (muscle) | Lùi bước nhử ("empty step"). Backstep ngắn hoặc thế khiêu khích. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_01_pre.anim` | Naraka `male_punch/male_punch_attack_light_01_pre` | 0.20 | - | Có (~0.2 m) | Humanoid (muscle) | Windup 0.2s của Light 1. Ghép pre+main thành 1 bước nếu muốn. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_01.anim` | Naraka `male_punch/male_punch_attack_light_01` | 0.80 | - | Có (~1.8 m) | Humanoid (muscle) | Light 1, lướt ~1.8 m. Bước 1 chuỗi Light. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_01_recover.anim` | Naraka `male_punch/male_punch_attack_light_01_recover` | 3.00 | - | Có (~0.2 m) | Humanoid (muscle) | Thu về idle sau Light 1 (3.0s - quá dài, cắt còn 0.4-0.6s). |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_02_pre.anim` | Naraka `male_punch/male_punch_attack_light_02_pre` | 0.20 | - | Có (~0.7 m) | Humanoid (muscle) | Windup Light 2. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_02.anim` | Naraka `male_punch/male_punch_attack_light_02` | 1.03 | - | Có (~3.2 m) | Humanoid (muscle) | Light 2, lướt ~3.2 m. Bước 2 chuỗi Light. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_02_recover.anim` | Naraka `male_punch/male_punch_attack_light_02_recover` | 3.83 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về sau Light 2 (3.8s, cắt ngắn). |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_03.anim` | Naraka `male_punch/male_punch_attack_light_03` | 1.03 | - | Có (~3.0 m) | Humanoid (muscle) | Light 3 (bản gốc không có _pre), lướt ~3.0 m. Bước 3 / kết chuỗi Light. |
| Light_Chain | `Naraka/Light_Chain/male_punch_attack_light_03_recover.anim` | Naraka `male_punch/male_punch_attack_light_03_recover` | 4.13 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về sau Light 3 (4.1s, cắt ngắn). |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_01_pre.anim` | Naraka `male_punch/male_punch_attack_heavy_01_pre` | 0.20 | - | Có (~0.2 m) | Humanoid (muscle) | Windup Heavy 1. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_01.anim` | Naraka `male_punch/male_punch_attack_heavy_01` | 0.63 | - | Có (~2.8 m) | Humanoid (muscle) | Heavy 1, lướt ~2.8 m; muscle: chân phải nâng ~0.95 -> đoán có gối/đá. Bước 1 chuỗi Heavy. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_01_recover.anim` | Naraka `male_punch/male_punch_attack_heavy_01_recover` | 1.53 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về sau Heavy 1. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_02_pre.anim` | Naraka `male_punch/male_punch_attack_heavy_02_pre` | 0.20 | - | Có (~0.3 m) | Humanoid (muscle) | Windup Heavy 2. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_02.anim` | Naraka `male_punch/male_punch_attack_heavy_02` | 0.80 | - | Có (~4.1 m) | Humanoid (muscle) | Heavy 2, lướt ~4.1 m; chân trái co mạnh -> đoán gối/đạp. Bước 2 Heavy. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_02_recover.anim` | Naraka `male_punch/male_punch_attack_heavy_02_recover` | 1.73 | - | Có (~0.3 m) | Humanoid (muscle) | Thu về sau Heavy 2. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_03.anim` | Naraka `male_punch/male_punch_attack_heavy_03` | 1.17 | - | Có (~6.3 m) | Humanoid (muscle) | Heavy 3, lướt ~6.3 m; hai chân biên độ lớn -> đoán đá xoay. Kết chuỗi Heavy. |
| Heavy_Chain | `Naraka/Heavy_Chain/male_punch_attack_heavy_03_recover.anim` | Naraka `male_punch/male_punch_attack_heavy_03_recover` | 1.60 | - | Có (~0.4 m) | Humanoid (muscle) | Thu về sau Heavy 3. |
| Charged | `Naraka/Charged/male_punch_attack_light_01_charge.anim` | Naraka `male_punch/male_punch_attack_light_01_charge` | 5.00 | Loop | Có (~0.6 m) | Humanoid (muscle) | Gồng/tụ lực khi giữ Light, loop 5s. |
| Charged | `Naraka/Charged/male_punch_attack_hold_light_02.anim` | Naraka `male_punch/male_punch_attack_hold_light_02` | 1.50 | - | Có (~7.5 m) | Humanoid (muscle) | Thả Light đã gồng: lao xa ~7.5 m, chân ~1.3 -> đoán có đá. |
| Charged | `Naraka/Charged/male_punch_attack_hold_light_02_recover.anim` | Naraka `male_punch/male_punch_attack_hold_light_02_recover` | 1.63 | - | Có (~0.3 m) | Humanoid (muscle) | Thu về sau hold light 02. |
| Charged | `Naraka/Charged/male_punch_attack_hold_light_03.anim` | Naraka `male_punch/male_punch_attack_hold_light_03` | 2.33 | - | Có (~3.5 m) | Humanoid (muscle) | Thả Light gồng biến thể 2 (2.33s, nhiều nhịp), lướt ~3.5 m. |
| Charged | `Naraka/Charged/male_punch_attack_hold_light_03_recover.anim` | Naraka `male_punch/male_punch_attack_hold_light_03_recover` | 3.70 | - | Có (~0.5 m) | Humanoid (muscle) | Thu về (3.7s, cắt ngắn). |
| Charged | `Naraka/Charged/male_punch_attack_heavy_01_charge.anim` | Naraka `male_punch/male_punch_attack_heavy_01_charge` | 4.50 | Loop | Có (~0.4 m) | Humanoid (muscle) | Gồng Heavy, loop 4.5s. |
| Charged | `Naraka/Charged/male_punch_attack_hold_heavy_01.anim` | Naraka `male_punch/male_punch_attack_hold_heavy_01` | 1.50 | - | Có (~6.1 m) | Humanoid (muscle) | Thả Heavy đã gồng, lướt ~6.1 m. Charged heavy / phá thủ. |
| Charged | `Naraka/Charged/male_punch_attack_hold_heavy_01_recover.anim` | Naraka `male_punch/male_punch_attack_hold_heavy_01_recover` | 1.00 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về sau hold heavy. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_light_01_pre.anim` | Naraka `male_punch/male_punch_sprint_attack_light_01_pre` | 0.20 | - | Có (~1.4 m) | Humanoid (muscle) | Windup đòn lao khi sprint. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_light_01.anim` | Naraka `male_punch/male_punch_sprint_attack_light_01` | 1.13 | - | Có (~5.2 m) | Humanoid (muscle) | Đòn lao (sprint), lướt ~5.2 m, nhấc thân (yR 0.83) -> đoán đá bay. Ứng viên Lock+tới+X. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_light_01_recover.anim` | Naraka `male_punch/male_punch_sprint_attack_light_01_recover` | 3.33 | - | Có (~0.3 m) | Humanoid (muscle) | Thu về (3.3s, cắt ngắn). |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_heavy_01_pre.anim` | Naraka `male_punch/male_punch_sprint_attack_heavy_01_pre` | 0.20 | - | Có (~3.0 m) | Humanoid (muscle) | Windup đòn nặng khi chạy. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_heavy_01.anim` | Naraka `male_punch/male_punch_sprint_attack_heavy_01` | 0.90 | - | Có (~4.0 m) | Humanoid (muscle) | Đòn nặng khi chạy, lướt ~4.0 m. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_sprint_attack_heavy_01_recover.anim` | Naraka `male_punch/male_punch_sprint_attack_heavy_01_recover` | 1.33 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_dodge_attack_light_01.anim` | Naraka `male_punch/male_punch_dodge_attack_light_01` | 1.03 | - | Có (~5.0 m) | Humanoid (muscle) | Đòn sau né (dodge -> X), lướt 5 m, chân ~1.2 -> đoán có đá. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_dodge_attack_light_02.anim` | Naraka `male_punch/male_punch_dodge_attack_light_02` | 0.87 | - | Có (~4.0 m) | Humanoid (muscle) | Đòn sau né biến thể 2, lướt ~4 m. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_dodge_attack_light_03.anim` | Naraka `male_punch/male_punch_dodge_attack_light_03` | 0.83 | - | Có (~5.8 m) | Humanoid (muscle) | Đòn sau né biến thể 3, lướt ~5.8 m. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_flashstep_attack_light_01.anim` | Naraka `male_punch/male_punch_flashstep_attack_light_01` | 0.63 | - | Có (~4.2 m) | Humanoid (muscle) | Đòn sau thiểm bộ 0.63s, lướt ~4.2 m. Nhanh nhất bộ: gap-closer. |
| Dash_Attack | `Naraka/Dash_Attack/male_punch_slide_attack_heavy_01.anim` | Naraka `male_punch/male_punch_slide_attack_heavy_01` | 0.63 | - | Có (~3.8 m) | Humanoid (muscle) | Đòn nặng sau trượt, 0.63s, lướt ~3.8 m. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_crouch_attack_heavy_02_pre.anim` | Naraka `male_punch/male_punch_crouch_attack_heavy_02_pre` | 0.20 | - | Có (~0.6 m) | Humanoid (muscle) | Windup launcher. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_crouch_attack_heavy_02.anim` | Naraka `male_punch/male_punch_crouch_attack_heavy_02` | 0.63 | - | Có (~2.9 m) | Humanoid (muscle) | LAUNCHER: từ thế thấp bật cao (RootT.y +4.7), chân trái nâng ~1.2 -> đoán móc/đá hất. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_crouch_attack_heavy_02_recover.anim` | Naraka `male_punch/male_punch_crouch_attack_heavy_02_recover` | 0.23 | - | Có, chủ yếu theo chiều cao (RootT.y 1.2) | Humanoid (muscle) | Kết thúc trên không (0.23s) -> nối air combo / jump_down_loop. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_slide_attack_heavy_soul_01.anim` | Naraka `male_punch/male_punch_slide_attack_heavy_soul_01` | 1.03 | - | Có (~6.7 m) | Humanoid (muscle) | Launcher bản "soul": lướt 6.7 m rồi bật cao +4.9. Launcher tiêu tài nguyên. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_light_01_pre.anim` | Naraka `male_punch/male_punch_jump_attack_light_01_pre` | 0.20 | - | Gần như tại chỗ | Humanoid (muscle) | Windup đòn trên không. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_light_01.anim` | Naraka `male_punch/male_punch_jump_attack_light_01` | 0.63 | - | Gần như tại chỗ | Humanoid (muscle) | Light trên không, gần như tại chỗ. Air combo (lặp có giới hạn). |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_light_01_recover.anim` | Naraka `male_punch/male_punch_jump_attack_light_01_recover` | 0.50 | - | Gần như tại chỗ | Humanoid (muscle) | Thu về trên không. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_heavy_01_pre.anim` | Naraka `male_punch/male_punch_jump_attack_heavy_01_pre` | 0.47 | - | Có (~0.3 m) | Humanoid (muscle) | Windup đòn bổ xuống trên không (0.47s). |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_heavy_01.anim` | Naraka `male_punch/male_punch_jump_attack_heavy_01` | 0.20 | - | Có (~0.2 m) | Humanoid (muscle) | Heavy trên không 0.2s (khoảnh khắc đập). Air finisher / dập xuống. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_attack_heavy_01_recover.anim` | Naraka `male_punch/male_punch_jump_attack_heavy_01_recover` | 0.97 | - | Có (~0.3 m) | Humanoid (muscle) | Thu về sau đập. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_shouji_attack_01.anim` | Naraka `male_punch/male_punch_shouji_attack_01` | 0.63 | - | Gần như tại chỗ | Humanoid (muscle) | Đòn trên không lao xuống (dive), chân trái nâng ~1.1. Phần 1 plunge. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_shouji_attack_01_land.anim` | Naraka `male_punch/male_punch_shouji_attack_01_land` | 0.67 | - | Có (~2.0 m) | Humanoid (muscle) | Chạm đất của plunge, lướt ~2 m. Phần 2. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_shouji_attack_01_land_recover.anim` | Naraka `male_punch/male_punch_shouji_attack_01_land_recover` | 1.47 | - | Có (~0.3 m) | Humanoid (muscle) | Thu về sau plunge. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_walk_wall_attack_light_01.anim` | Naraka `male_punch/male_punch_walk_wall_attack_light_01` | 0.80 | - | Gần như tại chỗ | Humanoid (muscle) | Đòn khi chạy tường, tại chỗ. Ứng viên đạp tường; controller chưa có trạng thái tường. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_down_loop_01.anim` | Naraka `male_punch/male_punch_jump_down_loop_01` | 0.80 | Loop | Gần như tại chỗ | Humanoid (muscle) | Rơi loop (thế quyền), chờ giữa các đòn air. |
| Launcher_Air | `Naraka/Launcher_Air/male_punch_jump_down_to_land_01.anim` | Naraka `male_punch/male_punch_jump_down_to_land_01` | 0.80 | - | Gần như tại chỗ | Humanoid (muscle) | Tiếp đất. |
| Finisher | `Naraka/Finisher/male_punch_attack_heavy_soul_03.anim` | Naraka `male_punch/male_punch_attack_heavy_soul_03` | 1.40 | - | Có (~9.2 m) | Humanoid (muscle) | FINISHER: Heavy "soul", lướt 9.25 m, chân biên độ ~1.8 -> đoán đá bay/xoáy. Đòn chốt tiêu tài nguyên. |
| Finisher | `Naraka/Finisher/male_punch_attack_heavy_soul_03_recover.anim` | Naraka `male_punch/male_punch_attack_heavy_soul_03_recover` | 0.87 | - | Có (~0.8 m) | Humanoid (muscle) | Thu về sau finisher. |
| Finisher | `Naraka/Finisher/male_punch_attack_hold_light_soul_03.anim` | Naraka `male_punch/male_punch_attack_hold_light_soul_03` | 2.00 | - | Gần như tại chỗ | Humanoid (muscle) | Finisher tại chỗ 2.0s nhiều nhịp (rush liên hoàn), gần như không lướt. |
| Finisher | `Naraka/Finisher/male_punch_perfect_dodge_attack_light_01.anim` | Naraka `male_punch/male_punch_perfect_dodge_attack_light_01` | 1.00 | - | Có (~2.8 m) | Humanoid (muscle) | COUNTER sau né hoàn hảo, bật lên (yR 1.4), chân phải In-Out ~1.9 -> đoán đá ngang. |
| Finisher | `Naraka/Finisher/male_punch_perfect_dodge_attack_light_01_recover.anim` | Naraka `male_punch/male_punch_perfect_dodge_attack_light_01_recover` | 2.07 | - | Có (~1.3 m) | Humanoid (muscle) | Thu về sau counter. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_flashstep_front_01.anim` | Naraka `male_punch/male_punch_flashstep_front_01` | 0.90 | - | Có (~2.8 m) | Humanoid (muscle) | Thiểm bộ tới 0.9s, ~2.8 m. Dodge chính. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_flashstep_back_01.anim` | Naraka `male_punch/male_punch_flashstep_back_01` | 0.90 | - | Có (~3.8 m) | Humanoid (muscle) | Thiểm bộ lùi 0.9s, ~3.8 m. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_flashstep_left_01.anim` | Naraka `male_punch/male_punch_flashstep_left_01` | 0.90 | - | Có (~4.4 m) | Humanoid (muscle) | Thiểm bộ trái 0.9s, ~4.4 m. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_flashstep_right_01.anim` | Naraka `male_punch/male_punch_flashstep_right_01` | 0.90 | - | Có (~3.6 m) | Humanoid (muscle) | Thiểm bộ phải 0.9s, ~3.6 m. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_dodge_back_double_01.anim` | Naraka `male_punch/male_punch_dodge_back_double_01` | 1.70 | - | Có (~3.9 m) | Humanoid (muscle) | Né lùi lần 2 (nhào lộn, yR 0.78), 1.7s. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_perfect_dodge_back_01.anim` | Naraka `male_punch/male_punch_perfect_dodge_back_01` | 3.43 | - | Có (~3.0 m) | Humanoid (muscle) | Né hoàn hảo 3.4s - cắt ngắn, dùng khi né đúng nhịp. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_attack_shock_light_01.anim` | Naraka `male_punch/male_punch_attack_shock_light_01` | 1.50 | - | Có (~5.7 m) | Humanoid (muscle) | PARRY tay không (Naraka "shock"/chấn đao): gạt rồi lao tới 5.7 m. Parry + phản đòn. |
| Dodge_Parry | `Naraka/Dodge_Parry/male_punch_attack_shock_light_01_recover.anim` | Naraka `male_punch/male_punch_attack_shock_light_01_recover` | 2.37 | - | Có (~0.4 m) | Humanoid (muscle) | Thu về sau parry. |
| HitReact | `Naraka/HitReact/male_hurt_stand_light_front_01.anim` | Naraka `male_hurt/male_hurt_stand_light_front_01` | 1.33 | - | Có (~0.5 m) | Humanoid (muscle) | Trúng đòn nhẹ từ trước, lùi 0.5 m. |
| HitReact | `Naraka/HitReact/male_hurt_stand_light_back_01.anim` | Naraka `male_hurt/male_hurt_stand_light_back_01` | 1.33 | - | Gần như tại chỗ | Humanoid (muscle) | Trúng đòn nhẹ từ sau. |
| HitReact | `Naraka/HitReact/male_hurt_stand_light_left_01.anim` | Naraka `male_hurt/male_hurt_stand_light_left_01` | 1.33 | - | Gần như tại chỗ | Humanoid (muscle) | Trúng đòn nhẹ từ trái. |
| HitReact | `Naraka/HitReact/male_hurt_stand_light_right_01.anim` | Naraka `male_hurt/male_hurt_stand_light_right_01` | 1.33 | - | Gần như tại chỗ | Humanoid (muscle) | Trúng đòn nhẹ từ phải. |
| HitReact | `Naraka/HitReact/male_hurt_stand_heavy_front_01.anim` | Naraka `male_hurt/male_hurt_stand_heavy_front_01` | 1.00 | - | Có (~1.3 m) | Humanoid (muscle) | Trúng đòn nặng từ trước, lùi 1.3 m. |
| HitReact | `Naraka/HitReact/male_hurt_stand_heavy_front_long_01.anim` | Naraka `male_hurt/male_hurt_stand_heavy_front_long_01` | 1.33 | - | Gần như tại chỗ | Humanoid (muscle) | Choáng nặng kéo dài (stagger long). |
| HitReact | `Naraka/HitReact/male_hurt_stand_heavy_front_long_01_recover.anim` | Naraka `male_hurt/male_hurt_stand_heavy_front_long_01_recover` | 0.30 | - | Gần như tại chỗ | Humanoid (muscle) | Hồi sau stagger long. |
| HitReact | `Naraka/HitReact/male_hurt_fly_up_01.anim` | Naraka `male_hurt/male_hurt_fly_up_01` | 1.00 | - | Gần như tại chỗ | Humanoid (muscle) | Bị hất lên (airborne) - victim của launcher. |
| HitReact | `Naraka/HitReact/male_hurt_fly_back_01.anim` | Naraka `male_hurt/male_hurt_fly_back_01` | 0.27 | - | Gần như tại chỗ | Humanoid (muscle) | Bị đánh bay ra sau (0.27s đoạn khởi đầu, nối land). |
| HitReact | `Naraka/HitReact/male_hurt_fly_land_back_01.anim` | Naraka `male_hurt/male_hurt_fly_land_back_01` | 0.80 | - | Gần như tại chỗ | Humanoid (muscle) | Rơi đất nằm ngửa (knockdown). |
| HitReact | `Naraka/HitReact/male_all_getup_back_battle.anim` | Naraka `male_all/male_all_getup_back_battle` | 0.50 | - | Có (~0.3 m) | Humanoid (muscle) | Đứng dậy từ nằm ngửa, bản chiến đấu 0.5s. |
| HitReact | `Naraka/HitReact/male_all_getup_front_battle.anim` | Naraka `male_all/male_all_getup_front_battle` | 0.50 | - | Gần như tại chỗ | Humanoid (muscle) | Đứng dậy từ nằm sấp, bản chiến đấu 0.5s. |
| HitReact | `Naraka/HitReact/male_stun_01.anim` | Naraka `male_stun/male_stun_01` | 7.00 | Loop | Gần như tại chỗ | Humanoid (muscle) | Choáng loop 7s (phá thủ / bị parry). |
| HitReact | `Naraka/HitReact/male_punch_hero_shock_01.anim` | Naraka `male_punch/male_punch_hero_shock_01` | 0.83 | - | Có (~1.1 m) | Humanoid (muscle) | Bị đối thủ parry - khựng lùi 1 m. |
| HitReact | `Naraka/HitReact/male_punch_shocked_01.anim` | Naraka `male_punch/male_punch_shocked_01` | 3.00 | - | Có (~3.8 m) | Humanoid (muscle) | Bị parry nặng, văng 3.8 m, bật cao. 3.0s. |
| Grab_Throw | `Naraka/Grab_Throw/male_unarmed_becatched_back_01.anim` | Naraka `male_unarmed/male_unarmed_becatched_back_01` | 2.00 | - | Gần như tại chỗ | Humanoid (muscle) | Victim bị túm từ sau (2.0s). Không có anim người túm khớp cặp. |
| Grab_Throw | `Naraka/Grab_Throw/male_unarmed_becatched_idle_01.anim` | Naraka `male_unarmed/male_unarmed_becatched_idle_01` | 2.00 | Loop | Gần như tại chỗ | Humanoid (muscle) | Victim bị giữ, loop. |
| Idle_Stance | `UltimateFighting/Idle_Stance/UF_baiya_stand.anim` | UF `hm_baiya/animations/stand` | 2.00 | Loop | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Thế đứng chiến đấu loop. |
| Combo_Chain | `UltimateFighting/Combo_Chain/UF_baiya_attack1_1.anim` | UF `hm_baiya/animations/attack1_1` | 0.63 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn thường 1 (đấm). Hit 0.18s, mở cửa nối 0.22s. |
| Combo_Chain | `UltimateFighting/Combo_Chain/UF_baiya_attack1_2.anim` | UF `hm_baiya/animations/attack1_2` | 0.67 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn thường 2. Hit 0.18s, nối 0.28s. |
| Combo_Chain | `UltimateFighting/Combo_Chain/UF_baiya_attack1_3.anim` | UF `hm_baiya/animations/attack1_3` | 0.80 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn thường 3. Hit 0.15s, nối 0.28s. |
| Combo_Chain | `UltimateFighting/Combo_Chain/UF_baiya_attack1_4.anim` | UF `hm_baiya/animations/attack1_4` | 0.80 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn thường 4 (kết). Hit 0.22s. |
| Dash_Attack | `UltimateFighting/Dash_Attack/UF_baiya_rush1_1.anim` | UF `hm_baiya/animations/rush1_1` | 0.40 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Lao tới 0.4s, bất tử từ 0s, nối 0.22s. |
| Dash_Attack | `UltimateFighting/Dash_Attack/UF_baiya_dodge_attack.anim` | UF `hm_baiya/animations/dodge_attack` | 0.50 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Né-đánh: bất tử 0s, hit 0.18s, nối 0.32s. |
| Launcher_Air | `UltimateFighting/Launcher_Air/UF_baiya_jump_attack01.anim` | UF `hm_baiya/animations/jump_attack01` | 0.70 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn trên không 1, hit 0.18s. |
| Launcher_Air | `UltimateFighting/Launcher_Air/UF_baiya_jump_attack02.anim` | UF `hm_baiya/animations/jump_attack02` | 0.70 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đòn trên không 2, hit 0.05s. |
| Finisher | `UltimateFighting/Finisher/UF_baiya_special_move.anim` | UF `hm_baiya/animations/special_move` | 1.70 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Skill liên hoàn 9 hit (0.28-1.05s), nối 0.52s. |
| Finisher | `UltimateFighting/Finisher/UF_baiya_super_power1.anim` | UF `hm_baiya/animations/super_power1` | 1.87 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Tuyệt chiêu 9 hit (0.35-1.32s), bất tử từ 0s. |
| Finisher | `UltimateFighting/Finisher/UF_baiya_max_super_special_move1.anim` | UF `hm_baiya/animations/max_super_special_move1` | 1.50 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Siêu tuyệt chiêu (cinematic, game gốc có camera riêng), hit 0.75s. |
| Finisher | `UltimateFighting/Finisher/UF_baiya_target_combo1.anim` | UF `hm_baiya/animations/target_combo1` | 1.37 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Target combo 3 hit (0.42 / 0.62 / 1.02s). |
| Grab_Throw | `UltimateFighting/Grab_Throw/UF_baiya_normal_throw1.anim` | UF `hm_baiya/animations/normal_throw1` | 0.43 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Túm (phần bắt) 0.43s. |
| Grab_Throw | `UltimateFighting/Grab_Throw/UF_baiya_normal_throw2.anim` | UF `hm_baiya/animations/normal_throw2` | 1.53 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Ném (phần quật), hit 0.98s. |
| Grab_Throw | `UltimateFighting/Grab_Throw/UF_baiya_be_thrown_middle.anim` | UF `hm_baiya/animations/be_thrown_middle` | 2.83 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Victim bị ném (2.83s), cửa phá ném 0-0.42s. |
| Grab_Throw | `UltimateFighting/Grab_Throw/UF_baiya_break_throw.anim` | UF `hm_baiya/animations/break_throw` | 0.60 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Phá ném. |
| Dodge_Parry | `UltimateFighting/Dodge_Parry/UF_baiya_resist.anim` | UF `hm_baiya/animations/resist` | 1.00 | Loop | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đỡ / block loop. |
| Dodge_Parry | `UltimateFighting/Dodge_Parry/UF_baiya_resistback.anim` | UF `hm_baiya/animations/resistback` | 0.17 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Block trúng đòn (0.17s). |
| HitReact | `UltimateFighting/HitReact/UF_baiya_rise.anim` | UF `hm_baiya/animations/rise` | 0.50 | - | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Đứng dậy 0.5s. |
| HitReact | `UltimateFighting/HitReact/UF_baiya_swoon.anim` | UF `hm_baiya/animations/swoon` | 1.20 | Loop | Không (game gốc dịch bằng code) | **Generic Bip001 - cần convert** | Choáng loop. |

## GUID và dependency

- Đã so 110 GUID với mọi `.meta` trong `DKM/Assets` và `DKM/Packages`: **không trùng**, giữ nguyên `.meta` gốc.
- Trong `.meta` đã xoá giá trị `assetBundleName` (game gốc gán bundle name) để không thêm AssetBundle lạ vào DKM. GUID giữ nguyên.
- Clip `.anim` Humanoid không tham chiếu avatar/FBX nào (không có `guid:` trong file) -> không cần copy model/avatar.
- Clip UF đổi tên file thành `UF_baiya_<tên>.anim` cho dễ nhận (tên gốc `attack1_1` quá chung); `m_Name` bên trong vẫn là tên gốc, Unity sẽ lấy theo tên file khi import.
- Không copy model, texture, material, controller nào.

## Clip UF: vì sao cần convert, và cách convert

UF là clip Generic: curve `localRotation/localPosition` theo đường dẫn xương `Bip001/Bip001 Pelvis/...`. Gán vào Animator Humanoid của Dante thì
không có gì chuyển động (path không khớp). Avatar `H_Baiya_01Avatar.asset` trong FightingRef **có** human description (Bip001 Pelvis -> Hips...),
nên convert được:
1. Làm trong `FightingRef` (Unity 6000.4): đặt prefab `H_Baiya_01` (Animator + avatar Humanoid đó), editor script lấy mẫu từng frame clip bằng
   `AnimationClip.SampleAnimation` rồi `HumanPoseHandler.GetHumanPose`, ghi muscle + `RootT/RootQ` ra `.anim` mới (giống cách Unity bake Humanoid).
2. Copy các `.anim` Humanoid đã bake sang DKM, thay cho file trong `MartialArts/UltimateFighting/`.
3. Game gốc dịch chuyển nhân vật bằng code (`OnAnimationForwardStart`, có tham số quãng đường), nên clip đã bake gần như **không có root motion**;
   phải thêm lunge bằng code hoặc tự cộng `RootT` khi bake.
Chưa làm bước nào ở trên.

## Ứng viên còn để lại (chưa import)

- Naraka `male_punch`: `attack_light_02/03_charge`, `attack_heavy_02/03_charge`, `dodge_*_01` 4 hướng (2.5-2.9s, có thể thay thiểm bộ), `crouch_attack_light_01`, `crouch_dodge_*`, `run_attack_*` (trùng `attack_*_01`), `jump_shock_*`, `bounce_*` (bị đẩy bật), `squat_*`, `walk_wall_*_attack_light_charge/pre` (chuỗi đạp tường đầy đủ).
- Naraka `male_hurt`: còn ~290 clip (light/heavy 5 hướng x nhiều biến thể, `fly_*` 4 hướng, `fly_drop_flip_*`).
- Naraka hero `monk` (`foxiang_meleecatch_*`): túm, nhưng ở dạng Phật tướng khổng lồ - không hợp làm túm thường.
- UF `hm_kui`, `hm_huanggai`: 2 tướng tay không khác (găng), cùng bộ tên clip với Baiya (`attack1_1-3`, `normal_throw1/2`, `super_power1`...).
