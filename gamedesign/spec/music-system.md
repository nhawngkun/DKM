# Spec: Hệ thống nhạc combat (D-007)

Brief cho music-lead và dev. Music-lead soạn nhạc theo cấu trúc này; dev dựng hệ phát.

## Trạng thái game

| Trạng thái | Nhạc |
|---|---|
| Khám phá | Nhạc nền nhẹ (chưa thiết kế, sau) |
| Vào trận | Khung combat L0 vào ở đầu ô nhịp kế |
| Trong trận | L1-L3 theo thanh nhân vật (bảng dưới) |
| Hết địch | Outro 2-4 ô nhịp, về khám phá |
| Boss | Sau này nâng lên bài riêng 4 tầng + lời hát ở cao trào (theo phương án B đã để dành) |

## Tầng

| Tầng | Nội dung | Dante (Style) | Vergil (Tĩnh tâm) | Trish (Nộ) |
|---|---|---|---|---|
| L0 | Trống / nhịp nền | Vào trận | Vào trận | Vào trận |
| L1 | Bass + riff | Rank B | Cấp 1 | >= 33% |
| L2 | Hòa âm / pad | Rank A-S | Cấp 2 | >= 66% |
| L3 | Giai điệu riêng nhân vật (cao trào) | Rank SS-SSS | Cấp 3 | Awake: biến thể Devil hoặc Angel |

- L0-L2 dùng chung cho cả 3 nhân vật: cùng tempo, cùng giọng, cùng độ dài vòng lặp (32 ô = 51.2s, D-010).
- L3 là leitmotif của từng nhân vật, viết trên cùng hòa âm của L2 để lắp vào được.
- Đổi nhân vật giữa trận: L0-L2 chạy tiếp, L3 đổi ở đầu ô nhịp kế.

## Quy tắc chuyển

- Thêm hoặc bỏ tầng chỉ ở **đầu ô nhịp** (fade 1 phách).
- Thanh tụt dưới ngưỡng phải giữ **4s** mới bỏ tầng (chống nhạc nhấp nháy).
- Bị đánh trúng mạnh: không bỏ tầng ngay, chỉ theo quy tắc trên.

## Stinger

| Sự kiện | Âm thanh |
|---|---|
| Dante lên SSS | Stinger ngắn (1 ô nhịp) trên L3 |
| Vergil deflect | Nhạc chìm khoảng -12dB trong 0.3s, rồi tiếng kim loại riêng, rồi nhạc về |
| Vergil Iai cấp 3 | Khoảng lặng 0.5s, rồi nhát chém, rồi nhạc vào lại trên phách mạnh |
| Trish kích hoạt Awake | Chuyển L3 sang biến thể Devil (nặng, méo tiếng) hoặc Angel (hợp xướng, sáng) |

## Câu hỏi music-lead sẽ hỏi người dùng (bước định hướng)
1. Màu sắc âm nhạc: rock/metal kiểu DMC có pha nhạc cụ Nhật (shamisen, shakuhachi, taiko) để hợp FeudalMap không?
2. L3 có lời hát không?
3. Bài tham khảo người dùng thích / ghét.

## Cho dev
- Phát stem đồng bộ: Unity `AudioSource.PlayScheduled` theo đồng hồ `AudioSettings.dspTime`, hoặc FMOD. Chưa chốt.
- Hệ nhạc đọc 3 thanh qua sự kiện (Style rank đổi, cấp Tĩnh tâm đổi, Nộ vượt ngưỡng, Awake bật/tắt), không tự tính lại.
