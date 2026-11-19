# Prompt pack - DKM
Art direction: `gamedesign/art/art-direction.md` (2026-11-14) · Concept: `concept-sheet.html` (silhouette A / A / A đã chốt)
Tools mặc định: Midjourney (v7) · Flux / SDXL (ComfyUI, Forge) · GPT-image. Kiểm tra lại cờ Midjourney trong docs bản hiện hành trước khi dùng.

**Quy tắc pack này**
- Trong prompt **không có tên nhân vật, tên game, tên studio hay tên họa sĩ**. Ba nhân vật chỉ được mô tả bằng thuộc tính. Tên nội bộ chỉ dùng trong tiêu đề mục.
- Prompt viết bằng tiếng Anh (model hiểu tốt nhất). Mã màu đổi thành tên màu (model bỏ qua hex).
- UI từ AI chỉ là **style frame và component không chữ**. Bố cục lấy từ `spec/ui-hud.md` + mockup trong concept sheet, dựng trong Unity.
- Ảnh nào giữ lại thì ghi vào `provenance.md` (tool, model, prompt ID, seed, ngày).

---

## Style block

### S-CHAR (dán vào mọi prompt nhân vật)
```
stylised-realistic action game concept art, grounded PBR materials, heavy cloth, worn leather and brushed steel, dramatic cool rim light on a dark ancient Japanese temple courtyard at dusk, muted desaturated background of charred wood, grey stone and moss in low contrast, the character carries the only saturated colour on screen, clean readable silhouette, strong value separation between figure and background, high detail on face, hands and weapon, low detail on large surfaces, solemn cinematic mood
```

### S-UI (dán vào mọi prompt UI)
```
minimal cinematic action game interface element, sumi-e ink brush stroke as background shape, thin 2px ivory hairlines with 45-degree chamfered corners, flat lighting, no gloss, no bevel, no ornament, bone ivory and ink black with a single accent colour, crisp vector-like edges, isolated on a plain flat dark grey background
```

### Negatives chung
```
text, letters, words, numbers, watermark, logo, signature, blurry, lowres, extra limbs, extra fingers, deformed hands, cropped, cut off, duplicate, busy background, anime, chibi, cel shading, thick outline, neon cyberpunk, glossy plastic, white hair with red leather coat, blonde hair with black leather outfit
```
(Hai cụm cuối chặn model trôi về ngoại hình nhân vật thương mại quen thuộc.)

---

## Nhân vật

Mô tả nhân vật (dùng lại trong mọi prompt cùng nhân vật):

- **DESC-D (Dante):** `a cocky young demon-hunter swordsman, lean athletic build, ash-black messy spiked hair with a single crimson-dyed lock, long deep crimson haori-cut coat flaring into a wide triangle below the knees with the back split into two tails, dark shirt and trousers, heavy boots, a massive broad greatsword with a brushed steel blade worn diagonally across his back with the hilt over the right shoulder, a slim katana at the left hip`
- **DESC-V (Vergil):** `a calm elegant swordsman, tall and narrow, black hair slicked back and tied low, long straight steel-blue high-collared coat reaching the ankles with one thin bright blue line running down the centre, almost no loose details, a katana in a black lacquered scabbard at the left hip with an ivory-wrapped hilt, the scabbard forming a single diagonal line across the silhouette`
- **DESC-T (Trish):** `a cold commanding sorceress who always hovers a little above the ground, long silver-lavender hair past the waist, dark plum gown narrowing into an inverted teardrop with a violet stripe down one side, bare feet pointed downward and never touching the ground, a glowing two-ring violet rune circle around her casting hand`

### C-01 / C-02 / C-03 Turnaround (Dante / Vergil / Trish)
Mục đích: khóa trang phục và tỷ lệ. Chấm: silhouette có khớp phương án A trong concept sheet không.

Midjourney:
```
character turnaround sheet of <DESC-x>, front view, three-quarter back view, side view, back view, full body, neutral relaxed pose, evenly spaced, same character in every view, plain flat mid-grey background, <S-CHAR> --ar 16:9 --style raw --s 100 --no text, watermark, logo, anime
```
Flux:
```
A character turnaround sheet on a plain mid-grey background showing the same character four times: front, three-quarter back, side and back, full body, neutral pose, evenly spaced. The character is <DESC-x>. <S-CHAR>. No text or labels anywhere.
```
SDXL: positive như Flux, negative = Negatives chung. 1344x768, CFG 5-6, 30-40 steps. Dùng ControlNet lineart/canny với ảnh chụp silhouette A trong concept sheet để giữ tỷ lệ.
GPT-image:
```
Create a clean character turnaround sheet for an original action game character: <DESC-x>. Show front, three-quarter back, side and back views, full body, same proportions in every view, on a plain mid-grey background. Style: <S-CHAR>. Do not add any text, labels or logos.
```
Settings: 16:9, ghi seed. Ảnh đạt → thành **hero image** cho các prompt sau.

### C-04 Key pose, Dante: taunt
```
<DESC-D>, mid-taunt: arms spread wide, chin raised, greatsword planted beside him, coat tails flaring, smirking at an off-screen enemy, seen from a third-person over-the-shoulder game camera slightly behind and to the right, small sparks of crimson rising around his feet, <S-CHAR> --ar 16:9 --style raw --no text, watermark
```
Chấm: tư thế mở, đọc được từ sau lưng; dùng làm brief cho clip taunt đang thiếu (D-002).

### C-05 Key pose, Vergil: Iai
```
<DESC-V>, crouched iaido stance, right hand resting on the hilt, scabbard angled back, perfectly still, a single razor-thin pale ice-blue cut line hanging in the air in front of him like a stroke of ink, third-person game camera behind and to the right, <S-CHAR> --ar 16:9 --style raw --no text, watermark
```
Chấm: silhouette "đóng" lại, nét chéo vỏ kiếm, vệt cắt mảnh chứ không phải chùm hạt.

### C-06 Key pose, Trish: cast khống chế
```
<DESC-T>, hovering one metre above a stone courtyard, one arm extended casting, a flat violet rune circle spreading on the ground below the enemies, hair lifting upward, her shadow clearly separated from her feet, third-person game camera behind and to the right, <S-CHAR> --ar 16:9 --style raw --no text, watermark
```
Chấm: thấy rõ đang bay (bóng tách chân), vòng phép đọc là vùng tròn.

### C-07 Expression sheet (mỗi nhân vật một lần)
```
expression sheet of <DESC-x>, six head close-ups in a 3x2 grid: neutral, smug, focused, angry, exhausted, battle cry, same face in every panel, plain grey background, <S-CHAR> --ar 3:2 --style raw --no text, watermark
```
Dùng `--cref` / omni-reference (MJ) hoặc IP-Adapter (SD) với hero image của C-01..C-03.

### C-08 Awake Devil vs Angel (Trish, cùng tư thế)
Mục đích: kiểm tra hai bộ khác nhau bằng **màu và hình dạng** (spec Trish). Giữ cùng seed + cùng tư thế, chỉ đổi phần sau.
```
<DESC-T>, same hovering casting pose, awakened form: <VARIANT>, <S-CHAR> --ar 16:9 --style raw --seed <n> --no text, watermark
```
- Devil: `hair and rune circle turned blood red, sharp jagged blood-red spikes and thick dark purple-black smoke curling around her lower body, menacing`
- Angel: `hair and rune circle turned ivory white, vertical rays of warm gold light and soft round ivory feather-like particles around her, serene`
Chấm: bật grayscale, hai ảnh vẫn phải khác nhau (gai vs lông vũ tròn).

### C-09 Readability shot (cả nhóm)
```
gameplay screenshot mock-up from a third-person over-the-shoulder camera, an original crimson-coated greatsword fighter in the foreground fighting one hunched jagged dark demon in a dim Japanese temple courtyard at night, fog, the fighter fills a quarter of the frame height, the demon's claw glowing hot orange about to strike, <S-CHAR> --ar 16:9 --style raw --no text, HUD, watermark
```
Chấm: ở cỡ màn hình, người chơi và địch tách nền; cam của mối nguy là thứ sáng nhất.

---

## UI (style frame và component, không chữ)

### U-01 HUD style frame
```
action game combat screen style frame, dim Japanese temple courtyard at night, a minimal ivory health bar sitting on a black sumi-e ink brush stroke in the top-left corner, a large crimson brush-stroke emblem shape on the right edge, a thin rotating ivory target ring around a dark enemy, everything else clean, <S-UI>, no text, no numbers, no letters --ar 16:9 --style raw --no text, letters, numbers, watermark
```
Chấm: mood, chất liệu nét mực, độ yên của HUD. **Không** lấy bố cục từ ảnh này.

### U-02 Thanh máu trên nét cọ
```
single horizontal game health bar, thin ivory fill with a short red depleted segment at the end, sitting on a long horizontal black sumi-e ink brush stroke with dry-brush edges, <S-UI> --ar 4:1 --style raw --no text, numbers
```
Thêm biến thể cùng seed: `boss version, longer and thinner, two small ivory notch markers dividing it into three phases`.

### U-03 Nền chữ rank (Dante)
Model vẽ chữ hỏng: **chỉ gen nét cọ nền**, chữ D, C, B, A, S, SS, SSS vẽ tay riêng.
```
single bold diagonal crimson sumi-e brush stroke, energetic dry-brush splatter at both ends, isolated on a plain flat dark grey background, <S-UI> --ar 1:1 --style raw --no text, letters
```
Biến thể đỉnh: `brighter coral red, with sharp light streaks bursting outward from the centre`.

### U-04 Ba vạch Tĩnh tâm (Vergil)
```
three thin horizontal segments side by side under a health bar, extremely minimal, steel blue, the third state glowing pale ice blue with a soft bloom, <S-UI> --ar 6:1 --style raw --no text
```

### U-05 Chân dung + vòng Nộ (Trish)
```
small circular character portrait frame, dark plum interior, an ensō-like violet brush ring drawn around it two thirds complete, thin dashed inner ring, <S-UI> --ar 1:1 --style raw --no text, face
```
Biến thể: `ring complete and glowing` · `ring blood red with dark smoke` (Devil) · `ring ivory and gold with soft rays` (Angel). Chân dung vẽ riêng từ hero image C-03.

### U-06 Bộ icon skill
```
set of 9 game skill icons in a 3x3 grid, consistent style, ivory line glyphs on dark ink-black circular backgrounds, thin ivory ring border, bold readable silhouettes at 48 pixels, row one crimson accents for a greatsword fighter (sweeping arc, forward lunge, heavy downward finisher), row two steel blue accents for a swordsman (drawing cut, dash-through, sheathing), row three violet accents for a sorceress (binding circle, piercing spear, repelling nova), <S-UI> --ar 1:1 --style raw --no text, letters, numbers
```
Chấm ở cỡ 48 px. Huy hiệu hướng (chấm / ▲ / ▼) thêm khi dựng trong Unity, không gen.

### U-07 Telegraph + vòng lock-on
```
game VFX reference sheet on dark background: a hot white star-shaped glint with an ember orange halo, a flat ember orange ground ring seen in perspective with a bright thin inner line, and a thin ivory dashed rotating target ring, isolated elements, <S-UI> --ar 16:9 --style raw --no text
```
Chỉ để chốt cảm giác. VFX thật dựng trong Unity, đúng bán kính gameplay (`vfx-list.md` V-03).

---

## Quy trình nhất quán
1. Một style block, không sửa từng prompt. Đổi style thì đổi ở đây và ghi ngày.
2. Mỗi nhân vật duyệt một hero image (C-01..C-03), sau đó mọi prompt của nhân vật đó gắn hero image (`--cref`/omni-reference, IP-Adapter, hoặc GPT-image "keep this character").
3. Dùng seed cố định cho các cặp biến thể (C-08, U-03, U-05).
4. Dùng ảnh chụp silhouette trong concept sheet làm ControlNet (lineart/canny) để giữ tỷ lệ đã chốt.
5. Khi có ≥ 15 ảnh đạt cho một nhân vật: train LoRA (SD/Flux) hoặc style model (Scenario/Leonardo).

## Checklist review ảnh gen
- [ ] Silhouette khớp phương án A, đọc được ở cỡ gameplay.
- [ ] Màu đúng vai trò: chỉ nhân vật bão hòa, nền trầm; cam chỉ cho mối nguy.
- [ ] Đọc gameplay: Big Sword vs Katana khác nhau; Trish có bóng tách chân; Vergil có nét chéo vỏ kiếm.
- [ ] Không yếu tố IP: không tóc bạc + áo da đỏ, không tóc vàng + đồ da đen, không logo, không chữ.
- [ ] UI: không có chữ trong ảnh, nét mảnh, đọc được ở cỡ thật.
