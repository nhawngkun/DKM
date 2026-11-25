# Ứng viên anim đánh đường phố (Street Brawl) - 2026-11-24

Nguồn: `Assets/_Other anim/SpiderFighter3Animation` + `SpriderFighting` (rip từ game Spider Fighter 3). Tất cả **Humanoid**, đo trên AvatarV10.
**Rủi ro bản quyền:** asset rip từ game thương mại (giống Naraka). Chỉ dùng prototype, phải thay trước khi phát hành.

Cột: dài (s) · lướt (m, hông đầu→cuối) · tay/chân vươn tới trước tối đa (m so với hông) · chân lên (m so với hông, >0 = đá cao).
Chưa xem tận mắt: tên đòn suy từ tên clip + số đo.

## Đấm (lướt ≤ 2 m)

| Clip | Dài | Lướt | Tay | Ghi chú |
|---|---|---|---|---|
| hulk/hulk_BaseCombo_01..04 | 0.47-0.90 | 0.9-1.8 | 0.77-0.89 | chuỗi đấm nặng 4 nhịp, chân trụ đất |
| Venom/Venom_BaseCombo_01_01..04 | 0.33-1.20 | 0.2-1.2 | 0.64-0.95 | chuỗi 4 đòn, tay vươn xa |
| IronMan/IronMan_BaseCombo (1)..(5) | 0.30-1.00 | 0-1.5 | 0.43-0.86 | chuỗi 5 đòn ngắn |
| Cap/Cap_BaseComboHands_01/02 | 0.50 | 0.8-1.2 | 0.63-0.80 | đấm 1-2 mở chuỗi |
| Spider/A_Spider_BaseCombo_01/02_Hand | 0.40-0.43 | 0.4-1.4 | 0.67-0.76 | |
| SpriderFighting/Combo/Combo_Punch_01_1..3 | 1.2 | 1.6-2.2 | 0.8 | |
| hulk/hulk_RunningPunch_Fatality02_* | | 0.9-3.0 | 0.93 | lao đấm |

## Đá

| Clip | Dài | Lướt | Chân | Chân lên |
|---|---|---|---|---|
| Cap/Cap_BaseComboLegs_03/04/05 | 0.50-1.23 | 0.7-1.5 | 0.75-0.96 | +0.5..+0.7 |
| SpriderFighting/Attack/Attack_Kick_A/B | 0.93-0.97 | 0 | 0.86-0.91 | +0.3..+0.4 |
| Spider/A_Spider_BaseCombo_01/02/03_*_Leg | 0.57-1.07 | 0.7-5.5 | 0.38-0.96 | |
| Spider/A_Spider_DashAttack01Attack | 0.50 | 0.8 | 0.89 | +0.88 (đá bay ngắn) |

## Vật / túm / ném (cần hệ grapple mới)

| Clip | Ghi chú |
|---|---|
| hulk/Hulk_ThrowOne_Take → Hulk_ThrowOne_Throw | túm rồi ném, tại chỗ |
| Spider/A_Spider_YankThrow_Pull → Rotate_loop → Rotate_to_throw → Throw | kéo, quay, ném |
| Spider/A_Spider_Fatality_KickTossUkick (2.4 s) | túm - tung - đá |
| Spider/A_Spider_Fatality_KneeFinish_* | lao gối kết liễu |
| IronMan_FatalityXX_attack + _victim + _standup | **cặp đòn kết liễu có anim nạn nhân** (cutscene liền mạch) |
| Boss/Boss_FatalityXX_victim_* | anim nạn nhân cho boss |

## Không dùng (dấu ấn siêu anh hùng)

Cap ném khiên, Spider tơ/đu tơ, Wolverine vuốt, Venom xúc tu, IronMan bay - copy signature, trái anti-pillar.
