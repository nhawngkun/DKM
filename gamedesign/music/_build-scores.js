// Dựng các trang score trong gamedesign/music từ template của skill music-lead.
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

// ---------- L3 ----------
const DANTE = ['!mf!D A d3 e f2', 'e d A2 d4', 'f2 d f b2 a f', 'g _e g "^bend"b-b4',
  'a2 f d A d f a', 'b2 a g d\'2 b g', 'd\'2 c\' b f2 d f', 'e2 ^c e "^pinch harm."a4',
  '!f!D A d3 e f2', 'a/g/f/e/ d2 a2 d\'2', 'b2 a f d f b d\'', 'g2 b g _e g b2',
  'd\'2 b g d\'3 b', 'c\'2 d\' c\' b f d2', 'b2 g _e B _e g b', 'e2 ^c A E A ^c e'];
const VERGIL = ['!mp!"^muraiki"A6 "^meri"B A', 'D4 "^lặng"z4', 'z2 F2 d4', '_e6 d2',
  'A6 B A', 'G4 z4', 'z2 B2 d2 f2', 'e8',
  '!mf!a6 b a', 'd4 "^lặng"z4', 'z2 f2 b4', 'g4 f2 _e2',
  'd8', 'z4 f2 d2', '_e4 G4', 'E4 A4'];
const TRISH = ['(3!mp!d_ed (3AGA d4', '(3fed (3AFD A4', '(3dfb (3fdB d4', '(3_egb (3g_eB G4',
  '(3d_ed (3AGA d4', "(3gbd' (3bgd B4", "(3fdB (3dfb d'4", "(3^c'ba (3e^cA E4",
  '(3!mf!d_ed (3AGA d2 f2', '(3agf (3edA d4', '(3dfb (3fdB F4', '(3GB_e (3g_eB G4',
  '(3Bdg (3dBG D4', '(3FBd (3fdB F4', '(3_EGB (3_egb g4', '(3^cBA (3E^CE A4'];
const DEVIL = ['!f!!accent!D _E D A, D2 !accent!_E2', 'F E D A, D,4', 'B, D F D B,2 F2', '_E G B G _E4',
  '!accent!D _E D A, D2 !accent!_E2', 'G B d B G2 D2', 'F D B, D F2 B2', 'A ^G A B ^c2 A,2',
  '!ff!D,D, D _E D,D, A, D', 'D,D, F E D,D, A,2', 'B,B, D F B,B, d2', '_E_E G B _E_E g2',
  'G,G, B, D G,G, d2', 'B,B, D F B,B, f2', '_E_E G B _e2 d2', 'A,A, ^C E A,A, ^C2'];
const ANGEL = ['!p!d2 _e2 d4', 'A2 G2 A4', 'B2 d2 f4', 'G2 B2 _e4',
  'd2 _e2 d4', 'B2 d2 g4', 'f2 d2 B4', 'A4 ^c4',
  '!mf!f2 g2 a4', 'f2 e2 d4', 'd2 f2 a4', 'g4 _e4',
  '!f!g2 a2 b4', 'a2 f2 d4', '_e2 f2 g4', '^c4 e4'];
const ANGEL_ALTO = ['A8', 'F8', 'F8', '_E8', 'A8', 'G8', 'D8', 'E8', 'd8', 'A8', 'F8', 'B8', 'd8', 'F8', 'B8', 'A8'];

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
  return `X:${x}\nT:${title}\nC:music-lead v1\nM:4/4\nL:1/8\nQ:1/4=150\n${PERC}\n%%score ${voices.map(v => v[0]).join(' ')}\n` +
    voices.map(([id, clef, name, snm, midi]) => `V:${id} clef=${clef} name="${name}" snm="${snm}"\n${midi}`).join('\n') + '\nK:Dm\n';
}
function body(voices) {
  const out = [];
  for (let s = 0; s < 16; s += 4)
    voices.forEach(([id, bars]) => out.push(`[V:${id}] ${s === 0 ? '|: ' : ''}${bars.slice(s, s + 4).join(' | ')} ${s === 12 ? ':|' : '|'}`));
  return out.join('\n');
}

// ---------- Tune: khung ----------
const frameVoices = [
  ['Ko', 'treble', 'L2 Koto', 'Koto', '%%MIDI program 107'],
  ['Pd', 'treble', 'L2 Pad: dây + hợp xướng', 'Pad', '%%MIDI program 52'],
  ['Gt', 'bass', 'L1 Guitar riff drop-D (+ shamisen)', 'Gtr', '%%MIDI program 30'],
  ['Bs', 'bass', 'L1 Bass', 'Bass', '%%MIDI program 34'],
  ['Tk', 'perc', 'L0 Taiko', 'Taiko', '%%MIDI channel 10'],
  ['Dr', 'perc', 'L0 Trống metal', 'Dr', '%%MIDI channel 10'],
];
const frameBars = [
  ['Ko', withChords(fromKind(KOTO)).map((b, i) => i === 0 ? b.replace('z2', '!mp!z2') : b)],
  ['Pd', PAD.map((b, i) => i === 0 ? '!mp!' + b : b)],
  ['Gt', fromKind(RIFF).map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Bs', fromKind(BASS).map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Tk', taiko.map((b, i) => i === 0 ? '!f!' + b : b)],
  ['Dr', drums.map((b, i) => i === 0 ? '!f!' + b : b)],
];
const frameAbc = hdr(1, 'Combat: khung L0-L2 (vòng 16 ô = 25.6s)', frameVoices) + body(frameBars);
const outroAbc = hdr(2, 'Combat: outro (2 ô, sau ô cuối đang chạy)', frameVoices) + [
  '[V:Ko] "^Outro""Eb"z2 _e g b4 | "Dm"!fermata!d8 |]',
  '[V:Pd] !f![_EGB]8 | !fermata![DFA]8 |]',
  '[V:Gt] !ff![_E,,B,,_E,]8 | !fermata![D,,A,,D,]8 |]',
  '[V:Bs] !ff!_E,,,8 | !fermata!D,,,8 |]',
  '[V:Tk] !ff!C C C C C2 C2 | !fermata!C8 |]',
  '[V:Dr] !ff!c c c c c/c/c/c/ c c | !fermata![Fa]8 |]',
].join('\n');

// Tham chiếu L2 + L1 dưới mỗi giai điệu L3 (để nghe độ khớp)
const refVoices = [
  ['Pd', 'treble', 'L2 Pad (tham chiếu)', 'Pad', '%%MIDI program 52'],
  ['Bs', 'bass', 'L1 Bass (tham chiếu)', 'Bass', '%%MIDI program 34'],
];
const refBars = [['Pd', PAD.map((b, i) => i === 0 ? '!p!' + b : b)], ['Bs', fromKind(BASS).map((b, i) => i === 0 ? '!mp!' + b : b)]];
function l3(x, title, mel, extra = []) {
  const voices = [mel.v, ...extra.map(e => e.v), ...refVoices];
  return hdr(x, title, voices) + body([[mel.v[0], withChords(mel.bars)], ...extra.map(e => [e.v[0], e.bars]), ...refBars]);
}
const danteAbc = l3(1, 'L3 Dante: "Cú nhảy quãng 5"', { v: ['L3', 'treble', 'L3 Lead guitar', 'Lead', '%%MIDI program 29'], bars: DANTE });
const vergilAbc = l3(1, 'L3 Vergil: "Tiếng thở dài"', { v: ['L3', 'treble', 'L3 Shakuhachi', 'Shaku.', '%%MIDI program 77'], bars: VERGIL });
const trishAbc = l3(1, 'Theme Trish: "Vòng xoáy" (nguyên mẫu)', { v: ['L3', 'treble', 'Koto', 'Koto', '%%MIDI program 107'], bars: TRISH });
const devilAbc = l3(2, 'L3 Trish Awake Devil', { v: ['L3', 'treble', 'L3 Guitar 8 dây + synth bass', 'Devil', '%%MIDI program 30'], bars: DEVIL });
const angelAbc = l3(3, 'L3 Trish Awake Angel', { v: ['L3', 'treble', 'L3 Hợp xướng nữ: Soprano', 'S', '%%MIDI program 52'], bars: ANGEL },
  [{ v: ['Al', 'treble', 'L3 Hợp xướng nữ: Alto', 'A', '%%MIDI program 52'], bars: ANGEL_ALTO.map((b, i) => i === 0 ? '!p!' + b : b) }]);

// ---------- Stingers ----------
const st = (x, t, voices, lines) => `X:${x}\nT:${t}\nC:music-lead v1\nM:4/4\nL:1/8\nQ:1/4=150\n${PERC}\n` +
  voices.map(([id, clef, name, midi]) => `V:${id} clef=${clef} name="${name}"\n${midi}`).join('\n') + '\nK:Dm\n' + lines.join('\n');
const stingAbc = [
  st(1, 'Stinger: Dante lên SSS (1 ô, bản trên ô Dm)', [['Ld', 'treble', 'Lead guitar', '%%MIDI program 29'], ['Tk', 'perc', 'Taiko', '%%MIDI channel 10'], ['Dr', 'perc', 'Trống', '%%MIDI channel 10']],
    ['[V:Ld] "^SSS: dịch theo gốc hợp âm của ô""Dm"!ff!D A d a "^pinch harm. + whammy"d\'4 |]', '[V:Tk] !ff!{C}C2 z2 z4 |]', '[V:Dr] !ff![Fa]2 z2 z4 |]']),
  st(2, 'Stinger: Vergil deflect (tức thì, không quantize)', [['Rn', 'treble', 'Chuông rin D5 + thép cạo', '%%MIDI program 14']],
    ['[V:Rn] "^bus nhạc -12dB trong 0.3s"!accent!d8 |]']),
  st(3, 'Stinger: Vergil Iai cấp 3 (ví dụ vào lại ở phách 3)', [['Gt', 'bass', 'Guitar + bass (mọi stem)', '%%MIDI program 30'], ['Dr', 'perc', 'Trống + ō-daiko', '%%MIDI channel 10']],
    ['[V:Gt] "^mọi stem câm 0.5s ≈ 1.25 phách"z4 "^chém (SFX)"z4 | z4 "^vào lại: phách mạnh kế"!ff![D,,A,,D,]4 |]', '[V:Dr] z8 | z4 !ff![Fa]4 |]']),
  st(4, 'Stinger: Trish Awake Devil (ô trước khi L3 Devil vào)', [['Gt', 'bass', 'Guitar 8 dây', '%%MIDI program 30']],
    ['[V:Gt] "^whammy dive từ D xuống + reverse cymbal"!ff!D,,8 |]']),
  st(5, 'Stinger: Trish Awake Angel (ô trước khi L3 Angel vào)', [['Ch', 'treble', 'Hợp xướng nữ', '%%MIDI program 52'], ['Ce', 'treble', 'Celesta', '%%MIDI program 8']],
    ['[V:Ch] !p!"^cresc. tới f"[DAd]8 |]', '[V:Ce] d a d\' a d\'4 |]']),
].join('\n\n');

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
  <p class="meta">Ký âm ở cao độ thực (concert pitch). Khi chép part: guitar và bass viết cao 1 quãng 8. Ký hiệu trống: F kick, c snare, g× hi-hat, a× crash; taiko: C ō-daiko, e shime-daiko. Nút play phát bằng synth GM, chỉ để nghe nốt và độ khớp, không phải âm sắc thật.</p>
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
${p.abc}
</script>

${SCRIPT}
</body>
</html>
`;
  fs.writeFileSync(path.join(OUT, p.file), html);
  console.log('wrote', p.file);
}

const META = 'Dựa trên: music-direction.md, D-007 · 150 BPM · D thứ + In · vòng 16 ô = 25.6s · Draft v1';

page({
  file: 'combat-frame.html', title: 'Combat Frame L0-L2',
  meta: 'Khung combat chung cho Dante, Vergil, Trish · ' + META,
  concept: `<p>Bài combat chung, chia 3 stem chạy đồng bộ. <b>L0</b> chỉ có trống metal và taiko: đủ nghe là đang đánh nhau nhưng còn trống chỗ. <b>L1</b> thêm riff drop-D chug trên thang In (nốt Eb nghiến ở phách 4 mỗi ô Dm) với bass khóa vào kick. <b>L2</b> thêm pad dây + hợp xướng giữ hòa âm, và koto gảy ở phách lệch (phách 2 và 4 nửa sau) để không đè guitar.</p>
  <p>Vòng 16 ô có 2 câu A / A'. Câu A' đổi 4 ô cuối thành iv - bVI - bII - V để cú bII -> V ở ô 15-16 kéo về ô 1 rất mạnh. Ô 8 và 16 có fill snare báo hiệu vòng lặp. Ô 1 và 9 có crash. Outro (tune 2) là bII -> i ngân dài.</p>`,
  harmony: harmonyStd.concat([['Outro', '2 ô', 'D', 'Eb -> Dm (bII -> i)', 'kết Phrygian, ngân dài']]),
  orch: [
    ['A', '1-8', '- (L3 nếu bật)', 'koto ostinato (L2)', 'dây cello+viola + hợp xướng nam "oh" + synth pad tối (L2)', 'bass pick + riff guitar drop-D double-track; shamisen gảy bachi nhân đôi riff 2 quãng 8 trên ở phách 1 và nốt Eb (L1)', 'trống metal + ō-daiko phách 1, 3; shime nói chuyện ở phách 2, 4 (L0)', 'f (L0-L1), mp (L2)', 'palm mute ở chug, mở tiếng ở nốt cuối ô'],
    ["A'", '9-16', '- (L3 nếu bật)', 'koto', 'pad lên quãng cao hơn (F-A-d ...), thêm hợp xướng nữ "ah"', 'như A', 'ô 12, 15 (Eb): taiko dồn; ô 16 fill snare 16th', 'f', 'ô 15-16 có thể mở tiếng hoàn toàn'],
    ['Outro', '2 ô', '-', 'koto Eb-G-Bb -> D', 'pad Eb -> Dm, fermata', 'power chord Eb5 -> D5 ngân', 'taiko dồn rồi 1 nhát + crash', 'ff -> ngân', 'đuôi reverb đền'],
  ],
  adaptive: [
    ['L0 trống + taiko', 'Dr, Tk', 'vào trận (mọi nhân vật)', 'đầu ô nhịp kế, không fade, vào bằng crash'],
    ['L1 riff + bass + shamisen', 'Gt, Bs', 'Dante rank B · Vergil cấp 1 · Trish Nộ >= 33%', 'vào đầu ô kế, fade 1 phách; ra khi dưới ngưỡng 4s, ở đầu ô kế'],
    ['L2 pad + koto', 'Pd, Ko', 'Dante A-S · Vergil cấp 2 · Trish >= 66%', 'như L1'],
    ['L3', 'xem trang l3-*.html', 'Dante SS-SSS · Vergil cấp 3 · Trish Awake', 'như L1; đổi nhân vật = crossfade ở đầu ô kế'],
    ['Outro', 'tune 2', 'hết địch', 'chờ đầu ô kế, phát outro, mọi stem fade 1 phách'],
  ],
  notes: [
    'Stem: L0, L1, L2 mỗi stem đúng 16 ô = 25.6s, 48 kHz. Render 2 vòng, lấy vòng 2 để đuôi reverb nối liền.',
    'L0 phải nghe tự đủ khi đứng một mình (vài nhịp trống như DMC5 ở rank thấp).',
    'Mix: chừa 2-5 kHz cho tiếng kiếm; snare không nhọn; sidechain -3 dB từ đòn nặng.',
    'Shamisen: gõ bachi vào da (sawari, tiếng rè) ở nốt Eb; ABC không ghi được, phối khí làm theo ghi chú.',
    'Tùy chọn biến tấu khi thu: ô 12 cho bass giữ pedal D dưới hợp âm Eb (Eb/D) để căng hơn; hợp âm L3 không đổi.',
  ],
  abc: frameAbc + '\n\n' + outroAbc,
});

const l3Adaptive = (who, gate) => [
  ['L3 ' + who, 'giai điệu 16 ô, cùng vòng hòa âm và độ dài với L2', gate, 'vào đầu ô kế, fade 1 phách; ra khi dưới ngưỡng 4s; đổi nhân vật = crossfade 1 phách'],
  ['Pad, Bass trên trang này', 'chỉ là tham chiếu L2/L1', '-', 'không xuất stem từ đây'],
];

page({
  file: 'l3-dante.html', title: 'L3 Dante',
  meta: 'Leitmotif Dante · Style SS-SSS · ' + META,
  concept: `<p>Motif <b>"Cú nhảy quãng 5"</b> (ô 1-2): D-A-d là hình bấm power chord của guitar, bật lên lệch phách rồi lượn xuống f-e-d như một cái nhếch mép. Ngạo nghễ, tự tin, trình diễn (D-002). Câu A trình bày motif và đi lên d' ở ô 6-7; câu A' gọi lại motif, rồi chạy 16th ở ô 10, vươn lên d' nhiều lần (ô 13-14) và nhảy rải hợp âm A về lại D.</p>
  <p>Lead guitar có bend, pinch harmonic, whammy: kỹ thuật "khoe" hợp với kẻ trình diễn. Stinger SSS dùng chính đầu motif này.</p>`,
  harmony: harmonyStd.concat([['Ghi chú', '2, 4, 14', '', 'e trên Dm (ô 2), a trên Bb (ô 3 phách 4), c\' trên Bb (ô 14)', 'appoggiatura/nốt lướt, đều giải về nốt hợp âm trong nửa ô']]),
  orch: [
    ['A', '1-8', 'lead guitar (drive vừa, delay 1/8 chấm)', '-', 'L2', 'L1', 'L0', 'mf', 'bend ở ô 4, pinch harmonic ô 8'],
    ["A'", '9-16', 'lead guitar + synth lead sawtooth nhân đôi quãng 8 dưới', '-', 'L2', 'L1', 'L0', 'f', 'whammy dive nhẹ cuối ô 16 dẫn về ô 1'],
  ],
  adaptive: l3Adaptive('Dante', 'Style rank SS-SSS'),
  notes: [
    'Tầm: D4-D6 (cao độ thực), trong tầm guitar 24 phím (E2-E6).',
    'Không lời (người dùng chốt). Đỉnh bài là guitar lead.',
    'Stinger SSS: xem stingers.html, cùng motif.',
    'Tham khảo màu: lead guitar DMC5; không trích riff/giai điệu nào.',
  ],
  abc: danteAbc,
});

page({
  file: 'l3-vergil.html', title: 'L3 Vergil',
  meta: 'Leitmotif Vergil · Tĩnh tâm cấp 3 · ' + META,
  concept: `<p>Motif <b>"Tiếng thở dài"</b> (ô 1-2): một nốt A giữ gần trọn ô, nhích nửa cung lên Bb (kỹ thuật meri của shakuhachi) rồi về A, rơi xuống D và <b>im lặng nửa ô</b>. Khoảng lặng là một phần của motif: giữa nền metal dày, Vergil là người duy nhất chừa chỗ trống. Căng, kiểm soát, tiết chế (D-003).</p>
  <p>Ít nốt, nốt dài, nhiều nghỉ. Câu A' nhắc motif ở quãng 8 trên, rồi thu dần về D4. Hợp với stinger deflect (chuông rin D5) và Iai (lặng 0.5s): cả hệ của Vergil nói bằng khoảng lặng.</p>`,
  harmony: harmonyStd.concat([['Ghi chú', '1, 4, 12', '', 'Bb trên Dm (nốt thêu), d trên Eb (maj7), f trên Eb (9)', 'màu thang In, đều có chủ ý']]),
  orch: [
    ['A', '1-8', 'shakuhachi 1.8 (muraiki ở nốt đầu, meri cho Bb)', 'kim loại cọ vĩ (bowed cymbal / rin) ngân dưới ô 2, 6', 'L2', 'L1', 'L0', 'mp', 'reverb đền dài hơn các tầng khác'],
    ["A'", '9-16', 'shakuhachi 8va + dàn dây nhân đôi 1 quãng 8 dưới', '-', 'L2', 'L1', 'L0', 'mf', 'không tăng mật độ; chỉ lên quãng'],
  ],
  adaptive: l3Adaptive('Vergil', 'Tĩnh tâm cấp 3').concat([['Deflect / Iai', 'stinger', 'deflect thành công / Iai cấp 3', 'xem stingers.html']]),
  notes: [
    'Tầm: D4-Bb5, trong tầm shakuhachi 1.8 (D4 là nốt thấp nhất).',
    'Mix: shakuhachi dễ chìm dưới metal; nhân đôi dây 8vb và nâng 1-2 dB khi L3 Vergil bật.',
    'Các ô lặng (2, 6, 10) không lấp bằng nhạc cụ khác.',
  ],
  abc: vergilAbc,
});

page({
  file: 'l3-trish.html', title: 'L3 Trish',
  meta: 'Leitmotif Trish + 2 biến thể Awake · ' + META,
  concept: `<p>Motif <b>"Vòng xoáy"</b> (ô 1-2): chùm ba (triplet) quấn quanh D với Eb của thang In, trên nền 4/4 thẳng: như phép đang cuộn lại. Áp đảo, bí ẩn (D-004). Tune 1 là nguyên mẫu (koto), dùng cho chọn nhân vật và làm nguồn cho 2 biến thể.</p>
  <p><b>Devil</b> (tune 2): motif hạ 1 quãng 8, bỏ chùm ba thành móc đơn thẳng, chug guitar 8 dây, Eb nhấn ở phách 4 (nghiến b2 có chủ ý); câu A' thành chug pedal D. Nặng, méo, hủy diệt.<br>
  <b>Angel</b> (tune 3): motif giãn trường độ (augmentation), hát bởi hợp xướng nữ SSA "ah" ở quãng cao, có maj7 trên Bb ở ô 11 và đỉnh Bb5 ở ô 13. Sáng, treo lơ lửng, khống chế.</p>`,
  harmony: harmonyStd.concat([
    ['Devil', '1, 5', '', 'Eb trên Dm phách 4', 'b2 nghiến có chủ ý (màu quỷ)'],
    ['Angel', '11, 14', '', 'a trên Bb (maj7), a đầu ô 14 giải xuống f', 'màu sáng; appoggiatura'],
    ['Cả 3', '8, 16', '', 'Bb trên A', 'b9, màu Phrygian dominant'],
  ]),
  orch: [
    ['Nguyên mẫu', '1-16', 'koto (gảy, có oshide bend ở Eb)', 'giọng nữ thì thầm (âm tiết, không lời)', 'L2', 'L1', 'L0', 'mp -> mf', 'chỉ dùng ngoài combat (chọn nhân vật), xem câu hỏi mở'],
    ['Devil', '1-16', 'guitar 8 dây méo, power chord', 'synth bass gầm nhân đôi 2 quãng 8 dưới', 'L2', 'L1', 'L0', 'f -> ff', 'distortion nặng, sub; ô 9-16 chug pedal D'],
    ['Angel', '1-16', 'hợp xướng nữ: soprano', 'alto giữ nốt hợp âm; celesta + harp rải nốt giai điệu', 'L2', 'L1', 'L0', 'p -> f', 'reverb sáng, dài'],
  ],
  adaptive: [
    ['L3 Devil', 'tune 2', 'Trish Awake, chọn Devil', 'đầu ô kế sau khi kích hoạt (stinger Devil ở ô trước); tắt Awake: bỏ ở đầu ô kế'],
    ['L3 Angel', 'tune 3', 'Trish Awake, chọn Angel', 'như Devil'],
    ['Theme nguyên mẫu', 'tune 1', 'chọn nhân vật (đề xuất thêm: Nộ đầy chờ Awake, chưa chốt)', '-'],
  ],
  notes: [
    'Tầm: nguyên mẫu D4-D6 (koto). Devil A3-G5 (guitar). Angel soprano G4-Bb5, alto D4-D5.',
    'Devil và Angel dùng cùng vòng hòa âm và độ dài với L2 nên đổi qua lại được ở đầu ô.',
    'Câu hỏi mở cho design lead: theme nguyên mẫu có phát trong combat khi Nộ đầy 100% (chờ kích hoạt) không? D-007 hiện chỉ có L3 khi Awake.',
  ],
  abc: [trishAbc, devilAbc, angelAbc].join('\n\n'),
});

page({
  file: 'stingers.html', title: 'Combat Stingers',
  meta: 'Stinger theo D-007 · ' + META,
  concept: `<p>Năm cue ngắn đè lên khung combat. Stinger SSS và Awake rơi vào đầu ô nhịp; deflect phát tức thì; Iai cắt nhạc rồi cho vào lại trên phách mạnh. Tất cả viết trong D để hợp vòng hòa âm.</p>`,
  harmony: [
    ['SSS', '1 ô', 'theo ô', 'D-A-d-a-d\' (power chord 5-8)', 'dịch theo gốc hợp âm: Dm=D, Bb=Bb, Eb=Eb, Gm=G, A=A; cần 5 bản thu'],
    ['Deflect', 'tức thì', 'D', 'rin D5', 'D là nốt hợp âm của Dm, Bb, Gm; maj7 trên Eb, sus4 trên A: đều nghe được'],
    ['Iai', '2 ô', 'D', 'D5 power chord', 'vào lại bằng tonic'],
    ['Awake', '1 ô', 'D', 'D pedal / D-A-d', 'trung tính, ô kế là L3 biến thể'],
  ],
  orch: [
    ['SSS', '1', 'lead guitar', '-', '-', '-', 'crash + ō-daiko flam', 'ff', 'tiếng trượt pick trước phách 1'],
    ['Deflect', '-', 'chuông rin D5', 'thép cạo ngắn (sound design)', '-', '-', '-', 'sfz', 'bus nhạc -12 dB 0.3s: dip 30 ms, giữ, hồi 80 ms'],
    ['Iai', '2', '-', '-', 'mọi stem câm 0.5s', 'D5 power chord vào lại', 'crash + ō-daiko', 'ff', 'stem vẫn chạy ngầm để giữ đồng bộ'],
    ['Devil', '1', 'guitar 8 dây whammy dive', '-', '-', '-', 'reverse cymbal', 'ff', 'méo nặng'],
    ['Angel', '1', 'hợp xướng nữ D-A-d', 'celesta', '-', '-', '-', 'p -> f', 'reverb sáng'],
  ],
  adaptive: [
    ['Dante SSS', 'tune 1', 'rank đạt SSS', 'đầu ô kế, chọn bản theo hợp âm của ô đó'],
    ['Vergil deflect', 'tune 2', 'deflect thành công', 'ngay lập tức, không quantize'],
    ['Vergil Iai cấp 3', 'tune 3', 'Iai ở Tĩnh tâm cấp 3', 'câm ngay, vào lại ở phách 1 hoặc 3 kế sau 0.5s'],
    ['Trish Awake Devil / Angel', 'tune 4 / 5', 'kích hoạt Awake', 'đầu ô kế; ô sau đó L3 biến thể vào'],
  ],
  notes: [
    'Deflect và Iai phụ thuộc hệ phát có duck/mute theo thời gian thực (dev: PlayScheduled + AudioMixer snapshot, hoặc FMOD).',
    'Deflect nối tiếp nhanh (parry chuỗi): mỗi lần đổi tiếng rin luân phiên D5 / A5 để không lặp nhàm.',
  ],
  abc: stingAbc,
});
