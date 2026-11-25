# Phantom Blade Zero — nghiên cứu tham khảo cho combat quyền cước DKM

> Ngày nghiên cứu: 2026-11-24. Game đã phát hành **29/10/2026** trên PS5 + PC (Steam ghi 28/10; ngày ban đầu 09/09/2026 rồi lùi) **[Nguồn]** gamingtrend 17/08/2026, itechguides 04/11/2026, Digital Trends 14/11/2026. Tài liệu ưu tiên bản chính thức (review launch); thông tin từ demo 2024–2025 được ghi rõ.
>
> Tag: **[Nguồn]** = có URL (xem mục Nguồn, đánh số [S#]); **[Quan sát]** = nhiều nguồn trùng khớp; **[Suy luận]** = suy đoán/ước lượng của researcher. Mọi con số frame/giây không có nguồn = **giá trị khởi điểm để tune**, không phải số của PBZ.
>
> Lưu ý về độ phủ: các bài review launch nói rất ít về **camera, lock-on, di chuyển trên không/wall run**. Mục nào không tìm được nguồn đều ghi "không tìm thấy" thay vì đoán.

---

## Tóm tắt 10 dòng

1. PBZ tạo cảm giác "mình là võ sư trong phim võ thuật Hồng Kông thập niên 90": nhanh, gọn, đánh-đỡ liền mạch **[Nguồn S1]**.
2. Đạo diễn tự định vị: *không* phải soulslike, *không* phải hack-and-slash truyền thống — "DMC-lite combat + Souls-lite level design + RE-lite không khí" **[Nguồn S7, S8]**.
3. Trục trung tâm là **Sha-Chi** — một thanh duy nhất vừa là stamina, vừa là posture, vừa là tài nguyên đòn đặc biệt **[Nguồn S2]**.
4. Phòng thủ đọc bằng **màu**: chớp xanh = parry được, chớp đỏ = phải né; "parry blue, dodge red" **[Nguồn S4, S5]**.
5. Parry/né đúng lúc vào đòn màu kích hoạt **Ghoststep** — tự lướt ra sau lưng địch, mở cửa sổ phản đòn + camera đổi góc ngắn **[Nguồn S4, S6]**.
6. Đổi vũ khí chính = hồi đầy Sha-Chi (có cooldown) → đổi vũ khí là quyết định *phòng thủ*, không chỉ là biến thể combo **[Quan sát S2, S6, S10]**.
7. Light không tốn Sha-Chi, heavy/combo tốn → nhịp "chọc nhẹ bào posture, xả heavy khi có cửa" **[Nguồn S2, S11]**.
8. Combo từ một tay chính + ~30 vũ khí chính + 20–25 **Phantom Edge** (vũ khí phụ giới hạn lượt, phá giáp/khiên) **[Nguồn S2, S12, S13]**.
9. Triết lý độ khó: "thắng kiểu xấu thì dễ, thắng *đẹp* mới khó"; nhà phát triển chấp nhận "hy sinh độ chính xác input để đổi lấy tốc độ" **[Nguồn S14]**.
10. Chuyển động do action director **Kenji Tanigaki** biểu diễn làm reference, Donnie Yen là creative consultant; review khen combat "hay nhất năm" nhưng chê camera sát tường, finisher bị che và boss to đánh gần khó đọc **[Nguồn S1, S15, S16, S6, S17]**.

---

## 1. Di chuyển

| Cơ chế | Mô tả | Tag |
|---|---|---|
| Sprint | Giữ nút dodge khi khám phá = chạy nhanh; có skill *Thousand-mile Stride*: chạy liên tục ~2 giây thì chuyển sang chạy tốc độ cao | **[Nguồn S5]** |
| Backstep | Bấm dodge không giữ hướng = lùi bước (backstep) | **[Nguồn S5]** |
| Dodge theo hướng | Giữ hướng + dodge, né mọi hướng; review gọi là "repositioning nhanh" | **[Nguồn S5, S6]** |
| Perfect dodge → **Ghoststep** | Né sát lúc chạm vào đòn chớp đỏ → Soul tự dịch ra **sau lưng** địch, mở cửa sổ combo/bào Sha-Chi. Né sớm quá thì vẫn dính phần sau của đòn | **[Nguồn S4, S5]** |
| i-frame | **Không nguồn chính thức nào công bố** số i-frame hay cửa sổ Ghoststep; itechguides nói rõ không có số chính thức | **[Nguồn S10]** |
| Phòng thủ không cắt combo | Skill *Flow Like Water*: block và dodge không làm gián đoạn combo đang chạy; một nguồn dẫn lại GameSpot: chuyển sang phòng thủ "tức thì, kể cả giữa combo" | **[Nguồn S2, S10]** |
| Traversal | Thế giới **tuyến tính** với level 3D nhiều tầng, shortcut, quay lại vùng cũ khi có Phantom Edge mới (kiểu Hollow Knight / Souls trước Elden Ring); "ít parkour" | **[Nguồn S6, S8, S18, S7]** |
| Traversal gắn combat | Vũ khí búa (Bashpole) phá vật cản / phá sàn; có đòn ground-pound từ trên không | **[Nguồn S11, S19]** (preview, có thể đã đổi) |
| Wall run / air dash | Không tìm thấy nguồn mô tả wall run hay air dash cho người chơi. Inven chỉ nói Soul trượt tường trong *cảnh scripted*, còn rơi từ độ cao vừa phải hay xuống nước nông = chết ngay (reviewer chê là vô lý) | **[Nguồn S6]**; phần còn lại **[Suy luận]**: không phải trọng tâm |
| Va chạm địa hình | Nhiều review than bị vướng vào đá/địa hình nhô lên giữa trận | **[Nguồn S2, S18]** |
| Di chuyển → đánh | Không có nguồn mô tả đòn khởi động riêng từ sprint. **[Suy luận]**: phần chuyển tiếp chủ yếu do dodge/Ghoststep → counter, không phải do dash-attack |

**Nhận xét [Suy luận]:** PBZ dồn "độ ngầu" của di chuyển vào *phản ứng* (Ghoststep tự định vị ra sau lưng) chứ không vào traversal tự do. Người chơi không phải tự canh góc vòng ra sau — game làm hộ khi timing đúng. Đây là cách rẻ để có khoảnh khắc "phim võ thuật" mà không cần hệ thống di chuyển phức tạp.

---

## 2. Camera

| Cơ chế | Mô tả | Tag |
|---|---|---|
| Lock-on | Có lock-on (R3 trên layout PS phổ biến); đổi mục tiêu trong đám đông bị nhận xét là **vụng** (AUTOMATON WEST, PC Gamer, theo itechguides) | **[Nguồn S10]** (nguồn thứ cấp) |
| Camera khi parry/Ghoststep | Parry/dodge đặc biệt vào đòn màu → **đổi góc camera ngắn** thành "cinematic sequence"; perfect parry có animation phản đòn riêng | **[Nguồn S6]** |
| Framing 1v1 | GamingBolt: camera "rất ngoan", 1v1 (ví dụ Lion Dance Puppet Grandmaster) dùng framing **gần hơn** để tăng căng thẳng | **[Nguồn S17]** |
| Boss to | Inven: một số boss lớn đánh gần làm **che pattern**; đề nghị khoảng cách camera thay đổi theo kích thước boss | **[Nguồn S6]** |
| Va tường | Shacknews: camera đôi khi trượt ra sau tường/địa hình khi đánh sát vật cản | **[Nguồn S18]** |
| Finisher | Creative Bloq: finisher đôi lúc bị môi trường che, góc lệch | **[Nguồn S17, S20]** |
| Camera shake | Có slider cường độ camera shake, tắt motion blur | **[Nguồn S21]** |
| Nhiều địch | Đánh đám đông được khen là trôi; vũ khí AoE (lưỡi liềm xích, polearm) để quét; có stealth kill nhiều mục tiêu | **[Nguồn S21, S2]** (gộp qua kết quả tìm kiếm) |
| Hitstop / FOV / khoảng cách số | Không tìm thấy số liệu FOV/khoảng cách/hitstop từ nguồn | — |
| Ngôn ngữ "điện ảnh" | Liang: người chơi cũng là "người quay phim"; câu chuyện kung fu truyền thống kể với "góc máy toàn cầu"; visual được so với Từ Khắc, Trương Nghệ Mưu | **[Nguồn S11]** |

**Nhận xét [Suy luận]:** Camera PBZ không có gì cách mạng; điểm mạnh là *cắt góc ngắn khi người chơi làm đúng* (phần thưởng điện ảnh có điều kiện), điểm yếu là các lỗi kinh điển (sát tường, boss to, finisher bị che). Đây chính là các lỗi DKM nên phòng ngay từ đầu vì `CameraFollow` đã có collision.

---

## 3. Combat

### 3.1 Cấu trúc đòn
- **Light / heavy / Power Surge** trên mỗi vũ khí chính; combo mở thêm khi vũ khí lên cấp (tới 30) **[Nguồn S2]**. Chuỗi kiểu `XXY`, `XXYYY` **[Nguồn S11]** (demo 2025).
- Combo thực tế "chủ yếu xen kẽ light và heavy", vũ khí đổi phong cách **[Nguồn S20]**; mỗi vũ khí đổi *stance* (tư thế) của Soul **[Nguồn S17]**; một số combo giống input fighting game **[Nguồn S22]**.
- Chỉ light, Sha-Chi attack và Ultimate dùng chung input; ~30 vũ khí chính mỗi cái có cơ chế riêng (thanh nguyên tố kích nổ, đánh dấu, buff kiếm khí…) **[Nguồn S6]**.
- Mang **2 vũ khí chính + 1 Phantom Edge**, đổi tự do **[Nguồn S22]**. Số lượng mâu thuẫn: 30 / 32 / "hơn 50" vũ khí; 20 / 25 Phantom Edge **[Nguồn S7, S12, S13, S20]** — chênh lệch có thể do cách đếm hoặc thông tin demo.
- **Phantom Edge**: vũ khí phụ sát thương cao, phá guard/khiên, **giới hạn lượt** (tiêu Remnant Sparks, nạp lại ở chuông) **[Nguồn S2, S10]**; còn là "chìa khóa" mở vùng bị chặn (lửa → Edge dập lửa) **[Nguồn S6, S18]**.

### 3.2 Sha-Chi (tài nguyên trung tâm)
- Một thanh: stamina + posture + tài nguyên đòn đặc biệt **[Nguồn S2]**.
- Light **không tốn**; heavy/combo **tốn**; có thể truyền vào vũ khí để tăng damage/phá guard **[Nguồn S2]**. Heavy tích Sha-Chi Essence để mở Power Surge **[Nguồn S10]**.
- Giữ block chặn được phần lớn đòn nhưng **rút Sha-Chi**; Sha-Chi về 0 → guard break **[Nguồn S21, S10]**.
- Inven ghi "tấn công, block và dodge đều tiêu Sha-Qi" — **mâu thuẫn** với Digital Trends (light không tốn). Có thể khác nhau theo vũ khí/skill **[Nguồn S6, S2]**.
- **Đổi vũ khí chính = hồi đầy Sha-Chi**, có cooldown ngắn **[Quan sát S2, S6, S20, S23]**.
- Phía địch: Sha-Chi địch về 0 → mở cửa trừng phạt; light bào Sha-Chi địch mà không tốn của mình **[Nguồn S10]**; Shacknews: "stamina system cho phép dồn đánh một kẻ địch đã kiệt sức" **[Nguồn S18]**.

### 3.3 Parry / deflect / dodge
- **Parry** = *bấm* Stance (không giữ) ngay trước khi trúng: không tốn Sha-Chi, rút Sha-Chi địch **[Nguồn S4, S5, S21]**.
- **Block** = *giữ*: tự động xử lý theo góc/khoảng cách đòn (demo) **[Nguồn S11]**, nhưng tốn Sha-Chi.
- **Chớp xanh (Brutal Move)**: parry đúng → rút mạnh Sha-Chi địch; perfect block đúng lúc chạm → Ghoststep **[Nguồn S4]**.
- **Chớp đỏ (Killer Move)**: không block/parry được, chỉ né → perfect dodge → Ghoststep **[Nguồn S4, S5]**.
- **Đòn hiệu ứng đen**: block được nhưng rút sạch Sha-Chi; né thì không mất gì **[Nguồn S6]**; itechguides mô tả "khói đen/tối màn hình" = tín hiệu né **[Nguồn S10]**.
- Cửa sổ timing: **không có số chính thức** **[Nguồn S10]**. Một preview nhắc cửa sổ parry chỉ ngắn hơn bản trước "2 frame" → đang được tune giữa các bản **[Nguồn, gián tiếp qua kết quả tìm kiếm]**.
- Âm thanh: "tiếng keng" parry liên tục trong boss fight, audio cue giúp theo dõi đòn **[Nguồn S22]**.
- Địch cũng biết block và parry → bấm loạn thất bại nhanh **[Nguồn S11]**.

### 3.4 Execution / finisher
- Địch máu rất thấp hoặc ở trạng thái đặc biệt → prompt finisher (QTE ở demo) **[Nguồn S2, S11]**; "mỗi vũ khí có execution riêng" **[Nguồn S24]**.
- Một số địch yếu cho dùng Phantom Edge *miễn phí* qua prompt **[Nguồn S10]**.
- Execution cho "một nhịp thở" giữa trận **[Nguồn S2]**. **[Suy luận]**: dùng như nhịp nghỉ có thưởng trong trận đông.

### 3.5 Animation cancel
- Animation "đọc được và cancel mượt sang counter, parry, dodge" **[Nguồn S17]**; phòng thủ chen ngang combo tức thì **[Nguồn S10]**.
- Skill *Flow Like Water* làm block/dodge không cắt combo → combo **tiếp tục** sau khi phòng thủ **[Nguồn S2]**.
- Một số Power Surge **không cancel được** **[Nguồn S10]**; skill *Still Mind* cho bất tử khi Power Surge, *Cicada's Escape* tự lùi + bất tử khi bị guard break — bị chê là quá mạnh, hạ skill ceiling **[Nguồn S2]**.

### 3.6 Đa địch, boss, độ khó
- Đánh nhóm được dàn dựng **như một khối** (trận Seven Stars, thủ lĩnh reset đội hình) **[Nguồn S17]**.
- Hơn 60 boss / 8 vùng, gần nửa là tùy chọn; tới 3 phase; chết ở phase sau thì quay lại phase đó **[Nguồn S2]**; không boss nào trùng pattern, không palette swap **[Nguồn S6]**.
- Boss thường: combo cố định, học được. Độ khó cao: AI mượn khái niệm fighting game — "phân tích tình huống có lợi hay bất lợi", dùng kết quả đòn trước để chọn đòn sau **[Nguồn S25]**; Digital Trends: độ khó cao nhất boss đổi moveset theo cách bạn xử lý **[Nguồn S2]**.
- Các mode: Wayfarer (truyện), Hellwalker (AI mạnh), Sixty-Six Days; được quảng cáo "4 mode" nhưng nguồn chỉ nêu 3 **[Nguồn S12]**.
- Cảm nhận độ khó **lệch nhau**: Beebom thấy dễ (boss 1–2 lần) **[Nguồn S22]**, TechRadar bị một số boss tùy chọn đánh bại nhiều lần **[Nguồn S21]**, Creative Bloq chê boss "trâu máu" **[Nguồn S20]**. Không mất tài nguyên khi chết, quái đã giết không respawn **[Quan sát S2, S18, S21]**.

### 3.7 Tay không / đá
- **Không tìm thấy** nguồn mô tả hệ vũ khí tay không/quyền thuần. Preview TechRadar nhắc đòn đá xoạc (split kick) như "kung-fu cổ điển" trong combo **[Nguồn S19]**. **[Suy luận]**: đá/quyền được dùng như *chuyển động lấp* trong choreo của vũ khí, không phải một moveset riêng. Đây là khoảng trống DKM có thể chiếm.

### 3.8 Cảm giác "phim võ thuật" & triết lý thiết kế
- Mục tiêu: "những đòn mượt, ngoạn mục trong phim kung fu thập niên 90"; fan DMC và Ninja Gaiden; kinh nghiệm mobile dạy họ "chuỗi đòn phức tạp với ít bấm loạn nhất" **[Nguồn S1]**.
- **Kenji Tanigaki** (action director, chỉ đạo võ thuật Rurouni Kenshin, cộng sự lâu năm của Donnie Yen) biểu diễn từng đòn trước ma trận camera làm reference; năm 2023 Liang nói đòn đánh **làm tay, không phải mocap** vì mocap "không làm nổi" **[Nguồn S1, S15]**. Các bài 2025–2026 lại mô tả studio mocap ở Thượng Hải, dây cáp treo võ sư, >20 võ sư/diễn viên đóng thế **[Nguồn S11, S26]** → **mâu thuẫn** hoặc đã đổi quy trình: khả năng cao là mocap làm nền rồi animator chỉnh tay **[Suy luận]**.
- Donnie Yen: creative consultant về choreo từ 2023, đóng vai Mo Yuan (cha Soul) **[Nguồn S12, S16]**. Không tìm thấy nguồn nối Yuen Woo-ping với PBZ.
- Combat director **Qianli Ma**: "ở một mức độ nào đó, từ bỏ độ chính xác của input để đổi lấy tốc độ"; "muốn thắng kiểu tầm thường, không đẹp thì khá dễ; muốn thắng thật đẹp thì đó mới là phần khó" **[Nguồn S14]**. Một thành viên khác (Bob Wu) nói mục tiêu là "dòng chảy nhanh và mượt chứ không phô từng chiêu" **[Nguồn, qua kết quả tìm kiếm S7]**.
- Quy trình: combat/movement/level làm bằng gray-box "white dummy" trước, văn hóa phủ lên sau **[Nguồn S3]**.
- Liang: "bỏ map quá rối khiến người chơi lạc, và bỏ độ khó" của Souls **[Nguồn S3]**; "đặt combat Ninja Gaiden vào một map Souls" **[Nguồn S7]**.

### 3.9 Phê bình đáng chú ý cho DKM
- Visual clutter trong boss fight che hành động **[Nguồn S2]**; "đôi khi quá nhanh", quá nhiều vũ khí gây ngợp, readability kém **[Nguồn S20]**.
- Nút block trùng Sha-Chi attack, không remap được → ra đòn ngoài ý muốn **[Nguồn S6]**. Không có remap nút nói chung **[Nguồn S21]**.
- Quái thường "hơi na ná nhau" **[Nguồn S18]**; thuật ngữ nhiều, khó học **[Nguồn S6]**.

---

## 4. Bài học cho DKM

1. **Một thanh, ba vai trò (học nguyên lý, không copy Sha-Chi).** Lý do hiệu quả: người chơi chỉ phải nhìn một chỗ, và mọi quyết định (đỡ, xả heavy, né) đều quy về cùng một ngân sách → đánh đổi rõ ràng **[Suy luận]**. DKM làm khác: thay vì gộp stamina+posture, gộp **"Khí" = posture của mình + nhiên liệu đòn quyền mạnh**, còn dodge giữ miễn phí như DMC (giữ bản sắc DMC-style, không phạt di chuyển).
2. **Light miễn phí, heavy có giá.** Giữ cho light luôn "an toàn để bấm" mà vẫn có lý do chọn heavy **[Nguồn S2 → Suy luận]**. Với quyền cước: đấm nhanh (jab) miễn phí và bào posture địch; đá xoay/đá bay tốn Khí nhưng phá guard. Cách khác PBZ: heavy *hoàn lại* Khí nếu trúng địch đang stagger — thưởng cho chuỗi chứ không chỉ phạt.
3. **Ngôn ngữ màu cho đòn địch là "UI không cần UI".** Chớp xanh/đỏ dạy người chơi phản ứng đúng mà không cần tutorial dài **[Nguồn S4, S21]**. DKM: dùng 2 tín hiệu nhưng gắn vào **động tác** (vệt sáng trên tay/chân địch + âm thanh riêng) thay vì chớp toàn thân, để giữ readability khi nhiều địch (PBZ bị chê clutter **[Nguồn S2]**).
4. **Phản ứng đúng → game tự dàn cảnh cho bạn (Ghoststep).** Hiệu quả vì biến timing chính xác thành khoảnh khắc điện ảnh *không cần thêm input* **[Nguồn S4, S6 → Suy luận]**. DKM làm khác: perfect dodge vào đòn "đỏ" → **trượt vào trong tầm tay** (inside position) chứ không ra sau lưng, rồi mở cửa sổ ~0.4–0.6s cho một chuỗi quyền ngắn (giá trị khởi điểm để tune). Hợp với quyền cước vì tay không cần áp sát; tận dụng `ClampApproach`/`AttackStopDistance` sẵn có để chốt khoảng cách.
5. **Phòng thủ phải cancel được tấn công.** Combo PBZ được khen vì đánh và đỡ "cùng lúc" **[Nguồn S10, S17]**. DKM: mở cancel window sang dodge/parry ở *mọi* step của bộ quyền sau frame active (dùng event `OnComboAnimEnd` đã có làm mốc, thêm một mốc "defense cancel" sớm hơn). Khác PBZ: không bán việc "giữ combo sau khi phòng thủ" thành skill — cho mặc định với quyền cước, vì combo tay không ngắn.
6. **Đổi vũ khí là van xả tài nguyên.** Hiệu quả vì ép người chơi luân phiên phong cách thay vì spam một bộ **[Quan sát S2, S6]**. DKM (DMC-style đã có đổi vũ khí qua `WeaponData`): đổi *sang* bộ quyền cước hồi một phần Khí và cho đòn mở đầu đặc biệt nếu đổi đúng lúc — gần "style switch" của DMC hơn là hồi đầy.
7. **"Thắng xấu thì dễ, thắng đẹp mới khó."** Tách sàn (sống sót) khỏi trần (biểu diễn) **[Nguồn S14]**. DKM vốn có style rank kiểu DMC → dùng Ghoststep-like counter, parry chuỗi và finisher quyền cước làm nguồn điểm style lớn nhất; người mới vẫn qua bằng dodge + light.
8. **Camera thưởng có điều kiện, không phải cinematic tự do.** PBZ chỉ cắt góc ngắn khi parry/dodge đặc biệt **[Nguồn S6]**. DKM: dùng `CameraEffects` cho một "punch-in" 0.2–0.35s (khởi điểm) + hitstop chỉ khi counter/finisher quyền trúng; không đổi góc trong combo thường để người chơi không mất định hướng. Kèm kiểm tra va chạm trước khi chọn góc finisher (lỗi PBZ bị chê **[Nguồn S18, S20]**).
9. **Khoảng cách camera theo kích thước địch.** Lỗi boss to che pattern **[Nguồn S6]** → DKM: `CameraFollow` lấy bán kính bounds của target lock-on để đẩy distance/FOV; đặc biệt cần vì quyền cước bắt người chơi đứng *rất sát*.
10. **Gray-box trước, văn hóa sau; choreo từ người thật.** PBZ chốt combat bằng dummy trắng rồi mới phủ wuxia **[Nguồn S3]**, và dùng võ sư biểu diễn làm reference **[Nguồn S1]**. DKM (team nhỏ): quay video tham chiếu võ thật (Vịnh Xuân/Thái/taekwondo) bằng điện thoại, chọn lọc clip có sẵn, tune trên capsule trước khi chỉnh anim — và tận dụng khoảng trống "tay không chưa ai làm kỹ" ở PBZ **[Suy luận từ 3.7]**.

### 3 điều KHÔNG nên lấy
1. **Hơn 30 vũ khí chính + 20–25 Phantom Edge, mỗi cái cơ chế riêng.** Tốn khủng khiếp animation/choreo và chính PBZ bị chê "ngợp", vũ khí gimmick **[Nguồn S20]**. DKM nên có ít bộ (kiếm + quyền cước + 1–2) nhưng sâu.
2. **Phạt nặng bằng việc block tốn tài nguyên + rơi nhẹ/nước nông = chết.** Hợp hơi soulslike, đi ngược nhịp DMC (giữ người chơi tấn công liên tục); và ludonarrative dissonance bị chê **[Nguồn S6]**.
3. **Boss AI "fighting game" thích nghi theo người chơi + 60 boss, 3 phase.** Đắt về AI/QA, khó tune cho team nhỏ; DMC-style hợp hơn với boss pattern cố định, đọc được, ít nhưng chất **[Nguồn S2, S25 → Suy luận]**. (Phụ: hệ thống tài nguyên vũ khí phụ giới hạn lượt + chuông nạp là ràng buộc của level design Souls-lite, DKM không cần.)

---

## 5. So sánh nhanh PBZ vs DMC5 vs Sekiro

| Trục | Phantom Blade Zero | Devil May Cry 5 | Sekiro |
|---|---|---|---|
| Tốc độ | Rất nhanh, "đôi khi quá nhanh" **[Nguồn S20, S1]** | Rất nhanh, tự do tối đa **[Quan sát]** | Nhanh nhưng theo nhịp đối kháng 1v1 **[Quan sát]** |
| Phòng thủ chủ đạo | Parry (xanh) + dodge (đỏ) + đổi vũ khí hồi tài nguyên **[Nguồn S4, S2]** | Dodge/jump-cancel, Royal Guard tùy style **[Quan sát]** | Deflect là trung tâm, Mikiri/nhảy cho đòn nguy hiểm **[Quan sát]** |
| Độ phạt | Trung bình: không mất đồ khi chết, giữ phase boss, quái không respawn **[Nguồn S2, S18]** | Thấp–trung bình (checkpoint dày, chấm điểm style) **[Quan sát]** | Cao: vài đòn chết, không cấp độ dễ **[Quan sát]** |
| Combo freedom | Trung bình–cao: chuỗi light/heavy theo vũ khí, 2 vũ khí + Edge, một số cancel bị khóa **[Nguồn S10, S22]** | Rất cao: cancel, juggle, style switch, combo tự do **[Quan sát]** | Thấp: chuỗi ngắn, combat art; trọng tâm là đỡ **[Quan sát]** |
| Tài nguyên trung tâm | Sha-Chi (stamina+posture+đòn đặc biệt) **[Nguồn S2]** | Devil Trigger / style meter **[Quan sát]** | Posture hai phía **[Quan sát]** |
| Camera | Lock-on có, đổi góc ngắn khi parry/dodge đặc biệt, lỗi sát tường/boss to **[Nguồn S6, S18, S10]** | Tự do + lock-on, camera xa, ưu tiên đọc không gian cho juggle **[Quan sát]** | Lock-on chặt 1v1, khoảng cách gần, góc thấp **[Quan sát]** |

**[Suy luận]** DKM đứng gần DMC5 nhất; thứ đáng mượn từ PBZ là *tầng phản ứng có màu + phần thưởng điện ảnh khi phản ứng đúng*, đặt lên trên tự do combo của DMC, không phải hệ tài nguyên nặng kiểu Sekiro/PBZ.

---

## 6. Nguồn

| # | Nguồn | Ngày |
|---|---|---|
| S1 | PlayStation Blog — Soulframe Liang, "Phantom Blade Zero: a new beginning in a long journey" https://blog.playstation.com/2023/05/24/phantom-blade-zero-a-new-beginning-in-a-long-journey/ | 24/05/2023 |
| S2 | Digital Trends review https://www.digitaltrends.com/gaming/phantom-blade-zero-review/ | 14/11/2026 |
| S3 | Inverse interview Soulframe Liang https://www.inverse.com/gaming/phantom-blade-zero-soulframe-liang-director-interview | 11/08/2025 |
| S4 | NoobFeed — parry & perfect dodge guide https://www.noobfeed.com/articles/phantom-blade-zero-parry-perfect-dodge | 30/10/2026 |
| S5 | AllThings.How — dodge guide https://allthings.how/phantom-blade-zero-dodge/ | cập nhật 29/10/2026 |
| S6 | Inven Global review (9.2) https://www.invenglobal.com/articles/27699/combat-pushed-to-the-limit-phantom-blade-zero-92 | 25/10/2026 |
| S7 | Kết quả tìm kiếm tổng hợp phỏng vấn "not soulslike" (GamesRadar, Push Square, Epic Games Store, GamesBeat) — https://www.gamesradar.com/games/action-rpg/phantom-blade-zero-is-neither-a-soulslike-game-nor-a-traditional-action-game-creator-says-suggesting-that-maybe-it-could-create-a-new-identity-altogether ; https://www.pushsquare.com/news/2025/06/phantom-blade-zero-dev-reiterates-yet-again-that-ps5-stunner-is-not-a-soulslike (chưa đọc được toàn văn, chỉ trích qua search) | 2025 |
| S8 | TechNode https://technode.com/2025/09/17/phantom-blade-zero-aims-to-create-a-unique-kung-fu-action-game-rather-than-a-souls-like-game/ | 17/09/2025 |
| S10 | iTechGuides — combat explainer (tổng hợp review, phân biệt chính thức/không) https://www.itechguides.com/how-phantom-blade-zeros-combat-works-stances-counters-combos-and-weapon-swaps | 04/11/2026 |
| S11 | Inven Global hands-on https://labs.invenglobal.com/articles/19994/kung-fu-punk-hands-on-phantom-blade-zeros-combat-story-and-studio-vision | 19/12/2025 |
| S12 | GamingTrend — State of Play https://gamingtrend.com/news/phantom-blade-zeros-state-of-play-digs-into-combat-side-quests-and-donnie-yens-involvement/ | 17/08/2026 |
| S13 | Shacknews / các nguồn đếm 20 Phantom Edge (xem S18) | — |
| S14 | GamingBolt — Qianli Ma về độ khó & vẻ đẹp https://gamingbolt.com/phantom-blade-zero-combat-director-says-difficulty-comes-from-wanting-to-look-beautiful-in-combat (gốc: GamesRadar https://www.gamesradar.com/games/action-rpg/beating-kung-fu-action-rpg-phantom-blade-zero-is-pretty-achievable-says-combat-director-but-only-if-youre-willing-to-do-so-in-a-mediocre-not-as-beautiful-way) | 16/09/2025 |
| S15 | Kết quả tìm kiếm về Kenji Tanigaki (PlayStation Blog 2023, Wikiwand) https://www.wikiwand.com/en/articles/Phantom_Blade_Zero | — |
| S16 | God is a Geek — Donnie Yen https://godisageek.com/2026/08/donnie-yen-phantom-blade-zero-kung-fu/ ; PC Gamer https://pcgamer.com/games/action/phantom-blade-zero-just-dropped-a-huge-secret-hong-kong-legend-donnie-yen-has-been-guiding-its-kungfupunk-stunts-since-2023 | 08/2026 |
| S17 | GamingBolt — "Turns Every Fight Into Cinema" https://gamingbolt.com/?p=655966 | 03/11/2026 |
| S18 | Shacknews review (8/10) https://shacknews.com/article/151138/phantom-blade-zero-review-score | 25/10/2026 |
| S19 | TechRadar preview https://www.techradar.com/gaming/phantom-blade-zero-preview (trích qua kết quả tìm kiếm) | 2024–2025 |
| S20 | Creative Bloq review https://creativebloq.com/reviews/phantom-blade-zero-review-dark-wuxia-martial-arts-masters-with-an-apprentices-execution | 26/10/2026 |
| S21 | TechRadar review https://www.techradar.com/gaming/phantom-blade-zero-review | 27/10/2026 |
| S22 | Beebom review (9/10) https://beebom.com/phantom-blade-zero-review/ | 08/11/2026 |
| S23 | Inverse preview https://www.inverse.com/gaming/phantom-blade-zero-hands-on-preview | 06/08/2025 |
| S24 | = S23 (chú thích ảnh "mỗi vũ khí có execution riêng") | 06/08/2025 |
| S25 | GamingBolt — boss AI kiểu fighting game https://gamingbolt.com/?p=628242 | 22/09/2025 |
| S26 | 80.lv https://80.lv/articles/phantom-blade-zero-uses-authentic-kung-fu-motion-capture ; AUTOMATON (JP) https://automaton-media.com/articles/reportjp/20261009-472629/ (trích qua kết quả tìm kiếm) | 2025–10/2026 |

**Không đọc được (403/bị cắt):** VGC review, Kotaku review, PC Gamer review & preview, Epic Games Store interviews/deep dive, Gematsu State of Play. Thông tin từ các nguồn này chỉ xuất hiện gián tiếp qua bài khác và được ghi tên nguồn gốc.
