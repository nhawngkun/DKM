# Character: Trish (D-004)

Role: nhân vật chơi được, tầm xa
Fantasy: pháp sư lơ lửng điều khiển chiến trường, khóa đối thủ rồi xóa sổ
Tính cách: lạnh lùng, quyến rũ, áp đảo.
Điểm yếu: yếu khi bị áp sát, phải giữ khoảng cách bằng Blink hoặc Đẩy lùi.

## Trạng thái di chuyển
- Luôn bay ở độ cao thấp (`AlwaysFly`). Không có chế độ đi bộ, bỏ việc Q bật/tắt bay.
- Bỏ đòn làm Trish đứng khựng lâu: mọi đòn phải hủy được bằng Blink sau mốc cast.

## Kit

### Đánh thường (X)
| Input | Đòn | Clip |
|---|---|---|
| X X X | Đạn phép nhanh, tự bám | `attack_FlyingAtk01_short`, `attack_FlyingAtk03_short` xen kẽ |
| Giữ X 0.6s rồi thả | Nạp năng lượng, nổ lớn (hủy diệt) | `attack_FlyingAtk05` |
| Lock + tới + X | Tia xuyên tầm xa | `attack_FlyingAtk02` |

Giá trị khởi điểm để tune: phép ra tay <= 0.30s sau khi bấm; nhịp bắn tối đa 4 phát/s.

### Skill (Y + hướng): khống chế
| Input | Phép | Hiệu ứng | Clip |
|---|---|---|---|
| Đứng yên | **Trói** | Vùng bán kính 3m, choáng 1.5s | `attack_FlyingAtk04` |
| Tới | **Thương xuyên** | Xuyên thẳng 10m, đánh ngã | `attack_FlyingAtk06` |
| Lùi | **Đẩy lùi** | Nova bán kính 4m, hất địch ra xa, Trish lùi 2m | `buff_Flying` (cắt ngắn) |

Hồi chiêu khởi điểm: 4s / 3s / 6s.

### Cơ chế riêng: B = Blink
- Dịch chuyển 5m theo hướng stick (không đẩy stick thì lùi ra sau). Bất tử trong lúc dịch chuyển.
- Hủy được mọi đòn sau mốc cast.
- Hồi chiêu 0.6s, tích tối đa 2 lần.
- Không cần anim: ẩn model + VFX ở điểm đi và điểm đến.

### Thanh Nộ và Awake
| Nguồn | Điểm (khởi điểm) |
|---|---|
| Đạn trúng | +3 |
| Nổ nạp trúng | +10 mỗi mục tiêu |
| Khống chế trúng (Trói, Thương, Đẩy) | +15 mỗi mục tiêu |
| Bị đánh | +5 (nộ khi bị đánh, nhỏ) |

Đầy ở 100. Kích hoạt bằng LB + tới = **Devil**, LB + lùi = **Angel**. Kéo dài 12s.

| | Devil Awake (làm trước) | Angel Awake (làm sau) |
|---|---|---|
| Vai trò | Hủy diệt | Khống chế |
| Đạn X | Sát thương x1.5, nổ lan bán kính 1.5m | Mỗi đạn làm chậm 30% trong 2s |
| Skill | Không hồi chiêu | Vùng hiệu ứng x1.5, thời gian khống chế x1.5 |
| Đòn kết thúc khi hết Awake | Nổ lớn quanh Trish | Vùng làm chậm thời gian 3s |
| Màu (cho art-lead) | Đỏ / tím đen | Trắng / vàng |

Dùng chung anim với trạng thái thường. Khác biệt nằm ở VFX, đạn và màu.

## Sửa ngay (từ playtest)
- Thiếu `TrailMaterial`: vệt đạn đang hiện màu hồng tím (màu báo lỗi material).
- Camera cinematic đòn chốt không áp cho đòn tầm xa.

## Gameplay reads cho art-lead
- Vùng Trói / Đẩy phải có telegraph mặt đất đúng bán kính gameplay.
- Devil và Angel phân biệt được bằng màu **và** bằng hình dạng hạt.

## Brief nhạc cho music-lead
- Emotional core: áp đảo, bí ẩn. Theme Awake có 2 biến thể: Devil (nặng, méo tiếng), Angel (hợp xướng, sáng).

## Prototype (slider cho unity-mechanic)
CastDelay, FireRate, ChargeTime, BindRadius, BindDuration, PierceLength, RepelRadius, BlinkDistance, BlinkCooldown, BlinkCharges, RagePerHit, RagePerControl, AwakeDuration.
