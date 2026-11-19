// v2: bảng màu nhân vật theo D-008 (rock/scream cho Dante, violin cho Vergil, piano + opera + hợp xướng cho Trish). Phần chung chép từ _build-scores.js.
const fs = require('fs');
const path = require('path');
const OUT = 'G:/UnityProject/DKM/gamedesign/music';
const TPL = path.join(process.env.USERPROFILE, '.claude/skills/music-lead/assets/score-template.html');
const tpl = fs.readFileSync(TPL, 'utf8');
const head = tpl.split('</head>')[0].replace(/<!--[\s\S]*?-->\n?/, '');

// ---------- Vòng hòa âm chung 16 ô ----------
const CH = ['Dm', 'Dm', 'Bb', 'Eb', 'Dm', 'Gm', 'Bb', 'A', 'Dm', 'Dm', 'Bb', 'Eb', 'Gm', 'Bb', 'Eb', 'A'];
const ROMAN = ['i', 'i', 'bVI', 'bII', 'i', 'iv', 'bVI', 'V', 'i', 'i', 'bVI', 'bII', 'iv', 'bVI', 'bII', 'V'];
// Loại ô: Dm2 = ô Dm thứ hai của cặp (ô 2, 10)
const KIND = ['Dm', 'Dm2', 'Bb', 'Eb', 'Dm', 'Gm', 'Bb', 'A', 'Dm', 'Dm2', 'Bb', 'Eb', 'Gm', 'Bb', 'Eb', 'A'];

// L0
const DR = {
  first: '[Fa] ^g [c^g] ^g [F^g] [F^g] [c^g] ^g',
  a: '[F^g] ^g [c^g] ^g [F^g] [F^g] [c^g] ^g',
  b: '[F^g] ^g [c^g] ^g [F^g] ^g [c^g] [F^g]',
  eb: '[F^g] ^g [c^g] ^g [F^g] ^g [c^g] c',
  fill: 'c c c c c/c/c/c/ c c',
};
const drums = CH.map((c, i) => i === 0 || i === 8 ? DR.first : i === 7 || i === 15 ? DR.fill : c === 'Eb' ? DR.eb : i % 2 ? DR.b : DR.a);
const TK = { n: 'C2 e e C2 e e', eb: 'C C z C C2 C2', a: 'C2 C2 C C C2' };
const taiko = CH.map(c => c === 'Eb' ? TK.eb : c === 'A' ? TK.a : TK.n);
// L1
const RIFF = {
  Dm: 'D,,D,,D,,_E,, D,,D,,A,,G,,', Dm2: 'D,,D,,D,,_E,, D,,D,,F,,E,,',
  Bb: 'B,,B,,B,,A,, B,,B,,F,,D,,', Eb: '_E,,_E,,_E,,D,, _E,,_E,,G,,_E,,',
  Gm: 'G,,G,,G,,F,, G,,G,,D,,F,,', A: 'A,,A,,A,,B,, A,,A,,^C,E,',
};
const BASS = {
  Dm: 'D,,,2 D,,,D,,, D,,,2 A,,,C,,', Dm2: 'D,,,2 D,,,D,,, F,,,2 E,,,A,,,',
  Bb: 'B,,,2 B,,,B,,, B,,,2 F,,,A,,,', Eb: '_E,,,2 _E,,,_E,,, _E,,,2 B,,,G,,,',
  Gm: 'G,,,2 G,,,G,,, G,,,2 D,,,F,,,', A: 'A,,,2 A,,,A,,, ^C,,2 E,,^C,,',
};
// L2
const PAD = ['[DFA]8', '[DFA]8', '[DFB]8', '[_EGB]8', '[DFA]8', '[DGB]8', '[DFB]8', '[^CEA]8',
  '[DFA]8', '[FAd]8', '[FBd]8', '[GB_e]8', '[GBd]8', '[FBd]8', '[GB_e]8', '[EA^c]8'];
const KOTO = {
  Dm: 'z2 d a z2 f a', Dm2: 'z2 d _e z2 d A', Bb: 'z2 B f z2 d f',
  Eb: 'z2 _e g z2 B g', Gm: 'z2 G d z2 B d', A: 'z2 ^c e z2 a e',
};

// Gắn hợp âm (và nhãn đoạn) vào đầu mỗi ô của giai điệu
function withChords(bars) {
  return bars.map((b, i) => {
    let tag = `"${CH[i]}"`;
    if (i === 0) tag = '"^A (điểm lặp)"' + tag;
    if (i === 8) tag = '"^A\'"' + tag;
    return b.startsWith('(3') ? '(3' + tag + b.slice(2) : tag + b;
  });
}
const line = bars => bars.map((b, i) => b + (i === 15 ? ' :|' : (i % 4 === 3 ? ' |\n' : ' | '))).join('');
const fromKind = tbl => KIND.map(k => tbl[k]);

const PERC = `%%percmap F bass-drum-1
%%percmap c acoustic-snare
%%percmap ^g closed-hi-hat x
%%percmap a crash-cymbal-1 x
%%percmap C low-floor-tom
%%percmap e high-tom`;

function hdr(x, title, voices) {
  return `X:${x}\nT:${title}\nC:music-lead v2\nM:4/4\nL:1/8\nQ:1/4=150\n${PERC}\n%%score ${voices.map(v => v[0]).join(' ')}\n` +
    voices.map(([id, clef, name, snm, midi]) => `V:${id} clef=${clef} name="${name}" snm="${snm}"\n${midi}`).join('\n') + '\nK:Dm\n';
}
function body(voices) {
  const out = [];
  for (let s = 0; s < 16; s += 4)
    voices.forEach(([id, bars]) => out.push(`[V:${id}] ${s === 0 ? '|: ' : ''}${bars.slice(s, s + 4).join(' | ')} ${s === 12 ? ':|' : '|'}`));
  return out.join('\n');
}

// ---------- Trang HTML ----------
const table = (cols, rows) => `<div class="scroll"><table>\n<tr>${cols.map(c => `<th>${c}</th>`).join('')}</tr>\n` +
  rows.map(r => `<tr>${r.map(c => `<td>${c}</td>`).join('')}</tr>`).join('\n') + '\n</table></div>';
const HARM_COLS = ['Section', 'Bars', 'Mode / key', 'Progression', 'Idea'];
const ORCH_COLS = ['Section', 'Bars', 'Melody', 'Counter / inner', 'Harmony / pad', 'Bass', 'Percussion', 'Dynamics', 'Colour'];
const ADAPT_COLS = ['Layer / section', 'Content', 'Game state', 'Enter / exit rule'];
const harmonyStd = [
  ['A', '1-8', 'D thứ + In (Eb)', 'Dm Dm Bb Eb | Dm Gm Bb A (i i bVI bII | i iv bVI V)', 'bII Eb là màu Phrygian/In; A trưởng (C#) kéo về D'],
  ["A'", '9-16', 'D thứ + In', 'Dm Dm Bb Eb | Gm Bb Eb A (i i bVI bII | iv bVI bII V)', 'bII -> V ở ô 15-16 (Neapolitan Phrygian), lặp về ô 1 rất chắc'],
];

const SCRIPT = `<script>
  const src = document.getElementById('abc-source').textContent.trim();
  document.getElementById('abc-print').textContent = src;
  const warnBox = document.getElementById('warnings');
  const host = document.getElementById('tunes');
  if (!window.ABCJS) {
    warnBox.className = 'bad';
    warnBox.textContent = 'Không tải được abcjs (cần mạng để hiện khuông nhạc).';
  } else {
    const all = [];
    src.split(/\\n(?=X:)/).forEach((abc, i) => {
      const sec = document.createElement('section');
      sec.innerHTML = '<div class="paper"><div id="paper' + i + '"></div></div><div class="audio" id="audio' + i + '"></div>';
      host.appendChild(sec);
      const tune = ABCJS.renderAbc('paper' + i, abc, { responsive: 'resize', add_classes: true, staffwidth: 900 })[0];
      (tune.warnings || []).forEach(w => all.push('Tune ' + (i + 1) + ': ' + w));
      if (ABCJS.synth.supportsAudio()) {
        const sc = new ABCJS.synth.SynthController();
        sc.load('#audio' + i, null, { displayLoop: true, displayRestart: true, displayPlay: true, displayProgress: true, displayWarp: true });
        sc.setTune(tune, false, { chordsOff: true }).catch(e => { warnBox.innerHTML += '<br>Synth: ' + e; });
      }
    });
    warnBox.className = all.length ? 'bad' : 'ok';
    warnBox.innerHTML = all.length ? '<b>Cảnh báo (' + all.length + '):</b><br>' + all.join('<br>') : 'Không có cảnh báo';
  }
</script>`;

function page(p) {
  const html = `${head.replace('<title>Game Score</title>', `<title>${p.title}</title>`)}
<style>body { font-family: "Times New Roman", "Noto Serif", serif; } .audio { margin: 8px 0 18px; } section + section { margin-top: 18px; }</style>
</head>
<body>
<main>
  <h1>${p.title}</h1>
  <div class="meta">${p.meta}</div>

  <h2>Concept</h2>
  ${p.concept}

  <h2>Score</h2>
  <p class="meta">Ký âm ở cao độ thực (concert pitch). Khi chép part: guitar và bass viết cao 1 quãng 8. Ký hiệu trống: F kick, c snare, g× hi-hat, a× crash. Nút play phát bằng synth GM, chỉ để nghe nốt và độ khớp, không phải âm sắc thật.</p>
  <div id="tunes"></div>
  <div id="warnings"></div>

  <h2>Harmony plan</h2>
  ${table(HARM_COLS, p.harmony)}

  <h2>Orchestration</h2>
  ${table(ORCH_COLS, p.orch)}

  <h2>Adaptive use in game</h2>
  ${table(ADAPT_COLS, p.adaptive)}

  <h2>Production notes</h2>
  <ul>${p.notes.map(n => `<li>${n}</li>`).join('\n')}</ul>

  <details><summary>ABC source</summary><pre id="abc-print"></pre></details>
</main>

<script type="text/plain" id="abc-source">
${fixMidi(p.abc)}
</script>

${SCRIPT}
</body>
</html>
`;
  fs.writeFileSync(path.join(OUT, p.file), html);
  console.log('wrote', p.file);
}


// ================= v2: giai điệu =================
const refBass = ['Bs', 'bass', 'L1 Bass (tham chiếu)', 'Bass', '%%MIDI program 34'];
const refPad = ['Pd', 'treble', 'L2 Pad (tham chiếu)', 'Pad', '%%MIDI program 52'];
const refBars = { Pd: PAD.map((b, i) => i === 0 ? '!p!' + b : b), Bs: fromKind(BASS).map((b, i) => i === 0 ? '!mp!' + b : b) };
// parts: [[voiceDef, bars, gắn hợp âm?]]
function tune(x, title, parts, refs = [refPad, refBass]) {
  const all = parts.concat(refs.map(r => [r, refBars[r[0]], false]));
  return hdr(x, title, all.map(p => p[0])) + body(all.map(([v, bars, ch]) => [v[0], ch ? withChords(bars) : bars]));
}
const rest = n => Array(n).fill('z8');

// ---- Dante: rock guitar + power chord + scream ----
const DANTE2 = ['!mf!D A d3 e f2', 'e d A2 d4', '[Bf]2 [Bf] z [Bf]2 d f', '[_eb]2 [_eb] z g2 f _e',
  'a2 "^bend ½"g a d\'2 a f', 'g2 b d\'2 b g d', 'f2 d\' c\' b2 f d', '^c2 e ^c A2 "^pinch harm."a2',
  '!f![DA]2 [DA] z [DA]2 "^slide"d f', 'a/g/f/e/ d2 a2 d\'2', '[Bf]2 [Bf] z [Bf] d f b', '[_eb]2 [_eb] z [_eb]4',
  'd\'2 b g d\'3 b', 'c\'2 d\' c\' b f d2', 'b2 g _e B _e g b', 'e2 ^c A E A "^whammy"^c e'];
const SCREAM = [...rest(7), 'z4 "^SCREAM: Yeah! (ad-lib)"!ff!A4', ...rest(3), 'z4 "^SCREAM gầm (không cao độ)"!ff!_E4', ...rest(3), 'z4 "^SCREAM + whammy"!ff!A4'];
const danteAbc = tune(1, 'L3 Dante v2: "Cú nhảy quãng 5" (rock + scream)', [
  [['L3', 'treble', 'L3 Lead guitar (power chord + lead)', 'Lead', '%%MIDI program 29'], DANTE2, true],
  [['Vx', 'treble', 'L3 Vocal scream', 'Scr.', '%%MIDI program 54'], SCREAM, false],
]);

// ---- Vergil: violin cinematic, trầm buồn -> cao trào ----
const VERGIL2 = ['!p!"^con sord., legato"A6 B A', 'D4 "^lặng"z4', 'z2 F2 d4', '_e6 d2',
  'A6 B A', 'G4 z4', 'z2 B2 d2 f2', 'e4- e2 ^c e',
  '!f!"^senza sord., cresc."a6 b a', 'd\'4 a f d f', 'f\'4 d\'2 b2', 'g\'4 _e\'2 b2',
  '!ff!"^đỉnh: bấm kép"[gd\']4 [gb]2 [dg]2', '[fd\']4 [db]2 [Bf]2', '"^dim."_e\'4 b2 g2', '!mf!^c\'4 "^về motif"A4'];
const CELLO = [...rest(8), '!mf!F,8', 'A,8', 'D8', '_E8', 'D8', 'D4 F4', '_E4 G4', 'E4 ^C4'];
const vergilAbc = tune(1, 'L3 Vergil v2: "Tiếng thở dài" (violin)', [
  [['L3', 'treble', 'L3 Violin solo', 'Vln', '%%MIDI program 40'], VERGIL2, true],
  [['Vc', 'bass', "L3 Cello (chỉ câu A')", 'Vc', '%%MIDI program 42'], CELLO, false],
]);

// ---- Trish: piano + soprano opera + hợp xướng ----
const PIANO = ['(3!p!d_ed (3AGA d4', '(3fed (3AFD A4', '(3dfb (3fdB d4', '(3_egb (3g_eB G4',
  '(3d_ed (3AGA d4', "(3gbd' (3bgd B4", "(3fdB (3dfb d'4", "(3^c'ba (3e^cA E4",
  '(3d_ed (3AGA d2 f2', '(3agf (3edA d4', '(3dfb (3fdB F4', '(3GB_e (3g_eB G4',
  '(3Bdg (3dBG D4', '(3FBd (3fdB F4', '(3_EGB (3_egb g4', '(3^cBA (3E^CE A4'];
const SOP = ['!p!"^vocalise ah, trong trẻo"z4 d4', 'f4 d2 e2', 'f6 d2', '_e4 g4',
  'a4 f2 d2', 'g4 b4', 'f4 d4', 'e8',
  '!mf!a6 g f', 'd8', 'd4 f4', 'g6 f _e',
  'd4 g4', 'f6 d2', '_e4 B4', '^c4 e4'];
const CHOIR = ['[FA]8', '[FA]8', '[FB]8', '[GB]8', '[FA]8', '[GB]8', '[FB]8', '[EA]8',
  '[FA]8', '[FA]8', '[FB]8', '[GB]8', '[GB]8', '[FB]8', '[GB]8', '[EA]8'];
const choirBars = tag => CHOIR.map((b, i) => i === 0 ? `!pp!"^${tag}"` + b : b);
const SOPv = ['L3', 'treble', 'Soprano (opera)', 'S', '%%MIDI program 53'];
const PNOv = ['Pn', 'treble', 'Piano', 'Pno', '%%MIDI program 0'];
const CHv = ['Ch', 'treble', 'Hợp xướng (A + T)', 'Ch.', '%%MIDI program 52'];
const trishAbc = tune(1, 'Theme Trish v2: "Lời cầu nguyện" (piano + soprano + hợp xướng)', [
  [SOPv, SOP, true], [PNOv, PIANO, false], [CHv, choirBars('Ah... kiểu thánh ca'), false]], [refBass]);

const ANGEL2 = ['!mf!"^Angel: opera trong, thánh thiện"d2 f2 a4', 'a2 g2 f4', 'd2 f2 b4', '_e2 g2 b4',
  'a6 g f', 'g4 b4', 'a4 f4', '"^melisma"(3^cde (3efg a4',
  '!f!a4 d\'4', 'd\'2 a2 f2 a2', 'b6 a2', 'g4 b4',
  '!ff!"^đỉnh D6"d\'8', 'd\'2 c\' b f4', '_e2 g2 b4', '"^dim."a4 ^c4'];
const angelAbc = tune(2, 'L3 Trish Awake Angel v2', [
  [SOPv, ANGEL2, true], [PNOv, PIANO.map((b, i) => i === 0 ? b.replace('(3!p!', '(3"^piano 8va + harp"!mp!') : b), false],
  [CHv, choirBars('hợp xướng nữ, sáng'), false]], [refBass]);

const DEVIL2 = ['!mp!"^Devil: giọng nữ trầm, mơ màng (cảm giác half-time)"A4- A2 G F', 'F2 E2 D4', 'D6 C2', 'B,6 G,2',
  'd4- d2 c A', 'B4 G2 D2', 'F4 D4', '^C4 A,4',
  '!mf!"^dây phồng lên"f4- f2 e d', 'd4 A4', 'd4- d2 c B', 'B4 G4',
  'G2 F2 D4', 'F2 D2 B,4', '_E4 B,2 G,2', '!p!"^rơi xuống"A,8'];
const DEV_PNO = PAD.map(b => b.replace('8', '4') + ' ' + b.replace('8', '4')).map((b, i) => i === 0 ? '!p!"^piano hợp âm, pedal"' + b : b);
const STR = [...rest(4), '!p!A,8', 'B,8', 'F,8', 'E,8', '!f!A8', 'F8', 'F4 D4', '_E4 G,4', 'G,4 B,4', 'D4 F,4', 'G,4 B,4', 'A,4 E,4'];
const devilAbc = tune(3, 'L3 Trish Awake Devil v2: "Sa ngã"', [
  [['L3', 'treble', 'Contralto (vocalise)', 'A', '%%MIDI program 53'], DEVIL2, true],
  [PNOv, DEV_PNO, false],
  [['St', 'bass', 'Dàn dây trầm (viola + cello)', 'Str', '%%MIDI program 48'], STR, false]], [refBass]);

// ================= v2: trang =================
const META2 = 'Dựa trên: music-direction.md (v2), D-007, D-008 · 150 BPM · D thứ, vòng 16 ô = 25.6s · Draft v2 (bản v1 giữ nguyên ở file không hậu tố)';
const ad = (who, gate, extra = []) => [
  ['L3 ' + who, 'giai điệu 16 ô, cùng vòng hòa âm và độ dài với L2', gate, 'vào đầu ô kế, fade 1 phách; ra khi dưới ngưỡng 4s; đổi nhân vật = crossfade 1 phách'],
  ...extra,
  ['Pad, Bass trên trang', 'chỉ là tham chiếu L2/L1', '-', 'không xuất stem từ đây'],
];

page({
  file: 'l3-dante-v2.html', title: 'L3 Dante v2',
  meta: 'Leitmotif Dante · Style SS-SSS · ' + META2,
  concept: `<p>Giữ motif <b>"Cú nhảy quãng 5"</b> (ô 1-2) để Dante vẫn nhận ra được, nhưng đổi chất liệu theo brief: <b>rock guitar action, distortion, power chord, thỉnh thoảng metal scream</b>. Ô 3-4 và 11-12 là <b>power chord nhấn kiểu rock</b> (Bb5, Eb5: chặt-chặt-nghỉ), xen lead lick có bend. Câu A' mở bằng power chord D5 rồi lead chạy 16th, vươn lên D6.</p>
  <p><b>Scream</b> chỉ 3 lần mỗi vòng 25.6s, đúng chỗ chuyển câu: ô 8 (vào A'), ô 12 (Eb, chỗ nghiến nhất) và ô 16 (vòng lặp). Ad-lib ngắn, không lời bài hát, để "cợt nhả" mà không đè lời thoại. Ít là cố ý: scream lặp mỗi ô sẽ thành ồn.</p>`,
  harmony: harmonyStd.concat([['Ghi chú', '2, 14', '', "e trên Dm, c' trên Bb", 'appoggiatura, giải trong nửa ô'],
    ['Power chord', '3, 4, 9, 11, 12', '', 'Bb5, Eb5, D5 (quãng 5 + 8, không có quãng 3)', 'đúng màu rock; trung tính trưởng/thứ nên khớp mọi tầng']]),
  orch: [
    ['A', '1-8', 'lead guitar (high gain, delay 1/8 chấm); ô 3-4 power chord', '-', 'L2', 'L1', 'L0', 'mf', 'bend ô 5, pinch harmonic ô 8'],
    ["A'", '9-16', 'lead guitar double-track + power chord', 'scream ô 8, 12, 16', 'L2', 'L1', 'L0', 'f -> ff', 'whammy cuối ô 16 dẫn về ô 1'],
  ],
  adaptive: ad('Dante', 'Style rank SS-SSS', [['Scream', 'stem riêng (Vx)', 'Style SSS (đề xuất)', 'ở SS chỉ có lead guitar; lên SSS thêm stem scream ở đầu ô kế: phần thưởng nghe được']]),
  notes: [
    'Tầm lead: A3-D6 (cao độ thực), trong tầm guitar 24 phím.',
    'Scream ghi cao độ gần đúng (A4, Eb4) để giữ chỗ; ca sĩ scream tự do, điều quan trọng là vị trí và độ dài 2 phách.',
    'Mix: scream nằm dải 1-4 kHz trùng tiếng kiếm, nên duck -4 dB khi có đòn nặng và tắt khi có lời thoại.',
    'Tham khảo màu: DMC5 "Subhuman" (metal + synth + dây, có vocal gầm). Không trích riff hay giai điệu.',
  ],
  abc: danteAbc,
});

page({
  file: 'l3-vergil-v2.html', title: 'L3 Vergil v2',
  meta: 'Leitmotif Vergil · Tĩnh tâm cấp 3 · ' + META2,
  concept: `<p>Đổi shakuhachi sang <b>violin solo cinematic</b>, du dương trầm buồn rồi có đoạn đẩy lên cao trào. Motif <b>"Tiếng thở dài"</b> giữ nguyên: nốt A dài, nhích lên Bb rồi về, rơi xuống D và <b>im lặng nửa ô</b>.</p>
  <p><b>Câu A (ô 1-8)</b>: violin đeo con sordino (tiếng mờ), p, legato, nhiều khoảng lặng: kiểm soát, tiết chế. <b>Câu A' (ô 9-16)</b> là cao trào: bỏ sordino, lên quãng 8, khoảng lặng của motif bị lấp bằng chuỗi móc đơn (ô 10): <i>sự điềm tĩnh vỡ ra</i>. Lên F6, G6, đỉnh ô 13-14 bằng bấm kép (double stop) ff. Cello vào ở A' làm bè ngược hướng. Ô 15-16 rút về và rơi xuống A4 để nối lại motif ở ô 1.</p>`,
  harmony: harmonyStd.concat([['Ghi chú', '1, 4', '', 'Bb trên Dm (nốt thêu), d trên Eb (maj7)', 'màu buồn, có chủ ý']]),
  orch: [
    ['A', '1-8', 'violin solo, con sordino', '-', 'L2', 'L1', 'L0', 'p', 'gần mic, reverb hall dài; vibrato chậm'],
    ["A'", '9-16', 'violin solo senza sordino, 8va', 'cello F3-Eb4 ngược hướng', 'L2', 'L1', 'L0', 'f -> ff -> mf', 'ô 13-14 bấm kép; giữ legato'],
  ],
  adaptive: ad('Vergil', 'Tĩnh tâm cấp 3', [['Deflect / Iai', 'stinger', 'deflect / Iai cấp 3', 'xem stingers-v2.html (violin pizz. + chuông tay)']]),
  notes: [
    'Tầm violin: D4-G6, trong tầm thoải mái (G3-A6). Bấm kép ô 13-14 là quãng 5 / quãng 6, dễ bấm.',
    'Cello F3-Eb4, trong tầm.',
    'Mix: violin dễ chìm dưới guitar méo ở L1; nâng 2 dB khi L3 Vergil bật, EQ giảm guitar quanh 2-3 kHz.',
    'Cảm hứng kỹ thuật: dây tăng dần lên cao trào (Ronchetti, Eldest Souls), solo đơn độc cho nỗi cô đơn (Derivière, Vampyr). Không trích giai điệu.',
  ],
  abc: vergilAbc,
});

page({
  file: 'l3-trish-v2.html', title: 'L3 Trish v2',
  meta: 'Theme Trish + Awake Angel / Devil · ' + META2,
  concept: `<p><b>Theme gốc "Lời cầu nguyện"</b> (tune 1): piano nhẹ chơi motif <b>"Vòng xoáy"</b> (chùm ba quấn quanh D) làm nền, soprano opera hát vocalise "ah" trong trẻo bằng nốt dài, hợp xướng bè vào như thánh ca trong nhà thờ (hòa âm khối, ngân).</p>
  <p><b>Angel</b> (tune 2): soprano lên cao, giai điệu đi lên, melisma ô 8, đỉnh D6 ở ô 13. Màu maj7 (A trên Bb, ô 7) cho cảm giác sáng, thánh thiện. Piano 8va + harp.</p>
  <p><b>Devil "Sa ngã"</b> (tune 3): ngược với Angel. Giọng nữ trầm (contralto) mơ màng, mọi câu đều <b>đi xuống</b>; nốt chậm bằng nửa (cảm giác 75 BPM trên khung 150), piano hợp âm ngân, dàn dây phồng lên ở câu A' rồi rơi về A3. Màu ballad orchestral buồn kiểu "Young and Beautiful": chỉ lấy màu (giọng trầm, piano + dây dày, chậm, giọng thứ), không lấy giai điệu.</p>`,
  harmony: harmonyStd.concat([
    ['Angel', '7, 11', '', 'a trên Bb (maj7)', 'màu sáng có chủ ý'],
    ['Devil', 'cả bài', '', 'giai điệu hầu hết là nốt hợp âm, đi xuống từng bậc', 'buồn bằng hướng đi, không bằng nghịch tai'],
    ['Piano', '8, 16', '', 'Bb trên A (b9)', 'màu Phrygian dominant'],
  ]),
  orch: [
    ['Theme gốc', '1-16', 'soprano opera (vocalise)', 'piano: motif chùm ba', 'hợp xướng A + T "ah", ngân', 'L1', 'L0', 'p -> mf', 'reverb nhà thờ dài'],
    ['Angel', '1-16', 'soprano, lên cao, melisma', 'piano 8va + harp', 'hợp xướng nữ', 'L1', 'L0', 'mf -> ff', 'reverb sáng'],
    ['Devil', '1-16', 'contralto, câu đi xuống', 'piano hợp âm nửa nốt', 'dàn dây trầm (vào ô 5, phồng ô 9)', 'L1', 'L0 bản half-time (đề xuất)', 'mp -> mf -> p', 'giọng gần mic, reverb mờ'],
  ],
  adaptive: [
    ['Theme gốc', 'tune 1', 'đề xuất: Nộ đầy 100% chờ kích hoạt Awake; và màn chọn nhân vật', 'đầu ô kế'],
    ['L3 Angel', 'tune 2', 'Awake Angel', 'đầu ô kế sau khi kích hoạt (stinger ô trước)'],
    ['L3 Devil', 'tune 3', 'Awake Devil', 'như Angel; đề xuất L0 đổi sang bản trống half-time để khớp cảm giác chậm'],
  ],
  notes: [
    'Tầm: soprano D5-F5 (theme), C#5-D6 (Angel); contralto G3-F5; piano, dây, hợp xướng trong tầm.',
    'Không lời bài hát: opera là vocalise "ah/oh", không đè lời thoại.',
    'Devil giờ là buồn và sa ngã, không còn guitar 8 dây hủy diệt (v1). Cảm giác "hủy diệt" phải đến từ VFX và SFX; nhạc kể sự mất mát.',
    'Cả 3 bản dùng chung piano motif để người chơi nhận ra đây vẫn là một người.',
  ],
  abc: [trishAbc, angelAbc, devilAbc].join('\n\n'),
});

// ================= v2: khung combat gothic (D-009) =================
const TIMP = {
  Dm: 'D,2 A,, A,, D,2 A,, A,,', Bb: 'D,2 z2 D,2 z2', Gm: 'D,2 z2 D,2 z2',
  Eb: '"^cuộn, pedal D dưới Eb"D,8', A: 'A,,2 A,,2 A,, A,, A,,2',
};
const timp = CH.map(c => TIMP[c]);
const bells = CH.map((c, i) => i === 0 || i === 8 ? '"^chuông nhà thờ D5"d8' : 'z8');
const gothVoices = [
  ['Hc', 'treble', 'L2 Harpsichord ostinato', 'Hpsd', '%%MIDI program 6'],
  ['Pd', 'treble', 'L2 Organ + hợp xướng SATB', 'Org/Ch', '%%MIDI program 19'],
  ['Gt', 'bass', 'L1 Guitar riff drop-D', 'Gtr', '%%MIDI program 30'],
  ['Bs', 'bass', 'L1 Bass (+ organ pedal 16\')', 'Bass', '%%MIDI program 34'],
  ['Bl', 'treble', 'L0 Chuông nhà thờ', 'Bell', '%%MIDI program 14'],
  ['Tm', 'bass', 'L0 Timpani (D3, A2)', 'Timp', '%%MIDI program 47'],
  ['Dr', 'perc', 'L0 Trống metal', 'Dr', '%%MIDI channel 10'],
];
const f1 = (bars, dyn) => bars.map((b, i) => i === 0 ? dyn + b : b);
const gothBars = [
  ['Hc', withChords(f1(fromKind(KOTO), '!mp!'))],
  ['Pd', f1(PAD, '!mp!')],
  ['Gt', f1(fromKind(RIFF), '!f!')],
  ['Bs', f1(fromKind(BASS), '!f!')],
  ['Bl', f1(bells, '!f!')],
  ['Tm', f1(timp, '!f!')],
  ['Dr', f1(drums, '!f!')],
];
const gothFrameAbc = hdr(1, 'Combat v2 gothic: khung L0-L2 (vòng 16 ô = 25.6s)', gothVoices) + body(gothBars);
const gothOutroAbc = hdr(2, 'Combat v2 gothic: outro (2 ô)', gothVoices) + [
  '[V:Hc] "^Outro""Eb"z2 _e g b4 | "Dm"!fermata!d8 |]',
  '[V:Pd] !f![_EGB]8 | !fermata![DFA]8 |]',
  '[V:Gt] !ff![_E,,B,,_E,]8 | !fermata![D,,A,,D,]8 |]',
  '[V:Bs] !ff!_E,,,8 | !fermata!D,,,8 |]',
  '[V:Bl] z8 | !ff!!fermata!d8 |]',
  '[V:Tm] !ff!D,2 D,2 D, D, D,2 | !fermata!D,8 |]',
  '[V:Dr] !ff!c c c c c/c/c/c/ c c | !fermata![Fa]8 |]',
].join('\n');

page({
  file: 'combat-frame-v2.html', title: 'Combat Frame v2 Gothic',
  meta: 'Khung combat chung, màu gothic (D-009) · ' + META2.replace('D-008', 'D-008, D-009'),
  concept: `<p>Cùng nốt, cùng hòa âm, cùng độ dài với khung v1 để mọi L3 v2 vẫn khớp; chỉ đổi màu từ đền Nhật sang <b>nhà thờ gothic</b>. Taiko thay bằng <b>timpani</b> (chỉnh D3 và A2) và <b>chuông nhà thờ D5</b> ở ô 1 và 9. Koto thay bằng <b>harpsichord</b> giữ đúng nhịp gảy lệch phách cũ. Pad thành <b>organ ống + hợp xướng SATB</b> hát âm tiết (oh/ah), không lời.</p>
  <p>Nốt Eb (bII) giữ nguyên: trong ngữ cảnh mới nó nghe như hợp âm Neapolitan / Phrygian của nhạc nhà thờ và requiem, nên màu "quỷ + thánh đường" đến từ chính hòa âm cũ. Nền vẫn là metal drop-D.</p>`,
  harmony: harmonyStd.concat([['Outro', '2 ô', 'D', 'Eb -> Dm (bII -> i)', 'kết Phrygian, organ + chuông ngân']]),
  orch: [
    ['A', '1-8', '- (L3 nếu bật)', 'harpsichord ostinato (L2)', 'organ ống (principal + mixture) + hợp xướng nam "oh" (L2)', 'riff guitar drop-D + bass pick (L1)', 'trống metal + timpani D/A + chuông D5 ở ô 1 (L0)', 'f (L0-L1), mp (L2)', 'reverb thánh đường dài cho chuông và organ, khô cho guitar'],
    ["A'", '9-16', '- (L3 nếu bật)', 'harpsichord', 'organ thêm stop 16\', hợp xướng đủ SATB "ah"', 'bass + organ pedal 16\' nhân đôi', 'timpani cuộn ở ô Eb, chuông ô 9, fill snare ô 16', 'f', 'ô 15-16 organ full'],
    ['Outro', '2 ô', '-', 'harpsichord Eb-G-Bb -> D', 'organ Eb -> Dm, fermata', 'Eb5 -> D5 ngân', 'timpani dồn + chuông cuối + crash', 'ff -> ngân', 'đuôi reverb thánh đường'],
  ],
  adaptive: [
    ['L0 trống + timpani + chuông', 'Dr, Tm, Bl', 'vào trận (mọi nhân vật)', 'đầu ô kế, không fade; chuông + crash là tiếng "vào trận"'],
    ['L1 riff + bass', 'Gt, Bs', 'Dante rank B · Vergil cấp 1 · Trish Nộ >= 33%', 'vào đầu ô kế, fade 1 phách; ra khi dưới ngưỡng 4s'],
    ['L2 organ + hợp xướng + harpsichord', 'Pd, Hc', 'Dante A-S · Vergil cấp 2 · Trish >= 66%', 'như L1'],
    ['L3', 'l3-*-v2.html', 'Dante SS-SSS · Vergil cấp 3 · Trish Awake', 'như L1'],
    ['Outro', 'tune 2', 'hết địch', 'chờ đầu ô kế, phát outro, mọi stem fade 1 phách'],
  ],
  notes: [
    'Stem L0, L1, L2 mỗi stem đúng 25.6s, 48 kHz; render 2 vòng, lấy vòng 2.',
    'Organ và hợp xướng ở L2 chiếm dải trung giống violin (Vergil) và soprano (Trish): khi L3 của hai người này bật, hạ L2 -2 dB và bỏ stop mixture của organ (đề xuất có 2 bản mix L2, hoặc EQ động).',
    'Hợp xướng chỉ âm tiết, không lời Latin thật, để khỏi đè lời thoại và khỏi dịch.',
    'Chuông D5 hợp với Dm, Bb, Gm; chỉ đánh ở ô 1, 9 và cuối outro để không nhàm.',
    'Bản v1 màu Nhật vẫn ở combat-frame.html.',
  ],
  abc: gothFrameAbc + '\n\n' + gothOutroAbc,
});

// ---------- Stingers v2 ----------
const st2 = (x, t, voices, lines) => `X:${x}\nT:${t}\nC:music-lead v2\nM:4/4\nL:1/8\nQ:1/4=150\n${PERC}\n` +
  voices.map(([id, clef, name, midi]) => `V:${id} clef=${clef} name="${name}"\n${midi}`).join('\n') + '\nK:Dm\n' + lines.join('\n');
const sting2Abc = [
  st2(1, 'Stinger: Dante lên SSS (bản trên ô Dm)', [['Ld', 'treble', 'Lead guitar', '%%MIDI program 29'], ['Vx', 'treble', 'Scream', '%%MIDI program 54'], ['Dr', 'perc', 'Trống', '%%MIDI channel 10']],
    ['[V:Ld] "^dịch theo gốc hợp âm của ô""Dm"!ff!D A d a "^pinch harm. + whammy"d\'4 |]', '[V:Vx] z4 "^SCREAM ngắn"!ff!A4 |]', '[V:Dr] !ff![Fa]2 z2 z4 |]']),
  st2(2, 'Stinger: Vergil deflect (tức thì)', [['Vn', 'treble', 'Violin pizz. + chuông tay D6', '%%MIDI program 45']],
    ['[V:Vn] "^bus nhạc -12dB 0.3s; pizz. + handbell + thép cạo"!sfz!d\'8 |]']),
  st2(3, 'Stinger: Vergil Iai cấp 3', [['Vn', 'treble', 'Violin', '%%MIDI program 40'], ['Og', 'bass', 'Organ + guitar (mọi stem)', '%%MIDI program 19'], ['Dr', 'perc', 'Trống', '%%MIDI channel 10']],
    ['[V:Vn] "^mọi stem câm 0.5s"z4 "^chém (SFX)"z4 | z4 "^vào lại"!ff![Ad]4 |]', '[V:Og] z8 | z4 !ff![D,,A,,D,]4 |]', '[V:Dr] z8 | z4 !ff![Fa]4 |]']),
  st2(4, 'Stinger: Trish Awake Devil "Sa ngã"', [['Ct', 'treble', 'Contralto + hợp xướng nữ thấp', '%%MIDI program 53'], ['Pn', 'bass', 'Piano trầm', '%%MIDI program 0']],
    ['[V:Ct] !mf!"^rơi xuống, mờ dần"a2 f2 d2 A2 |]', '[V:Pn] !p![D,,A,,D,]8 |]']),
  st2(5, 'Stinger: Trish Awake Angel', [['Ch', 'treble', 'Soprano + hợp xướng nữ', '%%MIDI program 52'], ['Bl', 'treble', 'Chuông + celesta', '%%MIDI program 14']],
    ['[V:Ch] !p!"^cresc. tới f"[DAd]8 |]', '[V:Bl] d a d\' a d\'4 |]']),
].join('\n\n');

page({
  file: 'stingers-v2.html', title: 'Combat Stingers v2',
  meta: 'Stinger theo D-007, màu D-008/D-009 · ' + META2,
  concept: `<p>Cùng 5 sự kiện như v1, đổi âm sắc theo màu mới. Dante SSS thêm một scream ngắn. Vergil deflect: <b>violin pizzicato + chuông tay D6</b> thay chuông rin. Iai vào lại bằng organ + violin. Trish Devil giờ là một câu hát đi xuống trên piano trầm (sa ngã), Angel là hợp xướng + chuông sáng.</p>`,
  harmony: [
    ['SSS', '1 ô', 'theo ô', "D-A-d-a-d' (power chord)", 'dịch theo gốc hợp âm của ô; cần 5 bản'],
    ['Deflect', 'tức thì', 'D', 'D6', 'D là nốt hợp âm của Dm, Bb, Gm; maj7 trên Eb, sus4 trên A'],
    ['Iai', '2 ô', 'D', 'D5 + A-d', 'vào lại bằng tonic'],
    ['Devil', '1 ô', 'Dm', 'A-F-D-A đi xuống', 'rải Dm xuống, không nghịch tai'],
    ['Angel', '1 ô', 'D', 'D-A-d', 'trung tính, sáng'],
  ],
  orch: [
    ['SSS', '1', 'lead guitar', 'scream 2 phách cuối', '-', '-', 'crash + kick', 'ff', 'pick slide trước phách 1'],
    ['Deflect', '-', 'violin pizz. D6', 'chuông tay + thép cạo', '-', '-', '-', 'sfz', 'bus -12 dB 0.3s: dip 30 ms, giữ, hồi 80 ms'],
    ['Iai', '2', 'violin [A d] bấm kép', '-', 'organ', 'guitar D5', 'crash + timpani', 'ff', 'stem chạy ngầm để giữ đồng bộ'],
    ['Devil', '1', 'contralto', 'hợp xướng nữ thấp ngân', 'piano D trầm', '-', '-', 'mf -> p', 'reverb mờ, kéo dài sang ô kế'],
    ['Angel', '1', 'soprano + hợp xướng D-A-d', 'chuông + celesta', '-', '-', '-', 'p -> f', 'reverb sáng'],
  ],
  adaptive: [
    ['Dante SSS', 'tune 1', 'rank đạt SSS', 'đầu ô kế, bản theo hợp âm ô đó'],
    ['Vergil deflect', 'tune 2', 'deflect thành công', 'ngay lập tức; parry liên tiếp luân phiên D6 / A5'],
    ['Vergil Iai cấp 3', 'tune 3', 'Iai ở Tĩnh tâm cấp 3', 'câm ngay, vào lại phách mạnh kế sau 0.5s'],
    ['Trish Awake Devil / Angel', 'tune 4 / 5', 'kích hoạt Awake', 'đầu ô kế; ô sau L3 biến thể vào'],
  ],
  notes: ['Bản v1 (chuông rin, guitar 8 dây) vẫn ở stingers.html.'],
  abc: sting2Abc,
});

// ================= Sửa nhạc cụ: abcjs chỉ nhận %%MIDI program khi nằm ngay sau "V:id" trong thân bài =================
function fixMidi(abc) {
  return abc.split(/\n(?=X:)/).map(t => {
    const lines = t.split('\n');
    const k = lines.findIndex(l => /^K:/.test(l));
    const midi = {};
    let cur = null;
    const head = [];
    lines.slice(0, k).forEach(l => {
      const v = l.match(/^V:(\S+)/);
      if (v) { cur = v[1]; head.push(l); return; }
      if (/^%%MIDI/.test(l) && cur) { (midi[cur] = midi[cur] || []).push(l); return; }
      head.push(l);
    });
    const seen = new Set();
    const bodyLines = lines.slice(k + 1).map(l => {
      const m = l.match(/^\[V:(\S+?)\]\s?(.*)$/);
      if (!m || seen.has(m[1]) || !midi[m[1]]) return l;
      seen.add(m[1]);
      return `V:${m[1]}\n${midi[m[1]].join('\n')}\n${m[2]}`;
    });
    return [...head, lines[k], ...bodyLines].join('\n');
  }).join('\n');
}

// ================= Đoạn B (mới): F C Dm Bb Gm C Bb A, giọng F trưởng tương quan =================
const B_CH = ['F', 'C', 'Dm', 'Bb', 'Gm', 'C', 'Bb', 'A'];
const PC = { F: ['F,,', 'C,'], C: ['C,', 'G,'], Dm: ['D,,', 'A,,'], Bb: ['B,,', 'F,'], Gm: ['G,,', 'D,'], A: ['A,,', 'E,'] };
const BROOT = { F: ['F,,', 'C,'], C: ['C,,', 'G,,'], Dm: ['D,,', 'A,,'], Bb: ['B,,,', 'F,,'], Gm: ['G,,,', 'D,,'] };
const FB = {
  Hc: { F: 'z2 c f z2 a f', C: 'z2 c e z2 g e', Dm: 'z2 d f z2 a f', Bb: 'z2 B d z2 f d', Gm: 'z2 G B z2 d B', A: 'z2 ^c e z2 a e' },
  Pd: { F: '[FAc]8', C: '[EGc]8', Dm: '[DFA]8', Bb: '[DFB]8', Gm: '[DGB]8', A: '[^CEA]8' },
};
const fb = {
  Hc: B_CH.map(c => FB.Hc[c]),
  Pd: B_CH.map(c => FB.Pd[c]),
  Gt: B_CH.map(c => { const p = `[${PC[c][0]}${PC[c][1]}]`; return `${p}2 ${p}${p} ${p}2 ${p}${p}`; }),
  Bs: B_CH.map(c => c === 'A' ? BASS.A : `${BROOT[c][0]}2 ${BROOT[c][0]}${BROOT[c][0]} ${BROOT[c][0]}2 ${BROOT[c][1]}${BROOT[c][0]}`),
  Bl: B_CH.map((c, i) => i === 0 ? '"^chuông"c8' : 'z8'),
  Tm: B_CH.map(c => ({ F: 'A,,2 z2 A,,2 z2', C: 'z8', Dm: TIMP.Dm, Bb: TIMP.Bb, Gm: TIMP.Gm, A: TIMP.A })[c]),
  Dr: B_CH.map((c, i) => i === 0 ? DR.first : i === 7 ? DR.fill : i % 2 ? DR.b : DR.a),
};
const ff = { Hc: fromKind(KOTO), Pd: PAD, Gt: fromKind(RIFF), Bs: fromKind(BASS), Bl: bells, Tm: timp, Dr: drums };
const fo = {
  Hc: ['z2 _e g b4', '!fermata!d8'], Pd: ['[_EGB]8', '!fermata![DFA]8'], Gt: ['[_E,,B,,_E,]8', '!fermata![D,,A,,D,]8'],
  Bs: ['_E,,,8', '!fermata!D,,,8'], Bl: ['z8', '!fermata!d8'], Tm: ['D,2 D,2 D, D, D,2', '!fermata!D,8'], Dr: ['c c c c c/c/c/c/ c c', '!fermata![Fa]8'],
};

// Giai điệu đoạn B
const DANTE_B = ['c2 f2 a3 g', 'g2 e2 c4', 'd2 f2 a2 d\'2', 'd\'3 c\' b2 f2', 'g2 b2 d\'4', 'c\'2 d\' c\' g2 e2', 'd2 f2 b2 d\'2', '^c\'2 a2 e2 ^c2'];
const VERGIL_B = ['c6 f2', 'e4 g4', 'f6 a2', 'd\'4 b2 f2', '"^cresc."g4 b2 d\'2', 'e\'4 c\'2 g2', 'f\'4 d\'2 b2', 'e\'4 ^c\'2 a2'];
const CELLO_B = ['A,4 F,4', 'G,4 E,4', 'F,4 D,4', 'F,4 D,4', 'G,4 B,4', 'G,4 E,4', 'F,4 D,4', 'E,4 ^C,4'];
const TRISH_B = ['c4 f4', 'g4 e4', 'f4 a4', 'b6 a2', 'g4 d\'4', 'c\'4 g4', 'd\'4 b4', 'a4 e2 ^c2'];
const CHOIR_B = ['[Ac]8', '[Gc]8', '[FA]8', '[FB]8', '[GB]8', '[Gc]8', '[FB]8', '[EA]8'];
const PNO_B = fb.Pd.map(b => b.replace('8', '4') + ' ' + b.replace('8', '4'));

// ================= Ghép demo =================
const FRAME_CH_INTRO = CH.slice(0, 4);
const stripDyn = b => b.replace(/^(!(pp|p|mp|mf|f|ff|sfz)!)+/, '');
function put(bar, pre) {
  if (!pre) return bar;
  return bar.startsWith('(3') ? '(3' + pre + bar.slice(2) : pre + bar;
}
// sections: [{label, chords, dyn, v: {id: bars}}]; voice không có trong v = nghỉ
function demo(x, title, voices, sections) {
  const out = {};
  voices.forEach(([id]) => { out[id] = []; });
  sections.forEach(s => {
    const n = s.chords.length;
    voices.forEach(([id]) => {
      const src = s.v[id];
      for (let i = 0; i < n; i++) {
        let bar = src ? src[i] : 'z8';
        let pre = '';
        if (id === 'L3') pre += (i === 0 ? `"^${s.label}"` : '') + `"${s.chords[i]}"`;
        if (i === 0 && src) { bar = stripDyn(bar); pre += `!${(s.dyn[id] || s.dyn.all)}!`; }
        out[id].push(put(bar, pre));
      }
    });
  });
  const total = out[voices[0][0]].length;
  const chunks = [];
  for (let s = 0; s < total; s += 4)
    voices.forEach(([id]) => chunks.push(`[V:${id}] ${out[id].slice(s, s + 4).join(' | ')} ${s + 4 >= total ? '|]' : '|'}`));
  return hdr(x, title, voices) + chunks.join('\n');
}
const sl = (arr, a, b) => arr.slice(a, b);
const frameSl = (a, b, ids) => Object.fromEntries(ids.map(id => [id, sl(ff[id], a, b)]));
const pickO = (o, ids) => Object.fromEntries(ids.map(id => [id, o[id]]));
const ALL = ['Hc', 'Pd', 'Gt', 'Bs', 'Bl', 'Tm', 'Dr'];
const OUT_CH = ['Eb', 'Dm'];
const A_CH = CH.slice(0, 8), C_CH = CH.slice(8, 16);
const frameV = gothVoices;

const danteDemo = demo(1, 'Demo Dante: rock + scream (Intro - A - B - Cao trào - Kết)', [
  ['L3', 'treble', 'Lead guitar', 'Lead', '%%MIDI program 29'], ['Vx', 'treble', 'Scream', 'Scr.', '%%MIDI program 54'], ...frameV], [
  { label: 'Intro: chỉ trống + riff', chords: FRAME_CH_INTRO, dyn: { all: 'f' }, v: frameSl(0, 4, ['Gt', 'Bs', 'Dr', 'Bl']) },
  { label: 'A', chords: A_CH, dyn: { all: 'f', L3: 'mf' }, v: { L3: sl(DANTE2, 0, 8), Vx: sl(SCREAM, 0, 8), ...frameSl(0, 8, ['Gt', 'Bs', 'Dr', 'Tm']) } },
  { label: 'B: hook mới (F trưởng)', chords: B_CH, dyn: { all: 'f' }, v: { L3: DANTE_B, Vx: [...rest(7), 'z4 "^SCREAM"A4'], ...pickO(fb, ALL) } },
  { label: 'Cao trào', chords: C_CH, dyn: { all: 'ff' }, v: { L3: sl(DANTE2, 8, 16), Vx: sl(SCREAM, 8, 16), ...frameSl(8, 16, ALL) } },
  { label: 'Kết', chords: OUT_CH, dyn: { all: 'ff' }, v: { L3: ['[_eb]8', '!fermata![Ad]8'], ...pickO(fo, ALL) } },
]);

const vergilDemo = demo(1, 'Demo Vergil: violin (Intro - A - B - Cao trào - Kết)', [
  ['L3', 'treble', 'Violin solo', 'Vln', '%%MIDI program 40'], ['Vc', 'bass', 'Cello', 'Vc', '%%MIDI program 42'], ...frameV], [
  { label: 'Intro: organ + chuông, nhà thờ vắng', chords: FRAME_CH_INTRO, dyn: { all: 'pp' }, v: frameSl(0, 4, ['Pd', 'Tm', 'Bl']) },
  { label: 'A: trầm buồn', chords: A_CH, dyn: { all: 'p' }, v: { L3: sl(VERGIL2, 0, 8), ...frameSl(0, 8, ['Pd', 'Bs', 'Tm']) } },
  { label: 'B: giai điệu mới, đẩy dần', chords: B_CH, dyn: { all: 'mf', L3: 'mp' }, v: { L3: VERGIL_B, Vc: CELLO_B, ...pickO(fb, ['Pd', 'Bs', 'Hc', 'Tm', 'Dr']) } },
  { label: 'Cao trào', chords: C_CH, dyn: { all: 'ff', L3: 'f' }, v: { L3: sl(VERGIL2, 8, 16), Vc: sl(CELLO, 8, 16), ...frameSl(8, 16, ALL) } },
  { label: 'Kết', chords: OUT_CH, dyn: { all: 'f' }, v: { L3: ['_e8', '!fermata!d8'], ...pickO(fo, ALL) } },
]);

const trishDemo = demo(1, 'Demo Trish: piano + opera + hợp xướng (Intro - A - B - Cao trào - Kết)', [
  ['L3', 'treble', 'Soprano', 'S', '%%MIDI program 53'], ['Ch', 'treble', 'Hợp xướng', 'Ch.', '%%MIDI program 52'], ['Pn', 'treble', 'Piano', 'Pno', '%%MIDI program 0'], ...frameV], [
  { label: 'Intro: piano solo', chords: FRAME_CH_INTRO, dyn: { all: 'p' }, v: { Pn: sl(PIANO, 0, 4) } },
  { label: 'A: lời cầu nguyện', chords: A_CH, dyn: { all: 'p', Ch: 'pp' }, v: { L3: sl(SOP, 0, 8), Pn: sl(PIANO, 0, 8), Ch: sl(CHOIR, 0, 8), ...frameSl(0, 8, ['Bs', 'Tm']) } },
  { label: 'B: thánh ca mới', chords: B_CH, dyn: { all: 'mf' }, v: { L3: TRISH_B, Ch: CHOIR_B, Pn: PNO_B, ...pickO(fb, ['Pd', 'Bs', 'Bl', 'Tm', 'Dr']) } },
  { label: 'Cao trào: soprano lên D6', chords: C_CH, dyn: { all: 'ff' }, v: { L3: sl(ANGEL2, 8, 16), Ch: sl(CHOIR, 8, 16), Pn: sl(PIANO, 8, 16), ...frameSl(8, 16, ['Pd', 'Gt', 'Bs', 'Bl', 'Tm', 'Dr']) } },
  { label: 'Kết', chords: OUT_CH, dyn: { all: 'f' }, v: { L3: ['g8', '!fermata!f8'], Ch: ['[GB]8', '!fermata![FA]8'], Pn: ['[_EGB]8', '!fermata![DFA]8'], ...pickO(fo, ['Pd', 'Bs', 'Bl', 'Tm']) } },
]);

const demoHarm = [
  ['Intro', '4', 'D thứ', 'Dm Dm Bb Eb', 'chỉ vài tầng'],
  ['A', '8', 'D thứ', 'Dm Dm Bb Eb | Dm Gm Bb A', 'motif nhân vật, hòa âm khung'],
  ['B', '8', '<b>F trưởng</b> (tương quan)', 'F C Dm Bb | Gm C Bb A', 'giai điệu mới + hòa âm sáng hơn = tương phản; A ở cuối kéo về D'],
  ['Cao trào', '8', 'D thứ', 'Dm Dm Bb Eb | Gm Bb Eb A', 'câu A\' với mọi tầng, ff'],
  ['Kết', '2', 'D', 'Eb -> Dm', 'bII -> i'],
];
const demoAdapt = [
  ['Intro / A', 'tầng thấp', 'thanh thấp', 'trong game: L0-L1'],
  ['B', 'đoạn mới', 'đề xuất: vòng combat 32 ô = A + B', 'người chơi nghe 2 giai điệu xen nhau thay vì 1 câu lặp'],
  ['Cao trào', 'mọi tầng + L3 câu A\'', 'thanh đầy (SSS / cấp 3 / Awake)', 'đỉnh bài được "kiếm" bằng cách chơi'],
];
const demoPage = (file, title, who, concept, orch, abc) => page({
  file, title, meta: `Bản demo nghe thử của ${who} · 30 ô ≈ 48s · ` + META2,
  concept, harmony: demoHarm, orch, adaptive: demoAdapt,
  notes: ['Đây là bản demo nghe cấu trúc (một lần từ đầu đến cuối). Trong game nhạc vẫn lặp và lên tầng theo thanh; xem file l3-*-v2.html cho từng stem.',
    'Synth trình duyệt dùng soundfont GM: guitar, violin, giọng chỉ là âm sắc gần đúng.'],
  abc,
});

demoPage('demo-dante.html', 'Demo Dante', 'Dante',
  `<p>Intro chỉ trống + riff. <b>A</b>: motif "quãng 5". <b>B</b>: hook guitar <i>mới</i> trên F trưởng (F-C-Dm-Bb), dễ ngân nga, scream ở cuối đoạn. <b>Cao trào</b>: câu A' ff với organ, hợp xướng, timpani, chuông, scream 3 lần. <b>Kết</b> Eb -> D.</p>`,
  [['Intro', '1-4', '-', '-', '-', 'riff + bass', 'trống + chuông', 'f', ''], ['A', '5-12', 'lead guitar', 'scream ô 12', '-', 'riff + bass', 'trống + timpani', 'mf/f', ''],
    ['B', '13-20', 'lead guitar (hook mới)', 'scream ô 20', 'organ + hợp xướng + harpsichord', 'gallop power chord', 'đủ', 'f', ''], ['Cao trào', '21-28', 'lead guitar', 'scream', 'đủ', 'đủ', 'đủ', 'ff', '']],
  danteDemo);
demoPage('demo-vergil.html', 'Demo Vergil', 'Vergil',
  `<p>Intro: organ + chuông, rất nhỏ. <b>A</b>: violin "thở dài", chỉ organ và bass đỡ: trầm buồn. <b>B</b>: giai điệu violin <i>mới</i> đi lên liên tục từ C5 tới E6, cello đối bè, trống vào: đây là đoạn đẩy. <b>Cao trào</b>: câu A' ff với toàn bộ ban, violin bấm kép. <b>Kết</b> Eb -> D.</p>`,
  [['Intro', '1-4', '-', '-', 'organ', '-', 'timpani + chuông', 'pp', ''], ['A', '5-12', 'violin con sord.', '-', 'organ', 'bass', 'timpani', 'p', ''],
    ['B', '13-20', 'violin (giai điệu mới, đi lên)', 'cello', 'organ + harpsichord', 'bass', 'trống + timpani', 'mp -> mf', ''], ['Cao trào', '21-28', 'violin 8va, bấm kép', 'cello', 'đủ', 'riff + bass', 'đủ', 'ff', '']],
  vergilDemo);
demoPage('demo-trish.html', 'Demo Trish', 'Trish',
  `<p>Intro: piano solo motif "Vòng xoáy". <b>A</b>: soprano + hợp xướng rất nhỏ, như cầu nguyện. <b>B</b>: thánh ca <i>mới</i> trên F trưởng, piano đổi sang hợp âm khối, organ và trống vào. <b>Cao trào</b>: soprano lên D6 (câu A' của Angel), hợp xướng, organ, guitar, ff. <b>Kết</b> Eb -> D.</p>`,
  [['Intro', '1-4', '-', '-', '-', '-', '-', 'p', 'piano solo'], ['A', '5-12', 'soprano', 'piano', 'hợp xướng pp', 'bass', 'timpani', 'p', ''],
    ['B', '13-20', 'soprano (thánh ca mới)', 'piano hợp âm khối', 'organ + hợp xướng', 'bass', 'trống + chuông', 'mf', ''], ['Cao trào', '21-28', 'soprano lên D6', 'piano', 'organ + hợp xướng', 'riff + bass', 'đủ', 'ff', '']],
  trishDemo);

// ================= v3: vòng combat 32 ô = A + B + A' + B' (D-010) =================
const B2_CH = ['F', 'C', 'Dm', 'Bb', 'Gm', 'C', 'Eb', 'A'];
const CH32 = [...CH.slice(0, 8), ...B_CH, ...CH.slice(8), ...B2_CH];
const LBL32 = { 0: 'A (điểm lặp)', 8: 'B', 16: "A' (cao trào)", 24: "B'" };
const fb2 = {
  Hc: fb.Hc.map((b, i) => i === 6 ? KOTO.Eb : b),
  Pd: fb.Pd.map((b, i) => i === 6 ? '[_EGB]8' : b),
  Gt: fb.Gt.map((b, i) => i === 6 ? '[_E,,B,,]2 [_E,,B,,][_E,,B,,] [_E,,B,,]2 [_E,,B,,][_E,,B,,]' : b),
  Bs: fb.Bs.map((b, i) => i === 6 ? BASS.Eb : b),
  Bl: rest(8),
  Tm: fb.Tm.map((b, i) => i === 6 ? TIMP.Eb : b),
  Dr: fb.Dr.map((b, i) => i === 0 ? DR.a : b),
};
const cat32 = id => [...ff[id].slice(0, 8), ...fb[id], ...ff[id].slice(8), ...fb2[id]];
const L32 = (a, b, a2, b2) => [...a, ...b, ...a2, ...b2];
function withChords32(bars) {
  return bars.map((b, i) => put(b, (LBL32[i] ? `"^${LBL32[i]}"` : '') + `"${CH32[i]}"`));
}
function body32(voices) {
  const out = [];
  for (let s = 0; s < 32; s += 4)
    voices.forEach(([id, bars]) => out.push(`[V:${id}] ${s === 0 ? '|: ' : ''}${bars.slice(s, s + 4).join(' | ')} ${s === 28 ? ':|' : '|'}`));
  return out.join('\n');
}
const ref32 = { Pd: cat32('Pd').map((b, i) => i === 0 ? '!p!' + b : b), Bs: cat32('Bs').map((b, i) => i === 0 ? '!mp!' + b : b) };
function tune32(x, title, parts, refs = [refPad, refBass]) {
  const all = parts.concat(refs.map(r => [r, ref32[r[0]], false]));
  return hdr(x, title, all.map(p => p[0])) + body32(all.map(([v, bars, ch]) => [v[0], ch ? withChords32(bars) : bars]));
}

// ---- Giai điệu B' (và B cho Angel / Devil) ----
const DANTE_B2 = ["f2 a2 c'3 a", "g2 c'2 e'4", ...DANTE_B.slice(2, 6), "_e2 g2 b2 _e'2", "^c'2 e' ^c' a2 \"^whammy\"e2"];
const VERGIL_B2 = ['!f!a6 f2', 'g4 e4', 'f4 d4', '"^dim."d6 B2', 'G4 B4', 'G4 E4', '_E4 G4', '!p!^C4 E4'];
const CELLO_B2 = ['F,8', 'E,8', 'D,8', 'D,8', 'G,,8', 'C,8', '_E,8', 'A,,8'];
const TRISH_B2 = ["a4 c'4", "c'4 g4", 'a4 f4', 'f6 d2', 'd4 g4', 'e4 g4', 'g4 b4', 'a4 ^c4'];
const CHOIR_B2 = CHOIR_B.map((b, i) => i === 6 ? '[GB]8' : b);
const PNO_B2 = fb2.Pd.map(b => b.replace('8', '4') + ' ' + b.replace('8', '4'));
const ANGEL_B = ["f2 a2 c'4", "e2 g2 c'4", "d'4 a4", "b2 d'2 f4", "g2 b2 d'4", "c'2 e2 g4", 'b2 a2 f4', "^c'4 a4"];
const ANGEL_B2 = ["a4 c'4", ...ANGEL_B.slice(1, 6), 'b4 g4', '"^melisma"(3efg (3a^c\'a e4'];
const DEVIL_B = ['c4- c2 A F', 'G4 E4', 'A4 F2 D2', 'D4 B,4', 'd4- d2 B G', 'c4 G2 E2', 'F4 D4', 'E4 ^C4'];
const DEVIL_B2 = [...DEVIL_B.slice(0, 6), 'G4 _E4', '"^rơi"A,8'];
const STR_B = ['F,4 A,4', 'G,4 E,4', 'F,4 D,4', 'D,4 F,4', 'G,4 B,4', 'G,4 E,4', 'F,4 D,4', 'E,4 A,,4'];
const STR_B2 = STR_B.map((b, i) => i === 6 ? 'G,4 _E,4' : i === 7 ? 'E,4 ^C,4' : b);
const DEV_PNO_B = fb.Pd.map(b => b.replace('8', '4') + ' ' + b.replace('8', '4'));

const A1 = a => a.slice(0, 8), A2 = a => a.slice(8, 16);
const SCREAM32 = L32(A1(SCREAM), rest(8), A2(SCREAM).map((b, i) => i === 7 ? 'z8' : b), [...rest(7), 'z4 "^SCREAM + whammy"!ff!A4']);

// ---- Khung v3 ----
const frame32Abc = hdr(1, "Combat v3 gothic: khung L0-L2 (vòng 32 ô = 51.2s)", gothVoices) + body32([
  ['Hc', withChords32(cat32('Hc').map((b, i) => i === 0 ? '!mp!' + b : b))],
  ['Pd', cat32('Pd').map((b, i) => i === 0 ? '!mp!' + b : b)],
  ['Gt', cat32('Gt').map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Bs', cat32('Bs').map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Bl', cat32('Bl').map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Tm', cat32('Tm').map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Dr', cat32('Dr').map((b, i) => i === 0 ? '!f!' + b : b)],
]);
const harm32 = [
  ['A', '1-8', 'D thứ', 'Dm Dm Bb Eb | Dm Gm Bb A', 'motif nhân vật'],
  ['B', '9-16', 'F trưởng tương quan', 'F C Dm Bb | Gm C Bb A', 'giai điệu mới, sáng hơn: tương phản'],
  ["A'", '17-24', 'D thứ', 'Dm Dm Bb Eb | Gm Bb Eb A', 'cao trào của vòng'],
  ["B'", '25-32', 'F -> D', 'F C Dm Bb | Gm C <b>Eb A</b>', 'bII -> V ở ô 31-32 kéo mạnh về ô 1'],
];
const META3 = 'Dựa trên: music-direction.md, D-007, D-008, D-009, D-010 · 150 BPM · D thứ · vòng 32 ô = 51.2s · Draft v3';

page({
  file: 'combat-frame-v3.html', title: 'Combat Frame v3',
  meta: 'Khung combat chung gothic, vòng 32 ô · ' + META3,
  concept: `<p>Vòng combat dài gấp đôi: <b>A - B - A' - B'</b>. A và A' là khung gothic v2; B và B' là đoạn mới trên F trưởng, riff đổi sang gallop power chord, organ và harpsichord đổi hợp âm. B' thay 2 ô cuối bằng Eb -> A để cú bII -> V kéo về ô 1. Chuông ở ô 1, 9, 17. Outro giữ như v2 (tune 2).</p>`,
  harmony: harm32.concat([['Outro', '2 ô', 'D', 'Eb -> Dm', 'như v2']]),
  orch: [
    ['A / A\'', '1-8, 17-24', '- (L3)', 'harpsichord', 'organ + hợp xướng', 'riff chug drop-D + bass', 'trống + timpani + chuông', 'f', 'như v2'],
    ['B / B\'', '9-16, 25-32', '- (L3)', 'harpsichord (hợp âm F trưởng)', 'organ + hợp xướng', 'gallop power chord + bass gốc-quãng 5', 'trống + timpani (A2/D3)', 'f', 'B\' ô 31 Eb: timpani cuộn'],
  ],
  adaptive: [
    ['L0', 'Dr, Tm, Bl', 'vào trận', 'đầu ô kế'],
    ['L1', 'Gt, Bs', 'Dante B · Vergil 1 · Trish 33%', 'đầu ô kế, fade 1 phách; bỏ sau 4s dưới ngưỡng'],
    ['L2', 'Pd, Hc', 'Dante A-S · Vergil 2 · Trish 66%', 'như L1'],
    ['L3', 'l3-*-v3.html', 'Dante SS-SSS · Vergil 3 · Trish Awake', 'như L1'],
    ['Outro', 'combat-frame-v2.html tune 2', 'hết địch', 'đầu ô kế'],
  ],
  notes: ['Mỗi stem đúng 32 ô = 51.2s ở 150 BPM, 48 kHz; render 2 vòng, lấy vòng 2.', 'Bộ nhớ: stem dài gấp đôi v2; nén Vorbis streaming trong Unity.', 'Bản 16 ô vẫn ở combat-frame-v2.html.'],
  abc: frame32Abc + '\n\n' + gothOutroAbc,
});

const ad3 = (who, gate, extra = []) => [
  ['L3 ' + who, 'giai điệu 32 ô, cùng hòa âm và độ dài với khung v3', gate, 'vào đầu ô kế, fade 1 phách; ra khi dưới ngưỡng 4s'],
  ...extra,
  ['Pad, Bass trên trang', 'tham chiếu L2/L1', '-', 'không xuất stem'],
];

page({
  file: 'l3-dante-v3.html', title: 'L3 Dante v3',
  meta: 'Dante · Style SS-SSS · ' + META3,
  concept: `<p>A: motif "quãng 5". B: hook guitar mới. A': câu cao trào. B': hook lên quãng cao hơn, ô 31 lên Eb6, ô 32 whammy về ô 1. Scream 4 lần / 51s: ô 8, 20, 32 (và ô 12 cũ đã dời).</p>`,
  harmony: harm32,
  orch: [['A, A\'', '1-8, 17-24', 'lead guitar + power chord', 'scream ô 8, 20', 'L2', 'L1', 'L0', 'mf / ff', ''], ['B, B\'', '9-16, 25-32', 'lead guitar (hook)', 'scream ô 32', 'L2', 'L1', 'L0', 'f', '']],
  adaptive: ad3('Dante', 'Style SS-SSS', [['Scream', 'Vx', 'SSS (đề xuất)', 'đầu ô kế']]),
  notes: ['Tầm lead A3-Eb6.', 'Bản 16 ô: l3-dante-v2.html.'],
  abc: tune32(1, 'L3 Dante v3 (32 ô)', [
    [['L3', 'treble', 'Lead guitar', 'Lead', '%%MIDI program 29'], L32(A1(DANTE2), DANTE_B.map((b, i) => i === 0 ? '!f!' + b : b), A2(DANTE2), DANTE_B2), true],
    [['Vx', 'treble', 'Scream', 'Scr.', '%%MIDI program 54'], SCREAM32, false]]),
});

page({
  file: 'l3-vergil-v3.html', title: 'L3 Vergil v3',
  meta: 'Vergil · Tĩnh tâm cấp 3 · ' + META3,
  concept: `<p>Một vòng là một cung cảm xúc: <b>A</b> trầm buồn (con sordino) -> <b>B</b> violin đi lên C5 -> E6, cello đối bè -> <b>A'</b> cao trào bấm kép ff -> <b>B'</b> lắng xuống, đi về quãng thấp, dim. tới p để vòng lặp quay lại A êm.</p>`,
  harmony: harm32,
  orch: [['A', '1-8', 'violin con sord.', '-', 'L2', 'L1', 'L0', 'p', ''], ['B', '9-16', 'violin đi lên', 'cello', 'L2', 'L1', 'L0', 'mp -> f', ''],
    ["A'", '17-24', 'violin 8va bấm kép', 'cello', 'L2', 'L1', 'L0', 'ff', ''], ["B'", '25-32', 'violin đi xuống', 'cello', 'L2', 'L1', 'L0', 'f -> p', '']],
  adaptive: ad3('Vergil', 'Tĩnh tâm cấp 3'),
  notes: ['Tầm violin C#4-G6, cello G2-Eb4.', 'Bản 16 ô: l3-vergil-v2.html.'],
  abc: tune32(1, 'L3 Vergil v3 (32 ô)', [
    [['L3', 'treble', 'Violin solo', 'Vln', '%%MIDI program 40'], L32(A1(VERGIL2), VERGIL_B.map((b, i) => i === 0 ? '!mp!' + b : b), A2(VERGIL2), VERGIL_B2), true],
    [['Vc', 'bass', 'Cello', 'Vc', '%%MIDI program 42'], L32(rest(8), CELLO_B.map((b, i) => i === 0 ? '!mp!' + b : b), A2(CELLO), CELLO_B2), false]]),
});

const PIANO32 = L32(A1(PIANO), PNO_B, A2(PIANO), PNO_B2);
page({
  file: 'l3-trish-v3.html', title: 'L3 Trish v3',
  meta: 'Trish: theme + Angel + Devil · ' + META3,
  concept: `<p>Cả 3 bản đều thành A - B - A' - B'. <b>Theme</b>: B là thánh ca mới, B' là bè cao (descant). <b>Angel</b>: B đi lên sáng, B' kết bằng melisma. <b>Devil</b>: B và B' vẫn chỉ đi xuống, B' kết bằng A3 "rơi". Piano: motif chùm ba ở A/A', hợp âm khối ở B/B'.</p>`,
  harmony: harm32,
  orch: [['Theme', '1-32', 'soprano', 'piano', 'hợp xướng', 'L1', 'L0', 'p -> mf', ''], ['Angel', '1-32', 'soprano cao', 'piano 8va + harp', 'hợp xướng nữ', 'L1', 'L0', 'mf -> ff', ''],
    ['Devil', '1-32', 'contralto', 'piano hợp âm', 'dàn dây trầm', 'L1', 'L0 (half-time đề xuất)', 'mp -> mf -> p', '']],
  adaptive: ad3('Trish', 'Awake (Angel / Devil); theme: Nộ đầy chờ Awake (đề xuất)'),
  notes: ['Bản 16 ô: l3-trish-v2.html.'],
  abc: [
    tune32(1, 'Theme Trish v3 (32 ô)', [[SOPv, L32(A1(SOP), TRISH_B.map((b, i) => i === 0 ? '!mf!' + b : b), A2(SOP), TRISH_B2), true], [PNOv, PIANO32, false],
      [CHv, L32(A1(choirBars('Ah... kiểu thánh ca')), CHOIR_B, A2(CHOIR), CHOIR_B2), false]], [refBass]),
    tune32(2, 'Trish Awake Angel v3 (32 ô)', [[SOPv, L32(A1(ANGEL2), ANGEL_B, A2(ANGEL2), ANGEL_B2), true], [PNOv, PIANO32.map((b, i) => i === 0 ? b.replace('(3!p!', '(3"^piano 8va + harp"!mp!') : b), false],
      [CHv, L32(A1(choirBars('hợp xướng nữ, sáng')), CHOIR_B, A2(CHOIR), CHOIR_B2), false]], [refBass]),
    tune32(3, 'Trish Awake Devil v3 (32 ô)', [[['L3', 'treble', 'Contralto', 'A', '%%MIDI program 53'], L32(A1(DEVIL2), DEVIL_B, A2(DEVIL2), DEVIL_B2), true],
      [PNOv, L32(A1(DEV_PNO), DEV_PNO_B, A2(DEV_PNO), PNO_B2), false],
      [['St', 'bass', 'Dàn dây trầm', 'Str', '%%MIDI program 48'], L32(A1(STR), STR_B, A2(STR), STR_B2), false]], [refBass]),
  ].join('\n\n'),
});
