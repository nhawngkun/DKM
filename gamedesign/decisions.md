# Decision log

Nguồn sự thật cho cả team (design, art-lead, music-lead, dev). Chỉ ghi thêm, không sửa lịch sử.
Muốn đổi một quyết định thì thêm entry mới có dòng `Supersedes: D-xxx`.

> Chưa có `pillars.md`. Các quyết định dưới đây dựa trên định hướng nhân vật người dùng đưa ra
> ngày 2026-11-14 và sẽ được kiểm lại khi chốt pillars.

---

## D-001 - Khung chung: mỗi nhân vật một thanh tài nguyên, combo tất định, input cố định
Date: 2026-11-14 | Status: Agreed | Area: Mechanic / Input
Decision: Mỗi nhân vật có đúng một thanh tài nguyên riêng, cách làm đầy thanh thể hiện tính cách (Dante = Style, Vergil = Tĩnh tâm, Trish = Nộ). Mọi combo là tất định: cùng input ra cùng đòn, bỏ chọn chuỗi ngẫu nhiên và bỏ xoay vòng skill. Input chung: X đánh thường, Y + hướng (đứng yên / tới / lùi) ra 3 skill cố định, B là nút cơ chế riêng, RT né, RB lock-on, A nhảy.
Why: Người chơi phải học được nhân vật. Ba nhân vật phải khác nhau ngay từ "động từ" chứ không chỉ ở skin.
Evidence: Playtest 2026-11-14: combo nhảy chuỗi ngẫu nhiên, skill xoay vòng, Skill A không bao giờ ra đầu. Xem gamedesign/research/character-kits.md.
Options rejected: Giữ chuỗi ngẫu nhiên vì "đa dạng" (người chơi không học được); mỗi nhân vật nhiều thanh tài nguyên (quá rộng cho team nhỏ).
Nearest reference: DMC5 (mỗi nhân vật một cơ chế riêng) - ours differs in: một thanh duy nhất mỗi nhân vật, dùng chung một sơ đồ nút.
Numbers: none yet
Affects: art-lead (HUD 3 loại thanh), music-lead (cường độ nhạc bám theo thanh), dev (CharacterCombo: bỏ RandomGroundChain/PickBranch ngẫu nhiên, skill theo hướng thay _skillCursor; ExamplePlayer: nút B)
Spec: gamedesign/spec/input.md
Open: Bàn phím/chuột chưa map nút B.

## D-002 - Dante "Kẻ trình diễn": Style rank + Taunt + đổi vũ khí giữa combo
Date: 2026-11-14 | Status: Agreed | Area: Character
Decision: Dante dùng thanh Style (D -> SSS), tăng khi đánh đa dạng và khi taunt sát địch, tụt khi lặp một đòn hoặc bị đánh. Nút B là Taunt, chen được giữa combo, càng gần địch càng nhiều Style. Đổi vũ khí (Big Sword nặng / Katana nhanh) giữa combo là một kiểu cancel. Mỗi vũ khí có 3 skill theo hướng. Có launcher thật (UpperAttack hất địch lên).
Why: Đúng định hướng "đa dạng, cuồng bạo, màu mè, trình diễn, cợt nhả". Taunt biến cợt nhả thành canh bạc có thưởng.
Evidence: [Nguồn] Taunt DMC5 hồi DT, giữ Style không reset, hồi ít hơn khi đứng xa (twinfinite, gamepressure trong research/character-kits.md).
Options rejected: B - thanh Cuồng nộ + super armor (rẻ hơn nhưng mất chất trình diễn và đa dạng).
Nearest reference: DMC5 Dante (style + taunt) - ours differs in: không có 4 style, đa dạng đến từ 2 vũ khí + taunt-cancel; Style là tài nguyên tiêu được cho đòn chốt.
Numbers: xem spec (giá trị khởi điểm để tune).
Affects: art-lead (HUD rank chữ D-SSS, VFX taunt, cần 1-2 anim taunt - đang thiếu), music-lead (nhạc combat lên tầng theo rank), dev (StyleMeter, Taunt state, cancel bằng đổi vũ khí, launcher hất mục tiêu)
Spec: gamedesign/spec/character-dante.md
Open: Nguồn anim taunt (mocap/Mixamo/làm tay). Ngưỡng rank.

## D-003 - Vergil "Kiếm sĩ tĩnh lặng": Deflect + thanh Tĩnh tâm + Iai
Date: 2026-11-14 | Status: Agreed | Area: Character
Decision: Nút B là đỡ: bấm đúng lúc đòn địch chạm thì Deflect (địch khựng, mở cửa sổ phản đòn, X ngay sau đó là nhát phản). Bấm sớm hoặc giữ thì chỉ là chặn thường. Thanh Tĩnh tâm tăng khi deflect và khi chém trúng chính xác, tụt khi chém trượt, bấm bừa hoặc bị đánh. Skill chính là Iai (tra kiếm rồi rút chém), tiêu Tĩnh tâm. Combo tối giản: 2 chuỗi mặt đất. Không có launcher, ít đòn trên không (điểm yếu có chủ đích).
Why: Đúng định hướng "điềm tĩnh, thanh lịch, tối giản, chặn và phản đòn". Thưởng sự chính xác, phạt bấm bừa.
Evidence: [Nguồn] Concentration của Vergil phạt đánh trượt và bị trúng, mức cao mạnh hơn. Sekiro xây combat quanh việc đỡ trước rồi tìm sơ hở (research/character-kits.md).
Options rejected: B - thế thủ tự phản (quá thụ động); C - deflect + thế Iai kết hợp (đắt nhất, để sau nếu cần).
Nearest reference: Sekiro deflect + DMC Concentration - ours differs in: không có posture riêng của địch, phần thưởng là Iai và nhát phản chứ không phải deathblow; không hồi gauge khi đứng yên.
Numbers: xem spec.
Affects: art-lead (VFX tia lửa deflect, HUD thanh Tĩnh tâm kiểu tối giản, VFX Iai), music-lead (khoảnh khắc deflect cần stinger âm thanh, theme Vergil tiết chế), dev (Deflect/Guard state, cửa sổ phản đòn, FocusMeter, Iai skill; địch cần telegraph + hit window rõ)
Spec: gamedesign/spec/character-vergil.md
Open: Phụ thuộc vào có địch biết đánh. Anim parry thành công đang thiếu (tạm dùng defense01 + VFX).

## D-004 - Trish "Pháp sư khống chế": đạn nhanh, 3 phép khống chế, Blink, Awake Devil/Angel
Date: 2026-11-14 | Status: Agreed | Area: Character
Decision: Trish luôn bay ở độ cao thấp. X là đạn phép nhanh tự bám (phép ra trong <= 0.3s), giữ X là nạp năng lượng rồi nổ lớn. Y + hướng là 3 phép khống chế: đứng yên = Trói (choáng vùng), tới = Thương xuyên (đánh ngã), lùi = Đẩy lùi (hất xa để thoát). Nút B là Blink (dịch chuyển ngắn, cách né của pháp sư). Thanh Nộ tăng khi phép trúng và khi khống chế thành công. Đầy thì kích hoạt Awake, người chơi chọn Devil (hủy diệt) hoặc Angel (khống chế). Làm Devil trước.
Why: Đúng định hướng "tầm xa, khống chế + hủy diệt, tích nộ vào Devil/Angel Awake". Điểm yếu khi bị áp sát cho Trish lý do để di chuyển chứ không đứng yên spam.
Evidence: [Nguồn] DMC5 V: nhân vật tầm xa phải khác hẳn; reviewer chê đòn mạnh nhất chạy ngẫu nhiên (research/character-kits.md). Playtest: bấm-trúng 2.0s, một đòn khoảng 5s không hủy được.
Options rejected: B - Awake tự chọn theo cách chơi (khó hiểu); C - chỉ Devil (được giữ làm thứ tự làm, không phải thiết kế cuối).
Nearest reference: DMC Devil Trigger + DMC5 V (tầm xa) - ours differs in: Awake có 2 nhánh theo vai trò, chọn lúc kích hoạt; Trish tự tay bắn, không qua thú triệu hồi.
Numbers: xem spec.
Affects: art-lead (VFX Blink, 2 bộ VFX Awake Devil/Angel dùng chung anim, sửa TrailMaterial đạn đang hồng tím), music-lead (theme Awake Devil vs Angel), dev (rút clip _short, giữ-X charge, 3 phép CC, Blink, RageMeter, Awake modifiers; camera cinematic không áp cho đòn tầm xa)
Spec: gamedesign/spec/character-trish.md
Open: Có cần trạng thái đi bộ không (hiện tại: không). Ngưỡng Nộ.

## D-005 - Thứ tự sản xuất: Dante -> địch biết đánh -> Vergil -> Trish
Date: 2026-11-14 | Status: Agreed | Area: Product
Decision: Làm Dante lên chuẩn trước (kho anim đủ nhất, chỉ thiếu taunt) để thành khuôn mẫu, sau đó là một loại địch có telegraph và đánh trả, rồi Vergil (cần địch để deflect), cuối cùng là Trish.
Why: Team nhỏ thì cảm giác AAA đến từ chiều sâu hẹp. Làm 3 nhân vật song song khi lõi chưa xong sẽ nhân lỗi lên 3 lần.
Evidence: Playtest 2026-11-14: 3 nhân vật ở 3 mức hoàn thiện khác nhau, lỗi chung lặp lại ở cả ba.
Options rejected: Làm song song cả 3 nhân vật.
Nearest reference: none (quyết định sản xuất).
Numbers: none
Affects: art-lead (ưu tiên asset Dante + địch), music-lead (ưu tiên nhạc combat chung + Dante), dev (backlog)
Spec: gamedesign/spec/input.md (phần lỗi nền phải sửa trước)
Open: Thiết kế loại địch đầu tiên là topic tiếp theo.

## D-006 - HUD tối giản ở góc, widget tài nguyên riêng từng nhân vật, rank Dante là phần thưởng
Date: 2026-11-14 | Status: Agreed | Area: UI
Decision: Combat HUD tối giản. Máu + thanh nhân vật ở góc trên trái, tự mờ khi hết combat 3s. Máu địch khi lock-on, thanh boss, 3 icon skill (chỉ khi đang hồi chiêu) và Style rank hiện khi cần rồi tự tắt. Không hiện số sát thương. Lock-on, mũi tên địch ngoài màn hình và telegraph nằm trong thế giới game. Mỗi nhân vật một dạng widget: Dante = chữ rank D-SSS lớn cạnh phải; Vergil = 3 vạch mảnh không chữ; Trish = vòng Nộ quanh chân dung, kèm gợi ý Awake khi đầy. Thêm chút hiển thị trong thế giới cho 2 khoảnh khắc đỉnh: kiếm Vergil sáng ở Tĩnh tâm cấp 3, hào quang Trish khi Awake. Có tùy chọn ẩn HUD.
Why: Mắt người chơi phải ở nhân vật và địch. HUD chỉ lên tiếng khi đáng, và bản thân nó cũng là phần thưởng (rank).
Evidence: [Nguồn] GoW gom combat HUD ở góc, có Immersion Mode; Ghost of Tsushima thay chỉ dẫn HUD bằng gió; [Quan sát] DMC rank là HUD phần thưởng (gamedesign/research/hud-music.md).
Options rejected: B - đưa hết thông tin lên người nhân vật (đắt VFX, khó đọc lúc hỗn loạn; chỉ giữ cho 2 khoảnh khắc đỉnh); C - HUD cổ điển có số sát thương (đi ngược hướng điện ảnh).
Nearest reference: God of War (tối giản) + DMC (rank) - ours differs in: mỗi nhân vật một dạng widget theo tính cách, không chỉ đổi màu.
Numbers: HUD mờ sau 3s hết combat; máu địch tắt sau 2s bỏ lock-on.
Affects: art-lead (thiết kế 3 widget, chữ rank, telegraph, mũi tên ngoài màn hình, phát sáng kiếm Vergil / hào quang Trish), music-lead (không), dev (UI HUD theo framework ui-panel, sự kiện từ 3 thanh)
Spec: gamedesign/spec/ui-hud.md
Open: Vị trí icon skill (gần máu hay dưới phải). Kích thước chữ trên TV ở 3m.

## D-007 - Nhạc combat xếp tầng: khung chung L0-L2 + tầng L3 riêng mỗi nhân vật
Date: 2026-11-14 | Status: Agreed | Area: Mechanic (audio)
Decision: Một bài combat chung có 3 tầng (L0 trống, L1 bass/riff, L2 hòa âm), thêm tầng L3 là giai điệu riêng của nhân vật đang chơi. Tầng bám theo thanh của nhân vật: Dante Style B / A-S / SS-SSS; Vergil Tĩnh tâm cấp 1/2/3; Trish Nộ 33% / 66% / Awake (L3 đổi biến thể Devil hoặc Angel). Thêm hoặc bỏ tầng chỉ ở đầu ô nhịp; dưới ngưỡng 4s mới bỏ tầng; hết địch thì outro. Vergil deflect: nhạc chìm khoảng 0.3s rồi tiếng kim loại riêng.
Why: Nhạc là phần thưởng cho việc chơi giỏi; đỉnh bài phải được kiếm. Một khung chung giữ chi phí hợp với team nhỏ, tầng L3 giữ bản sắc nhân vật.
Evidence: [Nguồn] DMC5 vào điệp khúc từ rank S, tụt rank về verse (Kota Suzuki); Metal Gear Rising cho lời hát vào ở cao trào cuối boss (gamedesign/research/hud-music.md).
Options rejected: B - mỗi nhân vật một bài 4 tầng (chi phí gấp 3, để dành cho boss); C - chỉ đổi bài theo trạng thái (mất "nhạc là phần thưởng").
Nearest reference: DMC5 - ours differs in: tầng đỉnh đổi theo nhân vật, và mỗi nhân vật có ý nghĩa thanh khác nhau (đa dạng / chính xác / khống chế).
Numbers: chuyển tầng ở đầu ô nhịp; trễ bỏ tầng 4s; duck deflect 0.3s.
Affects: art-lead (không), music-lead (định hướng âm nhạc, khung combat 3 tầng, 3 giai điệu L3 + 2 biến thể Awake, stinger deflect, outro), dev (hệ phát stem đồng bộ, đọc 3 thanh)
Spec: gamedesign/spec/music-system.md
Open: Màu sắc âm nhạc (rock/metal + nhạc cụ Nhật?), có lời hát ở L3 không, Unity AudioSource PlayScheduled hay FMOD.

## D-008 - Màu âm nhạc riêng từng nhân vật (bảng của người dùng)
Date: 2026-11-14 | Status: Draft (chờ người dùng nghe score v2) | Area: Audio
Supersedes: phần "Màu nhân vật" và "L3 không lời" trong music-direction v1 (D-007 giữ nguyên cấu trúc tầng)
Decision: Dante = rock guitar điện, distortion, power chord, thỉnh thoảng metal scream ad-lib. Vergil = violin solo cinematic, trầm buồn, có đoạn đẩy cao trào. Trish = piano nhẹ + soprano opera + hợp xướng kiểu cầu nguyện nhà thờ; Awake Angel = opera trong sáng, thánh thiện; Awake Devil = ballad buồn, sa ngã (màu "Young and Beautiful"). Giọng hát được dùng nhưng không có lời bài hát.
Why: Người dùng đưa bảng định hướng nhân vật - âm nhạc. Ba nhạc cụ chữ ký (guitar / violin / giọng opera + piano) tách ba nhân vật rõ hơn bảng màu Nhật v1.
Evidence: gamedesign/music/music-direction.md mục v2 (nguồn trích ở đó).
Options rejected: giữ shakuhachi/koto/guitar 8 dây của v1.
Nearest reference: DMC5 (mỗi nhân vật một bài, Dante có vocal gầm) - ours differs in: chung một khung 3 tầng, L3 đổi theo nhân vật.
Numbers: scream 3 lần / vòng 16 ô; Devil cảm giác half-time 75 trên khung 150.
Affects: music-lead (score l3-*-v2.html), dev (thêm stem scream của Dante; có thể thêm L0 half-time cho Devil)
Open: Khung L0-L2 giữ màu Nhật hay đổi gothic; scream ở SS hay SSS.

## D-010 - Đội hình địch: Ghost lính thường, Undead tinh anh, Oni boss phụ, Hell Executioner boss cuối
Date: 2026-11-14 | Status: Agreed | Area: Character (enemy) / Product
Decision: 4 model trong `Assets/_Asset/3D/_Enemy` nhận 4 vai: Ghost Samurai = lính thường (đánh theo đàn), Undead Samurai = lính tinh anh (1 con mỗi đợt, có super armor), Oni Samurai = boss phụ (giữa game), Hell Executioner = boss cuối.
Why: Người dùng chốt. Khớp với độ phức tạp của asset: Ghost low poly rẻ để nhân nhiều con, Undead to con và tầm với dài, Executioner đã có rig Humanoid và anim.
Evidence: gamedesign/research/enemies.md (asset, rig, tham chiếu về token và telegraph).
Options rejected: không có (người dùng chọn thẳng).
Nearest reference: cấu trúc fodder / elite / mid-boss / final boss của DMC5 và God of War - ours differs in: chưa chốt (kit từng con là topic tiếp theo).
Numbers: none yet
Affects: art-lead (silhouette và màu đọc vai trò, telegraph trên thân địch), music-lead (nhạc boss phụ và boss cuối), dev (khung AI địch chung, Mixamo/retarget anim cho 3 model Generic)
Spec: gamedesign/spec/enemies.md (sau khi chốt kit)
Open: Rig của Oni và Undead (import Generic, tên xương chưa rõ). Kit từng con.

## D-009 - Khung nhạc combat L0-L2 đổi từ màu Nhật sang gothic
Date: 2026-11-14 | Status: Agreed | Area: Audio
Supersedes: phần "Màu Nhật" (taiko, shamisen, koto, rin) trong music-direction v1
Decision: L0 = trống metal + timpani + chuông nhà thờ; L1 = riff drop-D + bass (+ organ pedal); L2 = organ ống + hợp xướng SATB (âm tiết, không lời) + harpsichord. Nốt, hòa âm (i i bVI bII ...), 150 BPM, vòng 16 ô giữ nguyên. Stinger deflect đổi sang violin pizz. + chuông tay.
Why: Màu nhân vật D-008 (violin, opera, hợp xướng nhà thờ) hợp gothic hơn đền Nhật; khung và L3 cùng một thế giới âm thanh.
Evidence: người dùng chọn "Đổi sang gothic" sau khi nghe đề xuất.
Options rejected: giữ màu Nhật của v1.
Nearest reference: DMC (gothic + metal) - ours differs in: tầng mở theo thanh của từng nhân vật.
Numbers: không đổi.
Affects: music-lead (combat-frame-v2.html, stingers-v2.html), art-lead (nếu muốn khớp: không gian gothic thay vì FeudalMap Nhật - cần xem lại), dev (không đổi hệ phát)
Open: Bản đồ hiện là FeudalMap (Nhật); nhạc gothic trên map Nhật có thể lệch, art/design cần quyết.

## D-011 - Oni (boss phụ) là kiếm khách lừa nhịp
Date: 2026-11-14 | Status: Agreed | Area: Character (enemy)
Decision: Oni đánh nhanh bằng dao/kiếm ngắn (tầm 1.8m), có đòn trễ nhịp, đòn giả và đòn Tàn ảnh. Luật trung thực: vũ khí luôn lóe trắng đúng 0.25s trước va chạm thật, động tác thân được phép lừa. Phase 1 chỉ dùng đòn giả sau khi người chơi deflect/chặn thành công 2 lần liên tiếp; phase 2 (dưới 50% máu) đánh nhanh hơn, chuỗi 5 nhát, đòn giả 30%. Taunt của Dante trong 3m bị phản đòn ngay; nhảy áp sát khi người chơi đứng xa (Trish).
Why: Người dùng chọn. Biến Oni thành bài kiểm tra deflect của Vergil (D-003) và giữ công bằng nhờ một tín hiệu không bao giờ nói dối.
Evidence: [Nguồn] DMC5 Death Scissors buộc canh nhịp để phản đòn; Sekiro đòn đỏ có cách trả lời riêng (gamedesign/research/enemies.md).
Options rejected: A - đấu sĩ cận chiến đấm/túm (khớp thân hình model hơn, khác hẳn hai con cầm kiếm). Rủi ro đã nêu: ba địch cầm kiếm dễ giống nhau, nên Oni phải khác bằng nhịp, tốc độ và tầm ngắn.
Nearest reference: DMC5 Death Scissors + Sekiro - ours differs in: đòn giả mở khóa theo hành vi người chơi (deflect 2 lần liên tiếp), và tín hiệu thật là tia lóe cố định 0.25s chứ không phải ký hiệu trên màn hình.
Numbers: FlashLead 0.25s; trễ nhịp 0.3-0.6s; đâm xuyên 6m; áp sát khi xa >8m quá 4s; phase 2 tốc độ x1.15; cửa sổ phạt 1.0s / 0.8s; choáng poise 3s.
Affects: art-lead (tia lóe trắng / cháy đỏ trên dao, ảo ảnh Tàn ảnh, Oni phải đọc được là "nhanh và nhẹ" khác Undead), music-lead (nhạc boss phụ 2 phase, khoảnh khắc gầm chuyển phase), dev (AI có bộ đếm deflect của người chơi, đòn giả không lóe, phản ứng với taunt)
Spec: gamedesign/spec/enemies.md (mục 4)
Open: Rig Oni (Generic). Nguồn anim chém ngắn, đòn giả, nhảy áp sát.

## D-012 - Oni: phase 1 cù nhây bịp bợm, phase 2 chém nhau điên cuồng
Supersedes: D-011
Date: 2026-11-14 | Status: Agreed | Area: Character (enemy)
Decision: Phase 1 (100-50% máu): Oni giữ khoảng cách 3-5m, lùi nhử, phản đòn khi người chơi chém trượt, đòn giả 40%, Tàn ảnh, đâm xuyên đỏ, và tự taunt (hở 1.2s = cửa sổ phạt); sát thương thấp x0.7. Phase 2 (50-0%): bỏ hết đòn giả, áp sát liên tục, chuỗi 5-7 nhát x1.25 tốc độ, x1.3 sát thương, nghỉ 0.4s giữa chuỗi, mỗi chuỗi kết bằng 1 nhát đỏ; nhận sát thương x1.3; sau 3 chuỗi thì kiệt sức 2s. Luật trung thực giữ nguyên: lóe trắng đúng 0.25s trước va chạm thật.
Why: Người dùng chọn. Hai phase đối lập (kiên nhẫn vs trụ vững) tạo cảm xúc lên đỉnh, và khoảnh khắc "lật mặt" là phần thưởng kể chuyện.
Evidence: gamedesign/research/enemies.md.
Options rejected: D-011 (lừa ở cả 2 phase, phase 2 chỉ nhanh hơn: ít tương phản).
Nearest reference: DMC5 Death Scissors + Sekiro - ours differs in: boss tự taunt mở cửa sổ phạt, đối xứng với taunt của Dante; phase 2 đánh đổi máu lấy máu và có kiệt sức theo số chuỗi.
Numbers: xem gamedesign/spec/enemies.md mục 4.
Affects: art-lead (biểu cảm/anim trêu ngươi, dao bốc lửa đỏ phase 2, ảo ảnh Tàn ảnh), music-lead (P1 thưa, giễu cợt; chuyển phase bùng nổ; P2 dồn dập), dev (AI giữ khoảng cách, đếm chém trượt, chuỗi dài + kiệt sức)
Spec: gamedesign/spec/enemies.md (mục 4)
Open: Anim taunt và kiệt sức của Oni; rig Oni (Generic).

## D-010 - Vòng nhạc combat 32 ô (A + B + A' + B')
Date: 2026-11-14 | Status: Agreed | Area: Audio
Supersedes: độ dài vòng 16 ô (25.6s) trong D-007 / music-direction v1
Decision: Mọi stem combat (L0-L3) dài 32 ô = 128 phách = 51.2s ở 150 BPM. Cấu trúc A (D thứ) - B (F trưởng tương quan: F C Dm Bb Gm C Bb A) - A' (cao trào) - B' (kết Eb -> A về ô 1). Mỗi nhân vật có giai điệu B riêng. Quy tắc chuyển tầng của D-007 giữ nguyên.
Why: Người dùng nghe bản 16 ô thấy "chỉ toàn lặp đi lặp lại giai điệu giống nhau" và không có cao trào. Đoạn B cho tương phản, A' thành đỉnh của mỗi vòng.
Evidence: phản hồi người dùng 2026-11-14 sau khi nghe score v2.
Options rejected: giữ 16 ô (lặp quá dày).
Nearest reference: none
Numbers: 32 ô, 51.2s, stem dài gấp đôi.
Affects: music-lead (combat-frame-v3, l3-*-v3), dev (stem 51.2s, nén streaming; hệ phát không đổi)
Spec: gamedesign/spec/music-system.md
Open: none

## D-011 - Mỗi nhân vật một bài combat riêng (bỏ khung chung)
Date: 2026-11-14 | Status: Agreed | Area: Audio
Supersedes: phần "khung chung L0-L2 + L3 riêng" của D-007, D-009 (khung gothic chung), D-010 (vòng 32 ô chung)
Decision: Dante, Vergil, Trish mỗi người một bài combat riêng với tempo, giọng/mode, nhịp, groove, form riêng (Dante: hard rock/metal 172 BPM E thứ/Dorian 4/4; Vergil: orchestral 12/8 B thứ hòa âm; Trish: gothic opera 7/8, C Phrygian/Lydian). Mỗi bài có 4 tầng mở theo thanh nhân vật (quy tắc chuyển tầng D-007 giữ nguyên) cộng các section ngang (verse / chorus / breakdown) để chorus là phần thưởng ở mức thanh cao. Đổi nhân vật giữa trận: stinger 1 ô rồi vào bài mới ở cùng tầng. Soạn Dante trước.
Why: Người dùng: 3 bản v1-v3 "giống hệt nhau, chỉ đổi nhạc cụ" và không hợp game chặt chém. Nguyên nhân: chung giọng, tempo, nhịp, vòng hợp âm, trống.
Evidence: gamedesign/research/combat-music-refs.md (DMC5 mỗi nhân vật một brief riêng, chorus chỉ ở rank S+; GoW khác nhau bằng mode, nhịp lẻ, ostinato).
Options rejected: giữ khung chung và viết thêm giai điệu (không sửa được gốc vấn đề).
Nearest reference: DMC5 - ours differs in: tầng mở theo 3 loại thanh khác nhau.
Numbers: Dante 172 BPM.
Affects: music-lead (3 bài mới), dev (3 bộ stem, section ngang + tầng dọc, stinger đổi nhân vật)
Open: thứ tự soạn Vergil / Trish sau khi người dùng duyệt Dante.

---

## D-013 - Reboot: action RPG siêu nhiên kinh dị, Hà Nội năm 2126, một nhân vật chính quyền cước
Date: 2026-11-24 | Status: Agreed | Area: Product / Character
Supersedes: định hướng sản phẩm 3 nhân vật kiểu DMC. D-002, D-003, D-004, D-005, D-008, D-010 (đội hình địch), D-011 (nhạc riêng 3 nhân vật), D-012 chuyển sang **Đóng băng**: không xoá, chỉ dùng lại khi pillars mới cần. D-001 (combo tất định, input cố định, một thanh tài nguyên mỗi nhân vật) giữ làm khung tạm cho tới khi chốt pillars.
Decision: Dự án reboot thành game nhập vai hành động. Bối cảnh: Hà Nội 100 năm sau (khoảng 2126), tận thế công nghệ trộn văn hoá tâm linh và ma quỷ Việt. Cảm hứng tông giọng: Jujutsu Kaisen, Dandadan. Một nhân vật chính mới, đánh quyền cước tay không (phương án 2 của bản nháp martial-arts-combo-draft), cơ chế xây quanh một thanh kiểu "Khí" (tham khảo Sha-Chi của Phantom Blade Zero). Người chơi đánh cả thế lực tâm linh lẫn con người để giữ trật tự thế giới. Triết lý cốt lõi do người dùng đặt: "đánh đấm đã mắt" và "tuyệt đối điện ảnh".
Why: Người dùng chọn. Một nhân vật thay cho ba giúp team nhỏ dồn sức làm sâu một kit; bối cảnh Hà Nội tương lai và tâm linh Việt là điểm khác biệt mà các game tham chiếu không có.
Evidence: gamedesign/research/phantom-blade-zero.md (Sha-Chi, defense-cancel, không có moveset tay không riêng); gamedesign/research/martial-arts-anim-inventory.md (89 clip Humanoid Naraka sẵn dùng).
Options rejected: Phương án 1 - Quyền là vũ khí thứ 3 của Dante (không còn hợp khi reboot); Phương án 3 - chỉ dùng clip cho địch.
Nearest reference: Phantom Blade Zero (một thanh gộp posture + tài nguyên, combat võ thuật điện ảnh) - ours differs in: tay không thay vì kiếm, bối cảnh tâm linh Việt tương lai, kẻ địch gồm cả người và ma.
Numbers: none yet
Affects: art-lead (nhân vật chính mới, bỏ model Dante/Vergil/Trish khỏi hướng phát hành, art direction Hà Nội 2126), music-lead (nhạc 3 nhân vật D-011 đóng băng; cần music direction mới), environment-lead (asset Naraka/FeudalMap kiểu Trung-Nhật không hợp Hà Nội; open world cần xem lại), dev (CharacterController/Combo giữ; WeaponData cho bộ quyền; enemy AI người + ma)
Spec: chưa có - topic kế tiếp là gamedesign/pillars.md
Open: Pillars, loại "nhập vai" (tuyến tính hay thế giới mở), cấu trúc phe phái, cơ chế chính của nhân vật chính.

## D-014 - Pillars của bản reboot + cấu trúc tuyến tính theo khu
Date: 2026-11-24 | Status: Agreed | Area: Pillars / Product
Decision: Chốt 4 pillars trong gamedesign/pillars.md: (1) Điện ảnh liền mạch - điện ảnh từ hệ thống (camera counter/né hoàn hảo, hitstop, finisher) như PBZ, cutscene chỉ cho một số quái/boss và luôn seamless (không cắt, không màn đen, camera đi tiếp từ camera gameplay và trả về); (2) Chuyển động không gãy - mọi trạng thái vào được đòn kế, ưu tiên cancel hơn anim khoá input, mạnh từ phút đầu; (3) Tâm linh Việt là luật chơi; (4) Hai mặt trận ma và người. Game là action RPG tuyến tính theo khu, không thế giới mở.
Why: Người dùng: "đánh đấm đã mắt", "tuyệt đối điện ảnh", nhưng không thể mỗi cú đấm một cutscene - phải dựa vào di chuyển, moveset, anim mượt. Tuyến tính theo khu cho phép dàn dựng cutscene seamless và góc máy có kiểm soát, vừa sức team nhỏ.
Evidence: research/phantom-blade-zero.md (PBZ tự nhận "DMC-lite combat + Souls-lite level design"; Ghoststep đổi góc máy ngắn; "hy sinh độ chính xác input để đổi lấy tốc độ" - GamingBolt 09/2025). God of War 2018 một cảnh máy liên tục [Quan sát].
Options rejected: Thế giới mở (cutscene seamless khó dàn dựng, quá rộng cho team nhỏ); điện ảnh bằng QTE/cutscene dày (người chơi thành người xem).
Nearest reference: Phantom Blade Zero + God of War 2018 - ours differs in: tay không cận thân, cutscene chỉ ở quái có tên, bối cảnh tâm linh Việt 2126.
Numbers: motion warping vào cutscene 0.2-0.4s (giá trị khởi điểm để tune).
Affects: art-lead (art direction theo pillar 3), music-lead (music direction mới, nhạc cutscene nối liền gameplay), environment-lead (khu tuyến tính thay open world; OpenWorld.unity không còn là hướng chính), dev (chế độ cinematic trong CameraController, motion warping vào/ra cutscene, cancel mọi trạng thái)
Spec: gamedesign/pillars.md
Open: Gốc năng lực tâm linh của nhân vật chính (topic kế tiếp). Danh sách khu của Hà Nội 2126.

## D-015 - Kit quyền cước v1 của nhân vật chính (prototype)
Date: 2026-11-24 | Status: Prototype (chờ playtest) | Area: Character / Mechanic
Decision: Nhân vật chính (Hero, model AvatarV10, bộ anim HR_, vũ khí WD_Fist kiểu WeaponType.Fist) đánh quyền cước tay không bằng clip Naraka. 8 chuỗi theo D-001: Liên Hoàn Quyền (X X X), Trọng Cước (X, dừng nhịp, X X X), Thiểm Bộ (dash), Thăng Long (launcher), Không Trung (air), 3 skill theo hướng (Bạo Phá Quyền / Phi Long Cước / Hồi Mã Phản Kích). Cơ chế lên đồng / Tứ Phủ / phong ấn quỷ để sau.
Why: Người dùng yêu cầu tập trung combat võ thuật trước. Clip Naraka lướt xa phục vụ Pillar 2 (Chuyển động không gãy).
Evidence: research/martial-arts-anim-inventory.md, research/martial-arts-combo-draft.md, research/phantom-blade-zero.md.
Options rejected: Quyền là vũ khí thứ 3 của Dante (bỏ khi reboot, D-013).
Nearest reference: Phantom Blade Zero (combo cancel sang phòng thủ, võ thuật điện ảnh) - ours differs in: tay không, đòn lướt xa nối liền, rẽ nhánh bằng dừng nhịp thay vì nút heavy.
Numbers: xem spec (giá trị khởi điểm để tune).
Affects: dev (WeaponType.Fist, quét nắm tay trong MeleeHitDetector, builder đọc .anim), art-lead (VFX đấm/đá, vệt tay), music-lead (SFX đấm/đá thay tiếng chém)
Spec: gamedesign/spec/character-hero.md
Open: Playtest cảm giác; đòn gồng + parry tay không (hệ combo chưa có); KickTimes không bắt được cú đá nào trong clip Naraka; Hồi Mã Phản Kích bước 1 là cú lùi nhưng có mốc trúng giả.

## D-016 - Kit Hero chuyển sang Street Brawl (đấm đá + vật), bỏ clip Naraka
Date: 2026-11-24 | Status: Agreed (prototype) | Area: Character / Mechanic
Supersedes: D-015 (kit quyền cước Naraka)
Decision: Phong cách đánh của nhân vật chính là đánh nhau đường phố bạo lực: đấm thẳng, móc, tạt, đá, túm/vật/ném. Bỏ toàn bộ clip Naraka khỏi kit (đấm nhẹ/nặng, hất tung, trên không, né flashstep) - để riêng, có thể dùng cho trạng thái lên đồng sau này. Anim đòn lấy từ Spider Fighter 3 (`Assets/_Other anim`, Humanoid, mỗi đòn lướt 0-2 m), bỏ mọi đòn có dấu ấn siêu anh hùng (khiên, tơ, vuốt, xúc tu, bay). Di chuyển + né dùng clip Dante (clip chạy/né SF3 đứng tại chỗ, không có root motion). Làm 2 bước: (A) kit đấm đá, (B) hệ túm-ném + đòn kết liễu có anim nạn nhân.
Why: Người dùng playtest: "lướt đi loạn xạ", "ít đòn đá", "vibe kung fu quá", muốn "đánh nhau đường phố... móc, tạt, vật và đá... bạo lực hơn". Clip Naraka lướt 2-9 m/đòn và là quyền Trung Hoa - chỉnh code không đổi được phong cách.
Evidence: research/street-brawl-anim-candidates.md (số đo từng clip trên AvatarV10) [Quan sát].
Options rejected: A thuần đấm đá không vật (thiếu cảm giác bạo lực); C mua/mocap pack mới (để giai đoạn chuẩn bị phát hành).
Nearest reference: Sleeping Dogs / Yakuza (brawler đường phố có túm ném, môi trường) [Suy luận] - ours differs in: địch gồm cả ma, lên đồng mượn sức Tứ Phủ, cutscene kết liễu seamless (Pillar 1).
Numbers: lướt tối đa ~2 m/đòn thường (đòn lao/skill được xa hơn).
Affects: dev (hồ sơ Hero trong CharacterSetupTool, hệ grapple mới), art-lead (VFX va chạm thô, không vệt quyền), music-lead (SFX đấm thịt, xương, ngã đất)
Spec: gamedesign/spec/character-hero.md
Open: Bản quyền - clip SF3 cũng là asset rip, phải thay trước phát hành. Địch có Animator để test anim nạn nhân.

## D-017 - Quay lại kit Naraka, bỏ 3 đòn đấm nhẹ (giữ túm-ném)
Date: 2026-11-24 | Status: Agreed (prototype) | Area: Character
Supersedes: D-016 (bản brawler SF3 - đã build, người dùng xem rồi bỏ)
Decision: Hero dùng lại kit Naraka (D-015) nhưng bỏ `male_punch_attack_light_01/02/03` (quá kung fu) -> mất chuỗi Liên Hoàn Quyền; chuỗi X dưới đất chỉ còn Trọng Cước (heavy_01 -> 02 -> 03). Giữ Túm Ném (nút B / Q, HeroGrapple, clip Hulk_ThrowOne_Take/Throw của SF3 chép vào Naraka/Grab_Throw). Người dùng sẽ xem tiếp anim nào cần bỏ.
Why: Người dùng: "back lại bộ combo trc đi nhưng bỏ ... 3 cái light đi, rồi t sẽ xem bỏ anim nào tiếp"; chuỗi X giữ phần Naraka còn lại.
Open: Danh sách anim Naraka bỏ tiếp; có đòn đấm thường (X nhanh) thay chỗ chuỗi nhẹ không. Clip SF3 vẫn ở Assets/_Asset/Animation/StreetBrawl để thay khi cần.

## D-018 - Thêm song phi, đá xoáy và 2 combo người dùng chọn; giới hạn lướt 1 m
Date: 2026-11-24 | Status: Agreed (prototype) | Area: Character
Decision: Bỏ thêm attack_heavy_03. Người dùng chọn clip: song phi = flashstep_attack_light_01 (mở Thiểm Bộ), đá xoáy = attack_hold_heavy_01 (kết Trọng Cước X X X) và sprint_attack_light_01 (Xoáy Cước, nhánh dừng nhịp), combo đẹp = attack_hold_light_02 (kết Xoáy Cước) và perfect_dodge_attack_light_01 (Hồi Mã #2). Mọi đòn dưới đất / lao / skill lướt tối đa 1 m (CharacterSetupTool Profile.MaxRootTravel -> RootMotionScale).
Why: Người dùng: "các đòn combo đang bị lướt xa quá cho nhích lên 1 chút thôi"; chỉ rõ từng clip muốn dùng.
Spec: gamedesign/spec/hero-combo-test.md

## D-019 - Chỉ còn Hero: nhân vật cũ (Dante, Vergil, Trish, Leon) cất vào _Archive
Date: 2026-11-25 | Status: Agreed | Area: Character
Decision: Bỏ mọi nhân vật cũ, dồn sức hoàn thiện combat Hero. AnimSetLibrary chỉ còn HR_ (AnimSetId 0), WeaponLibrary chỉ còn Fist. Prefab, controller, WD_ và thư mục model/anim riêng của Dante/Vergil/Trish chuyển vào Assets/_Archive/OldCharacters (giữ GUID). Scene SetUp: Player_H điều khiển Hero, bỏ CharacterSwitcher, có sẵn 1 TrainingDummy; giữ Boss_1, Dragon, Cerberus. Code chung (bay, phép, áo khoác, Taunt/Deflect) giữ nguyên nhưng không dùng.
Why: Người dùng: "bỏ hết các nhân vật cũ đi ... tập trung phát triển hoàn thiện combat cho nhân vật Hero".
Open: _Animation_Dante vẫn ở chỗ cũ vì Hero mượn clip đi bộ / strafe / crouch / turn; scene FeudalMap và OpenWorld vẫn dùng Character.prefab (đã vào _Archive, vẫn chạy).

## D-020 - Đỡ đòn: "đỡ xanh, né đỏ", đỡ trúng tự phản kích
Date: 2026-11-25 | Status: Agreed (prototype) | Area: Mechanic / Input
Decision: Nút đỡ = LB / L1, phím R (bấm, không giữ). Cửa sổ đỡ 0.2 s từ lúc bấm, bấm đỡ huỷ được đòn đang đánh; bấm trượt khoá 0.35 s. Đòn địch báo màu: chớp xanh = đỡ được, chớp đỏ = chỉ né được (tỉ lệ đỏ của bù nhìn 30%); bị phá thăng bằng đổi sang phát sáng vàng. Đỡ trúng: địch mất 45 thăng bằng (trên 100) và lỡ nhịp 1.2 s, khựng 0.12 s + zoom + tia lửa, Hero quay mặt về địch và tự ra đòn "Phan Don" (male_punch_attack_shock_light_01, chuỗi Skill slot 4) - đòn chốt nên có cinematic sẵn. Code: Combat/HeroParry.cs, EnemyAttack (ParryableColor / UnparryableColor / UnparryableChance), HitReaction.DrainBalance, ComboCameraFeedback.PlayParry.
Why: Người dùng chọn "Đỡ đòn / phản đòn" làm phần combat tiếp theo. Mượn nguyên lý Phantom Blade Zero (parry blue / dodge red, phản ứng đúng -> game tự dàn cảnh) [research/phantom-blade-zero.md mục 3.3, khuyến nghị 3-4], giữ Pillar 2 (phòng thủ cancel được tấn công).
Open: Chưa có tư thế đỡ riêng (bấm đỡ chỉ dừng đòn, chưa có anim) - ứng viên UF_baiya_resist; chưa có tiếng "keng"; đòn Phan Don dài 1.53 s, nhát trúng ở ~0.53 s, có thể cần cắt ngắn; chưa có máu người chơi nên đòn đỏ trúng chỉ ghi log.
