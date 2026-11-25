# Bảng combo Hero (kit Naraka + đòn người dùng chọn) - test tay

Scene `Assets/Sence/SetUp.unity` → Play: Hero là nhân vật duy nhất (D-019), scene có sẵn 1 `TrainingDummy` trước mặt (nó tự đi về chỗ cũ sau 3 s, tắt `HitReaction.ReturnHome` nếu muốn đứng yên).

## Phím (tay cầm theo VỊ TRÍ nút)

| Hành động | Bàn phím + chuột | Xbox | PlayStation |
|---|---|---|---|
| Đánh | Chuột trái | X (trái) | □ Vuông |
| Skill (theo hướng) | Chuột phải | Y (trên) | △ Tam giác |
| Túm - ném | Q | B (phải) | ○ Tròn |
| Né | E | RT | R2 |
| Đỡ đòn | R | LB | L1 |
| Nhảy | Space | A (dưới) | ✕ |
| Chạy nhanh | giữ Shift | giữ L3 | giữ L3 |
| Lock-on | giữ chuột giữa | giữ RB | giữ R1 |

Click chuột trái lần đầu chỉ để khoá con trỏ, chưa ra đòn.

## Combo

| # | Tên | Cách bấm | Clip (Naraka `male_punch_*`) |
|---|---|---|---|
| 1 | **Trọng Cước** | Đánh ×3 nhịp đều | attack_heavy_01 → heavy_02 → **attack_hold_heavy_01** (đá xoáy) |
| 2 | **Xoáy Cước** | Đánh, **dừng ~0.3-0.5 s**, rồi Đánh ×2 | attack_heavy_01 → **sprint_attack_light_01** (đá xoáy) → **attack_hold_light_02** |
| 3 | **Thiểm Bộ** | Né rồi Đánh ngay (trong 0.35 s), hoặc chạy nhanh + Đánh, hoặc lock-on + đẩy **tới** + Đánh; rồi Đánh ×2 | **flashstep_attack_light_01** (song phi) → dodge_attack_light_02 → 03 |
| 4 | **Thăng Long** | Lock-on + kéo **lùi** + Đánh | crouch_attack_heavy_02 (hất tung) |
| 5 | **Không Trung** | Trên không (sau Thăng Long hoặc nhảy) Đánh ×3 | jump_attack_light_01 ×2 → jump_attack_heavy_01 |
| 6 | **Bạo Phá Quyền** | Đứng yên + Skill | attack_heavy_soul_03 |
| 7 | **Phi Long Cước** | Đẩy **tới** + Skill | sprint_attack_heavy_01 |
| 8 | **Hồi Mã Phản Kích** | Kéo **lùi** + Skill, rồi **vẫn giữ lùi** + Skill | perfect_dodge_back_01 → **perfect_dodge_attack_light_01** (bổ nhào) |
| 10 | **Phản Đòn** | Tự ra khi **đỡ trúng** đòn chớp xanh (R / LB / L1 ngay trước khi đòn chạm) | attack_shock_light_01 |
| 9 | **Túm Ném** | Sát địch (≤ 1.8 m, trước mặt) bấm **Q / B / ○** | Hulk_ThrowOne_Take → Throw (SF3): ném văng ~3.2 m, hất cao ~2.4 m |

Đã bỏ: attack_light_01 / 02 / 03, attack_heavy_03. Ngừng bấm quá 0.6 s thì combo reset.
Quãng lướt: mọi đòn dưới đất / lao / skill tối đa **1 m** (`MaxRootTravel` trong hồ sơ Hero). Thăng Long và Không Trung giữ nguyên. Có mục tiêu ở xa thì nhân vật tự nhích thêm cho tới tầm.

## Playtest tự động 2026-11-24

Trọng Cước 3/3, Xoáy Cước #2-#3, Thiểm Bộ 3/3 (song phi trúng bằng chân), Hồi Mã #2 đều trúng. Túm Ném: kéo bù nhìn vào 0.8 m, ném văng 3.2 m, cao 2.4 m.

## Ghi lại khi test

Ghi số combo + đòn thứ mấy + hiện tượng (vd "2-#3 trượt chân").

## Đỡ đòn (D-020)

Bù nhìn tự ra đòn mỗi 2.5 s khi Hero trong tầm 2.5 m: chớp **xanh** = đỡ được, chớp **đỏ** = chỉ né được. Đòn chạm sau 0.6 s kể từ lúc chớp.
- Đỡ: bấm R / LB / L1 trong khoảng 0.2 s trước khi đòn chạm -> khựng, zoom, tia lửa, bù nhìn mất 45 thăng bằng, Hero tự ra Phản Đòn. Console: `[HeroParry] đỡ ...`.
- Bấm sớm quá / bấm loạn: khoá 0.35 s, đòn trúng -> `[EnemyAttack] ... đánh trúng ... (xanh)`.
- Đòn đỏ: đỡ không có tác dụng, phải né (E / RT) -> `[PerfectDodge] né chuẩn`.
- Hai lần đỡ trúng + vài đòn là phá thăng bằng (phát sáng vàng) -> bấm Đánh để kết liễu.
