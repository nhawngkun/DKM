# Art direction - DKM
Updated: 2026-11-14 | Based on: D-001, D-002, D-003, D-004, D-005, D-006 | Status: **Agreed 2026-11-14** (silhouette A/A/A)

## Mood
**"Đền cổ im lặng, chỉ người chơi mang màu."** Thế giới là đền gỗ Nhật về đêm / chạng vạng, rêu và
đá xám, sương thấp, gần như đơn sắc. Màu bão hòa chỉ xuất hiện trên nhân vật, VFX của họ và mối
nguy. Đánh càng giỏi, màn hình càng nhiều màu (rank SSS, kiếm Vergil sáng, hào quang Awake): màu là
phần thưởng, đúng tinh thần D-006 / D-007.

## Camera and scale
- Góc ba người hành động, camera sau vai, lock-on (`CameraController`). Nhân vật cao khoảng
  **1/4 - 1/3 chiều cao màn hình** khi đánh (mục tiêu ở review: nhân vật ở 1/3 dưới khung hình).
- Nền tảng: PC + tay cầm, 16:9, mốc thiết kế 1920x1080. Chữ HUD phải đọc được trên TV ở 3m.
- Cỡ chữ đề xuất ở 1080p: chữ nhỏ nhất 28 px; tên boss 32 px; chữ rank Dante 150-180 px
  (SSS 200 px khi nảy). Viền 2 px + đổ bóng mềm cho mọi phần HUD (spec: đọc được trên sân đá trắng
  lẫn đêm).

## Shape language
| Đối tượng | Hình khối chủ đạo | Ý nghĩa gameplay |
|---|---|---|
| **Dante** | **Tam giác, bất đối xứng.** Vạt áo khoác loe rộng, vai to, Big Sword tạo một khối nặng chéo sau lưng; Katana là một nét mảnh dài. | Phô trương, cuồng bạo. Hai vũ khí phân biệt bằng **khối vs nét** (spec Dante). |
| **Vergil** | **Đường thẳng đứng, khối đóng.** Áo dài thẳng, vai hẹp gọn, katana trong vỏ là một đường chéo duy nhất. Ít chi tiết bay. | Tĩnh, chính xác. Lúc tra kiếm (Iai) silhouette "đóng lại" thành một khối liền. |
| **Trish** | **Tròn và cong, lơ lửng.** Tóc dài và vạt váy chảy xuống thành hình giọt nước ngược; vòng phép tròn quanh tay; chân không chạm đất. | Pháp sư bay, khống chế vùng tròn (Trói, Đẩy là vùng tròn). |
| **Địch** (chưa thiết kế, D-005) | **Răng cưa, gù, khớp gãy.** Màu tối, thấp sáng, điểm phát sáng chỉ ở chỗ sắp đánh. | Đọc ngược với người chơi; telegraph là thứ sáng nhất trên người địch. |
| **Thế giới** | Mái cong, cột thẳng, đèn lồng đá, lá phong. Khối lớn, mặt phẳng rộng, ít chi tiết nhỏ ở tầm mắt. | Nền yên để nhân vật nổi. |
| **UI** | **Nét mực + đường chỉ mảnh.** Một nét cọ mực làm nền thanh máu; khung là đường chỉ ngà 1-2 px, góc vát 45°. Không ornament trong combat. | HUD im lặng (D-006). Mỗi widget nhân vật mang hình khối của chính nhân vật đó. |

## Values
- Thế giới giữ trong dải giá trị **15-55%** (tối-trung), ít tương phản nội tại.
- Nhân vật có điểm sáng nhất và tối nhất trên người (da/tóc sáng, áo tối) để tách khỏi nền.
- **Thứ tự độ sáng trên màn hình** (sáng nhất trước): telegraph địch và tia lửa deflect > VFX
  đòn chốt / Awake > vệt chém thường > nhân vật > HUD > thế giới. Spec yêu cầu telegraph và
  deflect "sáng hơn mọi VFX khác": hai thứ này được giữ quyền dùng trắng nóng (#FFF6DC) ở lõi.

## Palette
| Vai trò | Hex | Tên | Ghi chú |
|---|---|---|---|
| Dante accent | `#C8202E` | Đỏ thẫm | Áo khoác, chữ rank, vệt chém (vệt chém đỏ hiện tại giữ nguyên) |
| Dante highlight | `#FF5A4E` | Đỏ san hô | Flash lên rank, viền sáng của vệt chém |
| Vergil accent | `#2F5FD0` | Lam thép | Áo, vạch Tĩnh tâm |
| Vergil glow | `#9FD8FF` | Lam băng | Kiếm cấp 3, vệt Iai |
| Trish accent | `#8A4BFF` | Tím phép | Đạn, vệt đạn (thay màu hồng-tím lỗi) |
| Trish glow | `#D9C2FF` | Tím nhạt | Lõi đạn, vòng Nộ đầy |
| Awake Devil | `#B0122A` + `#2A0A3A` | Đỏ máu + tím đen | Hạt sắc nhọn, khói đặc |
| Awake Angel | `#FFF4DA` + `#F2C14E` | Trắng ngà + vàng | Hạt tròn, lông vũ, tia thẳng |
| Mối nguy (telegraph, mũi tên) | `#FF6A1A` | Cam than hồng | Khác đỏ thẫm Dante cả về sắc độ lẫn độ sáng; luôn kèm hình dạng (vòng, nháy) |
| Lõi sáng nhất | `#FFF6DC` | Trắng nóng | Chỉ cho lõi telegraph và tia lửa deflect |
| Gỗ đền | `#3B2A22` | Gỗ cháy | Thế giới |
| Đá / sân | `#8C8A84` | Đá xám | Thế giới, dải sáng nhất của nền |
| Rêu | `#4E5B3A` | Rêu | Thế giới |
| Sơn đền | `#7E3A2E` | Son cũ | Màu son của cổng đền, **giảm bão hòa** để không tranh với Dante |
| Đêm | `#1B2233` | Chàm đêm | Bầu trời, bóng |
| UI nền | `#0E0E10` @ 60% | Mực | Nét cọ dưới thanh máu |
| UI chính | `#EDE6D6` | Ngà | Chữ, đường chỉ, thanh máu |
| UI mất máu | `#D9473A` | Đỏ cảnh báo | Đoạn máu vừa mất, rút chậm 0.6 s |

Ánh sáng: key light ấm (đèn lồng, trăng vàng nhạt), bóng lạnh chàm. Nhân vật nhận rim light lạnh
để tách nền.

## Materials and rendering
- **HDRP PBR hiện thực có điều tiết** (khớp asset đền và nhân vật hiện có): vải dày, da thuộc,
  kim loại xước. Không cel shading, không outline.
- VFX là phần duy nhất "phi hiện thực": emissive HDR, bloom, hạt sắc. Mỗi nhân vật một **hình dạng
  hạt** riêng: Dante = tia lửa và mảnh vỡ góc cạnh; Vergil = đường cắt mảnh thẳng, ít hạt; Trish =
  hạt tròn, vòng ký tự, khói mềm.
- UI phẳng, không gradient bóng bẩy; texture duy nhất là nét cọ mực.

## Detail
- Chi tiết đặt ở: mặt, tay cầm và lưỡi vũ khí, vật đặc trưng (vỏ kiếm Vergil, vòng phép Trish),
  vùng trúng đòn của địch.
- Không đặt chi tiết ở: mặt phẳng lớn của nền, nền HUD, thân sau của áo khoác.

## Rules
1. Một màu bão hòa = một chủ. Đỏ thẫm của Dante, lam của Vergil, tím của Trish, cam của mối nguy.
   Thế giới không được dùng 4 màu này ở độ bão hòa cao.
2. Không mã hóa thông tin chỉ bằng màu: mỗi trạng thái có hình dạng hoặc chữ đi kèm (spec).
3. HUD: đường chỉ 2 px, góc vát 45°, chữ không chân (đậm cho số liệu), chữ rank là ngoại lệ duy nhất
   có phong cách nét cọ.
4. Telegraph đúng bán kính gameplay, vẽ từ cùng giá trị config (Trói 3 m, Đẩy 4 m).
5. Devil và Angel khác nhau bằng màu **và** hình dạng hạt (spec Trish).

## Signature
**Nét cọ mực.** Thanh máu nằm trên một nét cọ mực, chữ rank của Dante là nét cọ đỏ, đường cắt Iai
của Vergil để lại một vệt mực lam trong không khí, vòng Nộ của Trish là một vòng ensō vẽ dần. Một
motif chung nối ba tính cách với bối cảnh đền Nhật.

## References
| Tham khảo | Lấy | Tránh |
|---|---|---|
| Devil May Cry 5 | Rank lớn ở cạnh phải là HUD phần thưởng; VFX đỏ-xanh tương phản giữa hai anh em | Thiết kế nhân vật, logo, kiểu chữ gothic của rank, đồ họa áo khoác |
| Ghost of Tsushima | Ánh sáng và sương của bối cảnh Nhật, màu là cảm xúc, HUD gần như vắng | Bố cục UI, motif gió dẫn đường |
| Sekiro | Tia lửa deflect ngắn, sáng, đọc được ngay; ký hiệu nguy hiểm trên địch | Ký tự kanji cảnh báo của nó, thiết kế nhân vật |
| God of War (2018 / Ragnarök) | HUD gom ở góc, chế độ ẩn HUD | Khung rune và chất liệu Bắc Âu |
| Tranh mực sumi-e, thư pháp ensō | Nét cọ, khoảng trống, vòng ensō | Chữ Hán/kana thật trong UI (dễ sai nghĩa) |
