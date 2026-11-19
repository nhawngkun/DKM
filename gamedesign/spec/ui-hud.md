# UI spec - Combat HUD (D-006)

Purpose: cho người chơi biết máu, tài nguyên của nhân vật và tình trạng mục tiêu, mà không kéo mắt khỏi nhân vật.
Player state: combat (HUD đầy đủ) / ngoài combat (HUD mờ dần).

## Elements

| Element | Tầng | Vị trí | Hiển thị | Hiện khi | Tắt khi |
|---|---|---|---|---|---|
| Máu người chơi | Luôn có | Trên trái | Thanh ngang, đoạn mất máu đỏ rồi rút chậm | Luôn trong combat | Mờ 3s sau khi hết combat |
| Widget tài nguyên | Luôn có | Ngay dưới / cạnh máu | Theo nhân vật (bảng dưới) | Như máu | Như máu |
| Style rank (Dante) | Khi cần | Cạnh phải, giữa dọc | Chữ D C B A S SS SSS + thanh tiến độ mảnh | Có điểm Style > 0 | Rank về 0 |
| Icon skill x3 | Khi cần | Dưới trái, cạnh máu (tạm, xem Open) | 3 icon theo hướng: đứng yên / tới / lùi, vòng hồi chiêu | Có skill đang hồi chiêu | Hồi xong + 1s |
| Máu địch | Khi cần | Trên đầu địch đang lock-on | Thanh nhỏ | Lock-on hoặc vừa đánh trúng | 2s sau bỏ lock-on / không trúng |
| Thanh boss | Khi cần | Dưới giữa | Tên + thanh dài + vạch phase | Vào trận boss | Boss chết |
| Vòng lock-on | Trong thế giới | Trên mục tiêu | Vòng xoay | Lock-on | Bỏ lock-on |
| Mũi tên địch ngoài màn hình | Trong thế giới | Quanh nhân vật | Mũi tên, đỏ khi địch sắp đánh | Địch ngoài khung đang tấn công | Hết nguy hiểm |
| Telegraph đòn địch | Trong thế giới | Trên địch / mặt đất | Lóe sáng + vùng sát thương đúng bán kính | Trước đòn địch | Đòn kết thúc |
| Số sát thương | Không bao giờ | - | - | - | - |

## Widget tài nguyên theo nhân vật

| Nhân vật | Hình dạng | Trạng thái đặc biệt |
|---|---|---|
| Dante | Chữ rank lớn bên phải (widget chính); dưới máu chỉ có 1 vạch Style mảnh | Lên SSS: chữ nảy + rung nhẹ + flash |
| Vergil | 3 vạch mảnh dưới máu, không chữ, không màu chói | Cấp 3: vạch sáng, **kiếm Vergil phát sáng trong thế giới** |
| Trish | Vòng tròn quanh chân dung nhỏ cạnh máu | Đầy: vòng xoay + gợi ý "LB + ↑ Devil / LB + ↓ Angel"; Awake: vòng đổi màu + **hào quang quanh Trish** |

## Interactions
- Không có tương tác trực tiếp. Tùy chọn trong menu: ẩn HUD (chỉ giữ máu khi máu < 30%), độ trong suốt HUD, cỡ HUD.

## Feedback
- Mất máu: thanh rung 0.1s + vệt đỏ rút chậm 0.6s.
- Lên rank / lên cấp Tĩnh tâm / Nộ đầy: animation ngắn <= 0.3s, có âm thanh UI riêng (đồng bộ với tầng nhạc ở D-007).

## Accessibility
- Không mã hóa thông tin chỉ bằng màu: mỗi trạng thái có hình dạng hoặc chữ đi kèm.
- Chữ đọc được trên TV ở khoảng cách 3m (tham khảo Xbox Accessibility Guidelines). Cỡ cụ thể do art-lead đề xuất.
- HUD đọc được trên cả cảnh sáng nhất (sân đá trắng) và tối nhất (đêm FeudalMap): dùng viền hoặc đổ bóng.

## Art notes cho art-lead
- Tham khảo: God of War (góc tối giản), DMC (chữ rank). Không sao chép bố cục; phong cách theo từng nhân vật: Dante phô trương, Vergil tối giản, Trish huyền bí.
- Telegraph phải sáng hơn mọi VFX khác trên màn hình.

## Open
- Icon skill đặt cạnh máu hay góc dưới phải.
