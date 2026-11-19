# Spec: Input chung + nền combat (D-001, D-005)

## Sơ đồ nút

| Hành động | Gamepad | Bàn phím / chuột (đề xuất, chưa chốt) |
|---|---|---|
| Đánh thường | X (West) | Chuột trái |
| Skill (theo hướng) | Y (North) + hướng | Chuột phải + WASD |
| **Cơ chế riêng** (Dante Taunt / Vergil Deflect / Trish Blink) | **B (East)** | **Q** (Trish đang dùng Q để bật/tắt bay, sẽ bỏ vì Trish luôn bay) |
| Né | RT | E |
| Lock-on (giữ) | RB | Chuột giữa (giữ), cân nhắc thêm chế độ bấm-bật |
| Nhảy | A | Space |
| Đổi vũ khí (Dante) | D-pad trái/phải | Tab |
| Awake (Trish, khi đầy Nộ) | LB + hướng tới = Devil / LB + lùi = Angel | R + W / R + S |

## Skill theo hướng (thay cho xoay vòng)

Hướng được tính so với mục tiêu lock-on nếu có, không thì so với hướng camera:
- Đứng yên (stick < 0.3): skill 1
- Đẩy tới (dot > 0.6): skill 2
- Kéo lùi (dot < -0.6): skill 3

## Combo tất định

- Bỏ `RandomGroundChain` và `PickBranch` ngẫu nhiên.
- Dừng nhịp rồi bấm tiếp (delayed) luôn rẽ sang **một** nhánh cố định, khai báo trong data.
- Khoảng dừng nhịp, giá trị khởi điểm: bấm sau khi đòn hết 0.25-0.6s thì tính là delayed.

## Lỗi nền phải sửa trước khi làm kit (từ playtest 2026-11-14)

1. Bấm né giữa đòn: đòn bị hủy nhưng không né. Phải né thật.
2. Skill đầu tiên ra Skill B (con trỏ tăng trước khi chọn). Bỏ hẳn khi chuyển sang skill theo hướng.
3. Launcher không hất mục tiêu: `HitReaction` chỉ đẩy ngang. Cần trạng thái bị hất (airborne) cho mục tiêu.
4. Nhảy rồi đánh luôn trượt: đòn trên không bay cao hơn mục tiêu 2-3m.
5. Skill trượt ở khoảng cách mặc định 1.6m: tầm skill phải khớp `AttackStopDistance`, hoặc lướt tới.
6. Vergil đáp lên đầu mục tiêu sau đòn trên không.
7. Camera cinematic đòn chốt chỉ áp cho cận chiến.

## Mục tiêu độ nhạy (giá trị khởi điểm để tune)

| Thông số | Hiện tại | Mục tiêu |
|---|---|---|
| Bấm đến khi trúng, đòn nhẹ đầu tiên | Dante 0.40s, Vergil 0.81s, Trish 2.0s | 0.20-0.30s cận chiến, <= 0.30s phép ra tay |
| Quay 180° khi đang combat / lock-on | 0.42s | <= 0.20s |
| Buffer input | đến LinkTime | 0.15-0.20s trước mốc nối |
