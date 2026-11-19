# Nghiên cứu: kit combo + cơ chế riêng cho Dante / Vergil / Trish

Ngày: 2026-11-14. Dùng cho topic "thiết kế combo đánh thường + skill + cơ chế riêng từng nhân vật".

## Định hướng nhân vật (từ người dùng)

| Nhân vật | Định hướng | Kho anim |
|---|---|---|
| Dante | Cận chiến, đa dạng, cuồng bạo, màu mè, trình diễn, cợt nhả đối thủ | `_Animation_Dante` |
| Vergil | Kiếm sĩ điềm tĩnh, thanh lịch, tối giản, chặn đòn + phản đòn | `_Animation_Vergil` |
| Trish | Pháp sư tầm xa, khống chế + hủy diệt, tích nộ -> Devil/Angel Awake | `_Animation_Trish` |

## Kho animation hiện có (đếm từ project)

- **Dante**: 2 bộ vũ khí.
  - Big Sword: 3Combo, 4Combo A/B, 7Combo, Dash_Attack A/B, Jump_Attack_Combo 1-3, **UpperAttack (launcher)**, Skill A-L, bộ Damage đầy đủ.
  - Katana Blade: 3Combo, 4Combo, 5Combo, Dash_Attack A/B, Jump_Attack_Combo 1-4 (+Attach), Skill A-I.
  - **Thiếu**: taunt (cợt nhả), block.
- **Vergil**: attack01-03, combo01-08 (3-5 nhát mỗi chuỗi), attack_jumpATK, avoid x4, roll x4, **defense01 (1 clip chặn)**, **wp_equip / wp_unequip (tra / rút kiếm)**, hit, down, stun.
  - **Thiếu**: launcher, clip phản đòn riêng, clip parry thành công.
- **Trish**: GroundAtk01-06, FlyingAtk01-06 (có bản `_short` cho 01, 03), buff ground/flying, chuyển ground <-> flying, hit, stun, down.
  - **Thiếu**: né, anim Awake (biến hình).

## Game tham khảo (claim + nguồn)

### Dante -> DMC5 Dante: Taunt + Style Rank
- [Nguồn] Taunt trong DMC5 chủ yếu để hồi Devil Trigger và giữ Style Gauge không bị reset. Đứng càng xa địch thì hồi càng ít. Nhảy hay lăn để hủy taunt thì mất phần hồi.
  - Nguồn: [twinfinite: How to Taunt](https://twinfinite.net/?p=431370), [gamepressure: Dante abilities](https://www.gamepressure.com/devil-may-cry-5/abilities/zac052)
- [Nguồn] Taunt chỉ tăng rank khi chạy hết anim, nên taunt là một canh bạc: dễ bị đánh.
  - Nguồn: [Tom's Guide: SSStylish tips](https://www.tomsguide.com/us/devil-may-cry-5-tips,news-29586.html), [hyperx: style ranks](https://blog.hyperx.com/article/7224/all-style-ranks-in-devil-may-cry-5)
- [Nguồn] Royal Guard: chặn đúng lúc để tích gauge, rồi "Release" để phản đòn. Cửa sổ Just Release rất hẹp, theo số liệu fan đo khoảng 4 frame.
  - Nguồn: [samurai-gamers: Royal Guard](https://samurai-gamers.com/devil-may-cry-5/royal-guard-style/)

### Vergil -> DMC Vergil "Concentration" + Sekiro "deflect / posture"
- [Nguồn] Concentration của Vergil thưởng lối chơi có chủ đích và phạt bấm bừa: đánh trượt, bị trúng đòn, lăn né đều làm tụt gauge. Gauge càng cao thì đòn càng mạnh, và có chiêu chỉ dùng được ở mức cao. Số liệu chính xác cho DMC5 chưa chắc chắn, nhiều nguồn là wiki fan.
  - Nguồn: [GamesRadar: producer về Vergil mode](https://www.gamesradar.com/devil-may-cry-5-special-edition-producer-discusses-vergil-mode-in-new-interview), [Siliconera: Concentration](https://www.siliconera.com/look-vergils-concentration-style-devil-may-cry-4-special-edition/), [biligame wiki](https://wiki.biligame.com/dmc5/Vergil)
- [Nguồn] Sekiro xây quanh "kiếm chạm kiếm": phải đỡ (deflect) trước rồi mới tìm sơ hở. Deflect làm đầy posture của địch, posture đầy thì có deathblow bất kể máu. Chỉ chặn mà không deflect thì posture địch hồi liên tục, nên rùa không thắng được.
  - Nguồn: [GamingBolt: Sekiro combat](https://gamingbolt.com/sekiro-shadows-die-twice-dev-talks-about-how-combat-differs-from-dark-souls-and-the-challenges-of-developing-it), [Xbox Wire: Conkey interview](https://news.xbox.com/en-us/?p=112348), [Superjump](https://www.superjumpmagazine.com/the-art-and-science-of-sekiros-combat/)

### Trish -> DMC5 V (nhân vật tầm xa) + Devil Trigger
- [Nguồn] V gần như không đánh trực tiếp mà đánh qua thú triệu hồi. Itsuno: nhân vật mới phải khác Dante và Nero, nếu không thì vô nghĩa. Reviewer chê việc Nightmare tự hành động ngẫu nhiên, ngoài tầm kiểm soát của người chơi.
  - Nguồn: [gamerguides: V gameplay](https://www.gamerguides.com/devil-may-cry-5/the-basics/characters/v-s-gameplay), [Siliconera: V](https://www.siliconera.com/devil-may-cry-5-talks-about-v-the-return-of-dante-and-neros-devil-breaker/), [whatculture review](https://whatculture.com/amp/gaming/devil-may-cry-5-review-5-ups-3-downs?page=5)
- [Suy luận] Bài học từ V: nhân vật tầm xa phải có lý do để không đứng yên spam, và đòn mạnh nhất không được chạy ngẫu nhiên.

## Số đo playtest hiện tại (2026-11-14, bot tay cầm ảo)

- Bấm đến khi trúng lần đầu: Dante 0.40s, Vergil 0.81s, Trish 2.0s.
- Trish: một đòn dài khoảng 5s không hủy được, không né được.
- Vergil: không có launcher (lock + lùi + đánh ra combo thường).
- Skill đang xoay vòng theo con trỏ, combo chọn ngẫu nhiên.
