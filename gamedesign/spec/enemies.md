# Spec: Địch (D-010, D-012)

Vai trò (D-010, đã chốt): Ghost = lính thường, Undead = tinh anh, Oni = boss phụ, Hell Executioner = boss cuối.
Trạng thái từng phần: **Chốt** = có decision; **Đề xuất** = chờ người dùng duyệt.
Mọi con số là giá trị khởi điểm để tune.

---

## 1. Khung chung (Đề xuất)

### Telegraph: 2 mức, nằm trên thân địch
| Mức | Tín hiệu | Người chơi trả lời |
|---|---|---|
| Trắng | Vũ khí lóe trắng **0.25s trước va chạm** (cố định, mọi địch) | Đỡ, deflect (Vergil), né, Blink |
| Đỏ | Vũ khí cháy đỏ + tiếng riêng, bắt đầu từ đầu wind-up | Chỉ né / Blink / nhảy. Deflect và chặn đều không có tác dụng |

Luật trung thực: tia lóe trắng luôn đến đúng 0.25s trước va chạm. Thân địch được phép lừa, tia lóe thì không.

### Phản ứng và poise
| Hạng | Đòn thường của người chơi | Poise | Hất (launcher) |
|---|---|---|---|
| Lính thường | Khựng ở mọi đòn | 0 | Được |
| Tinh anh | Super armor khi đang ra đòn | Thanh poise, vỡ thì choáng 2s | Chỉ khi poise vỡ |
| Boss | Super armor khi đang ra đòn | Thanh poise, vỡ thì choáng 3s | Không |

Deflect (Vergil) gây poise damage gấp 3 một nhát chém thường.

### Nhịp đám đông
- Tối đa 2 địch tấn công cùng lúc (token). Tinh anh tính 1, boss tính 2.
- Địch ngoài màn hình không được tấn công. Địch đang chờ token thì đi vòng, giữ khoảng cách 3-5m.

---

## 2. Ghost Samurai: lính thường (Đề xuất)
- Đàn 3-6 con. Máu: chết sau khoảng 1 combo 4 nhát của Dante.
- Đòn 1: chém 1 nhát, wind-up 0.6s, trắng.
- Đòn 2: lao chém 4m, wind-up 0.8s, trắng. Phạt Trish đứng yên.
- Chết: tan khói, không để xác.
- Anim: rig Mixamo -> Humanoid; idle, đi, chạy, 2 đòn, khựng, bị hất, chết.

## 3. Undead Samurai: tinh anh (Đề xuất)
- 1 con mỗi đợt, đi cùng Ghost. Máu khoảng 4 lần Ghost. Nodachi với tới 3.5m.
- Quét ngang 180° (trắng), chém bổ dọc (trắng).
- Quét vòng 360° (đỏ): phải nhảy hoặc né; hồi 1.2s sau đòn = cửa sổ phạt.
- Tay quái túm (đỏ): né ngang.
- Big Sword của Dante gây poise damage x1.5. Khống chế của Trish chỉ còn 50% thời gian khi poise chưa vỡ.

---

## 4. Oni Samurai: boss phụ, kiếm khách lừa nhịp (Chốt, D-012 thay D-011)

Fantasy: con quỷ thích đùa với con mồi, bị dồn thì lật mặt.
Phase 1 hỏi **"bạn có kiên nhẫn không?"**, phase 2 hỏi **"bạn có trụ được không?"**.
Dao/kiếm ngắn, tầm 1.8m. Luật trung thực xuyên suốt: vũ khí lóe trắng đúng 0.25s trước va chạm thật.

### Phase 1 (100-50% máu): cù nhây, bịp bợm
Oni ít khi mở màn: giữ khoảng cách 3-5m, đi vòng, nhử người chơi tấn công rồi trừng phạt.
| Đòn / hành vi | Mô tả | Mức |
|---|---|---|
| Lùi nhử | Người chơi áp sát thì lùi 1 bước; người chơi chém trượt thì phản ngay 1 nhát | Trắng |
| Đòn giả | Vung lên rồi dừng (không lóe), lùi, rồi đâm. Tỉ lệ 40% | Trắng ở nhát thật |
| Chuỗi 3 nhát | Nhát 3 trễ 0.3-0.6s | Trắng |
| Tàn ảnh | 2 nhát, nhát đầu là ảo ảnh (không lóe, không sát thương) | Nhát thật trắng |
| Đâm xuyên | Lao thẳng 6m | Đỏ |
| Trêu ngươi | Oni taunt lại người chơi, đứng hở 1.2s | **Cửa sổ phạt miễn phí** |

- Người chơi đứng yên quá 3s thì Oni tự lao vào, để hai bên không cùng đứng chờ.
- Sát thương thấp (x0.7): phase này phiền chứ không nguy hiểm, mục đích là chọc tức.
- Dante taunt trong 3m thì Oni phản đòn ngay: trêu nhau là canh bạc của cả hai bên.

### Chuyển phase (50% máu)
Oni bị dồn: gầm, dao bốc lửa đỏ, đẩy người chơi ra 3m, bất tử 1.5s. Nhạc nhảy thẳng lên tầng cao.

### Phase 2 (50-0%): lao vào chém nhau điên cuồng
Hết lừa: không đòn giả, không lùi, áp sát liên tục.
| Thông số | Giá trị |
|---|---|
| Chuỗi | 5-7 nhát, tốc độ x1.25, sát thương x1.3 |
| Nghỉ giữa 2 chuỗi | 0.4s |
| Kết chuỗi | Mỗi chuỗi kết bằng 1 nhát đỏ (chém xoay hoặc đâm) |
| Super armor | Chỉ trong nhát đỏ kết chuỗi; các nhát khác ngắt được |
| Liều mạng | Oni nhận sát thương x1.3 (đổi máu lấy máu) |
| Kiệt sức | Sau 3 chuỗi liên tiếp: thở dốc 2s = cửa sổ phạt lớn |

Poise vỡ: choáng 3s.

### Dành cho từng nhân vật
| Nhân vật | Phase 1 | Phase 2 |
|---|---|---|
| Vergil | Đợi tia lóe, không bấm B bừa trước đòn giả | Deflect cả chuỗi 5-7 nhát: Tĩnh tâm lên rất nhanh |
| Dante | Taunt là rủi ro; phạt khi Oni trêu ngươi | Ngắt chuỗi bằng đòn nặng; đổi vũ khí khi Oni kiệt sức |
| Trish | Oni đứng xa nên dễ bắn, nhưng đâm xuyên phạt đứng yên | Không thả diều lâu được: Blink + Đẩy lùi để sống |

### Prototype (slider cho unity-mechanic)
FlashLead, KeepDistanceMin/Max, WhiffPunish, FeintChanceP1, DelayMin/Max, TauntOpenTime, IdleForceAttack, P1DamageMul, P2ChainMin/Max, P2SpeedMul, P2DamageMul, P2ChainGap, P2DamageTakenMul, ExhaustAfterChains, ExhaustTime, PoiseMax, PoiseStun.

---

## 5. Hell Executioner: boss cuối (Đề xuất)
- **P1 (100-66%):** chùy chậm, đập đất ra vùng có telegraph mặt sàn; đỏ: nhảy đập.
- **P2 (66-33%):** nham thạch lan, thu hẹp sàn; gọi 2-3 Ghost.
- **P3 (33-0%):** combo dài; đỏ: "Hành quyết" túm rồi đập. Nhạc L3 lên đỉnh.
- Đã có rig Humanoid và 2 anim.

## Thứ tự làm (Đề xuất)
Ghost -> Undead -> Oni -> Executioner.

## Mở
- Rig của Oni và Undead (đang import Generic).
- Nguồn anim cho 3 model chưa có anim (Mixamo / retarget clip người chơi).
