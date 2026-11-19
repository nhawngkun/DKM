# Character: Vergil (D-003)

Role: nhân vật chơi được, lối kỹ thuật
Fantasy: kiếm sĩ không thừa một động tác, đợi đối thủ sơ hở rồi kết thúc bằng một nhát
Tính cách: điềm tĩnh, kiêu hãnh, tiết chế.
Điểm yếu: không có launcher, ít đòn trên không, bấm bừa thì yếu.

## Kit

### Đánh thường (X): tối giản
| Input | Đòn | Clip |
|---|---|---|
| X X X | Chuỗi chính | `combo01` 1-3 |
| X (dừng nhịp) X X X | Chuỗi nặng | `combo04` từ đòn 2 |
| Lock + tới + X | Lướt chém | `attack01` (lướt bằng lunge) |
| Trên không X | Một nhát | `attack_jumpATK` (giới hạn độ cao, không được đáp lên đầu mục tiêu) |

Không có launcher. Lock + lùi + X ra `attack02` (nhát lùi rồi chém), không hất.

### Cơ chế riêng: B = Đỡ / Deflect
| Trạng thái | Điều kiện | Kết quả |
|---|---|---|
| Deflect | Bấm B, đòn địch chạm trong cửa sổ | Không mất máu, địch khựng, mở cửa sổ phản đòn, +Tĩnh tâm lớn |
| Chặn | Giữ B, đòn chạm ngoài cửa sổ | Mất ít máu (chip), không thưởng, -Tĩnh tâm nhỏ |
| Phản đòn | X trong cửa sổ phản đòn | Nhát phản nhanh (`attack03` tăng tốc), gây khựng, +Tĩnh tâm |

Giá trị khởi điểm để tune:
- Cửa sổ deflect: 0.15s (khoảng 9 frame ở 60fps), tính từ lúc bấm.
- Cửa sổ phản đòn: 0.6s.
- Hồi chiêu deflect khi bấm trượt: 0.35s, để chống spam B.

### Thanh Tĩnh tâm (3 cấp)
| Nguồn | Điểm (khởi điểm) |
|---|---|
| Deflect | +35 |
| Phản đòn trúng | +25 |
| Chém trúng | +5 |
| Chém trượt (cửa sổ chém qua mà không trúng gì) | -8 |
| Bấm X khi không có gì để nối (spam) | -3 |
| Bị đánh | -40 |

- 100 / 200 / 300 điểm tương ứng cấp 1 / 2 / 3. Không tự hồi khi đứng yên.
- Sát thương nhân: cấp 1 1.0, cấp 2 1.15, cấp 3 1.3.

### Skill (Y + hướng)
- Đứng yên: **Iai**. Tra kiếm (`wp_unequip`) rồi rút chém. Tiêu toàn bộ thanh, sát thương theo cấp. Ở cấp 3 là đòn chém vùng (signature move).
- Tới: lướt xuyên qua mục tiêu (`combo02` đòn đầu, tăng tốc).
- Lùi: lùi lại tra kiếm, hồi một ít Tĩnh tâm (`avoid_back` + `wp_unequip`). Đây là động tác "lấy lại bình tĩnh".

## Anim còn thiếu
- Deflect thành công: tạm dùng `defense01` cắt ngắn, thêm VFX tia lửa và hitstop.
- Phản đòn riêng: tạm dùng `attack03` tăng tốc.

## Phụ thuộc
- Cần địch có telegraph và cửa sổ đòn đánh rõ ràng (D-005).

## Gameplay reads cho art-lead
- Tia lửa deflect sáng và ngắn, phải đọc được rõ hơn mọi VFX khác trên màn hình.
- HUD Tĩnh tâm tối giản: 3 vạch, không chữ.

## Brief nhạc cho music-lead
- Emotional core: căng, kiểm soát. Theme tiết chế, có khoảng lặng. Deflect có stinger kim loại riêng.

## Prototype (slider cho unity-mechanic)
DeflectWindow, CounterWindow, DeflectWhiffCooldown, ChipDamage, FocusPerDeflect, FocusPerCounter, FocusPerHit, FocusWhiffPenalty, FocusHitPenalty, IaiDamageByLevel.
