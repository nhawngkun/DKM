# Danh sách VFX cần làm - DKM
Updated: 2026-11-14 | Dựa trên: D-002, D-003, D-004, D-005, D-006 | Art direction: `art-direction.md` | Hình dạng hạt: `concept-sheet.html` mục 3

Quy tắc chung (từ art direction):
- **Thứ tự độ sáng:** telegraph địch và tia lửa deflect > đòn chốt / Awake > vệt chém thường > nhân vật > HUD > thế giới. Chỉ telegraph và deflect được dùng lõi trắng nóng `#FFF6DC`.
- **Mỗi chủ một màu:** Dante đỏ thẫm, Vergil lam, Trish tím, mối nguy cam `#FF6A1A`.
- **Mỗi nhân vật một hình dạng hạt:** Dante tia lửa + mảnh vỡ nhọn; Vergil đường cắt thẳng, rất ít hạt; Trish hạt tròn + vòng ký hiệu + khói mềm. Devil gai nhọn + khói tím đen; Angel lông vũ tròn + tia thẳng đứng.
- **Vùng hiệu ứng vẽ từ đúng giá trị config gameplay** (bán kính, độ dài), không ước lượng bằng mắt.
- Làm theo skill `unity-vfx` / `vfx-core` + `vfx-combat` khi bắt tay vào Unity. Pool mọi VFX lặp lại.

Ưu tiên theo D-005: Dante → địch → Vergil → Trish. Riêng V-09 (sửa vệt đạn Trish) là lỗi đang có, đã giao S3 (B-11).

| ID | VFX | Nhân vật | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| V-01 | Taunt | Dante | 1 | Cần làm (chờ anim taunt) |
| V-02 | Flash lên rank SSS (trong HUD) | Dante | 1 | Cần làm (UI) |
| V-03 | Telegraph đòn địch (lóe vũ khí + vòng mặt đất) | Địch | 2 | Cần làm |
| V-04 | Tia lửa Deflect | Vergil | 3 | Cần làm |
| V-05 | Kiếm phát sáng ở Tĩnh tâm cấp 3 | Vergil | 3 | Cần làm |
| V-06 | Vệt Iai (vệt mực lam) | Vergil | 3 | Cần làm |
| V-07 | Blink (điểm đi + điểm đến) | Trish | 4 | Cần làm |
| V-08 | Awake Devil + Awake Angel (dùng chung anim) | Trish | 4 | Cần làm, Devil trước |
| V-09 | Sửa vệt đạn phép đang hồng tím | Trish | Ngay | S3 đang sửa material (B-11); art cấp màu + hình |
| V-10 | Telegraph mặt đất Trói / Đẩy lùi | Trish | 4 | Cần làm |

---

## V-01 Taunt (Dante)
- **Mục đích:** biến taunt thành một khoảnh khắc "khoe", báo cho người chơi biết taunt đã ăn Style (D-002: chỉ thưởng khi chạy hết anim).
- **Thời điểm:** hạt nổi lên suốt anim, nhưng **phần thưởng chỉ bật khi anim chạy hết**: vòng sóng đỏ bung từ chân + tia lửa đỏ san hô hất lên. Bị đánh hoặc tự hủy thì tắt ngay, không có vòng bung.
- **Đọc gameplay:** cỡ vòng bung theo tầm thưởng: mục tiêu trong 3 m thì vòng to và sáng (+40 Style), 3-8 m thì vòng nhỏ (+15).
- **Màu / hình:** đỏ thẫm `#C8202E`, viền đỏ san hô `#FF5A4E`. Tia lửa và mảnh vỡ nhọn, không dùng hạt tròn.
- **Ngân sách:** ngắn (≤ 0.5 s cho phần bung), không che mặt nhân vật.
- **Phụ thuộc:** 1-2 clip taunt (đang thiếu, D-002 Open).

## V-02 Flash lên rank SSS (Dante, HUD)
- Chữ rank nảy (scale 1.0 → 1.25 → 1.0), đổi sang đỏ san hô, tia flash bung từ chữ, ≤ 0.3 s (spec ui-hud). Đồng bộ âm thanh UI và tầng nhạc L3 (D-007).
- Các bậc thấp hơn: chỉ nảy nhẹ, không tia. SSS phải là khoảnh khắc riêng.
- Làm bằng uGUI/animation theo `ui-panel` + `vfx-ui`, không phải particle trong thế giới.

## V-03 Telegraph đòn địch (dùng chung cho mọi địch)
- **Hai lớp:** (1) lóe sao trắng nóng + quầng cam đúng ở bộ phận sắp đánh (móng, vũ khí), (2) vòng mặt đất cam đúng bán kính / hình dạng vùng sát thương.
- **Thời điểm:** bắt đầu ở đầu windup, lóe sáng nhất đúng lúc mở cửa sổ deflect của Vergil (để deflect học được bằng mắt), tắt khi đòn kết thúc.
- **Đọc gameplay:** sáng hơn mọi VFX khác trên màn hình (spec). Không dựa chỉ vào màu: lóe sao + vòng nhấp nháy có hình dạng riêng.
- **Phụ thuộc:** loại địch đầu tiên (D-005) chưa thiết kế.

## V-04 Tia lửa Deflect (Vergil)
- **Mục đích:** xác nhận deflect thành công tức thì; phải đọc rõ hơn mọi VFX khác (spec Vergil).
- **Hình:** chùm tia lửa thẳng, ngắn, bắn theo hướng ngược đòn địch + một chớp sáng tròn nhỏ ở điểm kim loại chạm nhau. Lõi trắng nóng `#FFF6DC`, đuôi tia ngả vàng rồi lam băng `#9FD8FF` (màu Vergil).
- **Thời điểm:** xuất hiện ngay frame deflect, sống 0.15-0.25 s, cùng hitstop và stinger kim loại (D-007: nhạc chìm 0.3 s rồi tiếng kim loại).
- **Phân biệt:** chặn thường (giữ B, ngoài cửa sổ) chỉ có vài tia nhỏ, mờ, không chớp sáng, để người chơi biết mình chặn chứ không deflect.
- Tạm thay anim parry còn thiếu (D-003 Open): VFX + hitstop đang gánh phần lớn cảm giác.

## V-05 Kiếm Vergil phát sáng ở Tĩnh tâm cấp 3
- **Hiển thị trong thế giới** (D-006): lưỡi kiếm emissive lam băng `#9FD8FF`, vệt mờ bám lưỡi khi vung. Ở cấp 1-2 không sáng.
- **Vào cấp 3:** một chớp sáng chạy dọc lưỡi từ chuôi ra mũi (≤ 0.3 s), cùng lúc 3 vạch HUD sáng lên.
- **Rời cấp 3** (bị đánh, tiêu Iai): ánh sáng tắt dần 0.2 s.
- Kiếm trong vỏ thì phát sáng qua khe chuôi / miệng vỏ, đủ để thấy từ sau lưng.

## V-06 Vệt Iai (Vergil)
- Đường cắt thẳng, rất mảnh, để lại vệt "mực lam" đứng yên trong không khí khoảng 0.4 s rồi tan như mực loang (signature nét cọ).
- Cấp 1-2: một đường. Cấp 3 (chém vùng, signature move): nhiều đường cắt chéo xuất hiện cùng lúc trong vùng, đúng bán kính gameplay của đòn vùng.
- Ít hạt. Vergil nổi bật nhờ sự tiết chế, không nhờ lượng hạt.

## V-07 Blink (Trish)
- Không có anim (spec Trish): ẩn model + VFX ở hai đầu.
- **Điểm đi:** model tan thành hạt tròn tím + một vòng ký hiệu tím co lại. **Điểm đến:** vòng ký hiệu nở ra, hạt hội tụ lại thành model.
- Một vệt mờ mảnh nối hai điểm (0.1 s) để người chơi thấy mình đã đi đâu, nhất là khi Blink ra sau (không đẩy stick).
- Ngắn: Blink hồi 0.6 s, tích 2 lần, nên VFX ≤ 0.3 s mỗi đầu và không chồng chất khi bấm 2 lần liền.
- Màu: tím `#8A4BFF`, lõi tím nhạt `#D9C2FF`. Trong Awake: đổi theo bảng màu Devil / Angel.

## V-08 Awake Devil và Awake Angel (Trish, dùng chung anim)
Hai bộ cùng **cấu trúc** (để dùng chung anim và code), khác **màu và hình dạng hạt** (spec Trish: phân biệt bằng màu và hình).

| Thành phần | Devil (làm trước) | Angel (làm sau) |
|---|---|---|
| Kích hoạt | Cột lửa đỏ đen dâng từ dưới chân, gai nhọn bung ra | Cột sáng trắng ngà từ trên xuống, lông vũ tròn tỏa ra |
| Hào quang khi Awake | Gai nhọn đỏ máu `#B0122A` + khói đặc tím đen `#2A0A3A` quanh Trish | Tia thẳng đứng vàng `#F2C14E` + lông vũ tròn trắng ngà `#FFF4DA` |
| Đạn X | Đạn to hơn, đỏ máu, nổ lan bán kính 1.5 m (vẽ đúng 1.5 m) | Đạn trắng ngà, trúng thì để vòng làm chậm vàng nhạt trên địch 2 s |
| Skill | Như thường, đổi màu sang đỏ / tím đen | Vùng to x1.5 (vẽ đúng bán kính mới), màu trắng / vàng |
| Kết thúc | Nổ lớn quanh Trish, sóng gai | Vòm làm chậm thời gian 3 s, viền vàng, hạt bên trong rơi chậm |
| Tóc + vòng phép | Đổi đỏ máu | Đổi trắng ngà |

- HUD: vòng Nộ đổi màu theo nhánh (mockup trong concept sheet).
- Hào quang không được che địch đứng sát Trish: giữ dưới thắt lưng và mờ dần ra ngoài.

## V-09 Sửa vệt đạn phép Trish đang hồng tím
- Lỗi hiện tại: thiếu `TrailMaterial` → màu hồng tím báo lỗi (B-11, **S3 đang sửa**, `Combat/SpellCaster.cs`).
- **Màu đích:** vệt tím `#8A4BFF`, lõi đạn tím nhạt `#D9C2FF`, additive, mờ dần về đuôi. Vệt ngắn (≈ 0.15 s) để bắn nhanh 4 phát/s không thành một dải liền.
- Hạt tròn nhỏ rơi rải dọc vệt (hình dạng hạt của Trish), không dùng tia lửa nhọn (của Dante).
- Phân biệt với vệt chém đỏ của Dante cả bằng màu lẫn độ dày (vệt đạn mảnh và tròn đầu).

## V-10 Telegraph mặt đất Trói / Đẩy lùi (Trish)
- Vòng mặt đất tím, **đúng bán kính gameplay**: Trói 3 m, Đẩy lùi 4 m. Thương xuyên: dải thẳng 10 m.
- Đây là telegraph của người chơi: dùng tím Trish, **không** dùng cam (cam dành cho mối nguy từ địch).
- Trói: vòng ký hiệu khép lại + xích phép tròn; Đẩy lùi: sóng nova nở ra.

---

## Bàn giao
- Khi làm trong Unity: đọc `vfx-core` → `vfx-combat`, dùng script kiểm tra bán kính (`vfx-fit`) cho V-03, V-06 cấp 3, V-08, V-10.
- Câu hỏi cho design lead: hình dạng vùng đòn của loại địch đầu tiên (để vẽ V-03).
