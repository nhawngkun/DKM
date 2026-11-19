# Character: Dante (D-002)

Role: nhân vật chính / chuẩn mẫu
Fantasy: thợ săn quỷ phô diễn, đánh đẹp để chọc tức đối thủ
Tính cách: ngạo nghễ, cợt nhả, bùng nổ. Càng đánh đẹp càng tự tin, và game phải thưởng cho điều đó.
Điểm yếu: taunt và đòn nặng để lộ sơ hở; lặp lại một đòn thì Style tụt.

## Kit

### Đánh thường (X): Big Sword
| Input | Đòn | Clip |
|---|---|---|
| X X X X | Chuỗi cơ bản | `Attack_4Combo_A` 1A-2-3-4 |
| X X (dừng nhịp) X X X X X | Chuỗi trình diễn | rẽ sang `Attack_7Combo` từ đòn 3 |
| Lock + tới + X | Lao tới | `Dash_Attack_ver_A` |
| Lock + lùi + X | **Launcher**, hất mục tiêu | `UpperAttack` |
| Trên không X X X | Air combo | `Jump_Attack_Combo_1-3` |

### Đánh thường (X): Katana Blade
| Input | Đòn | Clip |
|---|---|---|
| X X X X X | Chuỗi nhanh | `Attack_5Combo` |
| X X (dừng nhịp) X X | Nhánh | `Attack_3Combo` từ đòn 2 |
| Lock + tới + X | Lao tới | `Dash_Attack_ver_A` |
| Lock + lùi + X | Launcher | chọn 1 skill có hất lên trong Skill A-I (cần xem anim) |
| Trên không X x4 | Air combo | `Jump_Attack_Combo_1-4` |

### Skill (Y + hướng): 3 skill mỗi vũ khí
Chọn từ `M_Big_Sword@Skill_A-L` và `M_katana_Blade@Skill_A-I` theo vai trò:
- Đứng yên: đòn quét vùng (đánh đám đông)
- Tới: đòn lao xa (áp sát)
- Lùi: đòn chốt nặng (tiêu Style, mạnh nhất ở SSS)

Việc chọn clip cụ thể làm trong Character Studio sau khi xem anim.

### Cơ chế riêng: B = Taunt
- Chen được giữa combo, ở mốc LinkTime giống như nối đòn.
- Taunt chạy hết anim thì được Style. Bị đánh trúng hoặc tự hủy (nhảy, né) thì mất phần thưởng.
- Taunt càng gần mục tiêu lock-on càng nhiều Style.
- Đổi vũ khí giữa combo (D-pad) là cancel: đòn kế mở chuỗi của vũ khí mới, và tính là "đa dạng".

### Thanh Style (D, C, B, A, S, SS, SSS)
| Nguồn | Điểm (khởi điểm) |
|---|---|
| Đòn trúng, đòn mới chưa dùng trong 5 đòn gần nhất | +10 |
| Đòn trúng, lặp lại đòn vừa dùng | +2 |
| Taunt hết anim, mục tiêu trong 3m | +40 |
| Taunt hết anim, mục tiêu 3-8m | +15 |
| Đổi vũ khí rồi trúng đòn trong 1s | +15 |
| Bị đánh | tụt 2 bậc |
| Không trúng gì 4s | tụt dần 15/s |

- Mỗi bậc rank: 100 điểm (D=0 ... SSS=600).
- Sát thương nhân theo rank: D 1.0 -> SSS 1.3.
- Skill lùi (đòn chốt) ở SSS: tiêu về A, mạnh gấp 2.

## Anim còn thiếu
- 1-2 clip taunt (một ngắn khoảng 0.8s, một dài khoảng 1.5s, thưởng nhiều hơn).

## Gameplay reads cho art-lead
- Chữ rank to và nổi khi lên bậc. SSS phải là một khoảnh khắc riêng.
- Big Sword và Katana phải phân biệt được ngay bằng silhouette.

## Brief nhạc cho music-lead
- Emotional core: tự tin, ngạo nghễ. Nhạc combat lên tầng theo rank (D-B: nền; A-S: thêm lớp; SS-SSS: full).

## Prototype (slider cho unity-mechanic)
DelayedWindowMin/Max, StylePerNewHit, StylePerRepeat, TauntRangeNear/Far, TauntReward, DecayDelay, DecayRate, RankDamageMax.
