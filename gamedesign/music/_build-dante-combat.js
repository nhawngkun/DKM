// Bài combat riêng của Dante (D-011). Helper trang chép từ _build-scores-v2.js.
const fs = require('fs');
const path = require('path');
const OUT = 'G:/UnityProject/DKM/gamedesign/music';
const TPL = path.join(process.env.USERPROFILE, '.claude/skills/music-lead/assets/score-template.html');
const tpl = fs.readFileSync(TPL, 'utf8');
const head = tpl.split('</head>')[0].replace(/<!--[\s\S]*?-->\n?/, '');

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


// ================= Dante combat: hard rock / metal, 172 BPM, E thứ + Dorian, 4/4 =================
const PERC_D = `%%percmap F bass-drum-1
%%percmap c acoustic-snare
%%percmap ^g closed-hi-hat x
%%percmap a crash-cymbal-1 x
%%percmap b ride-cymbal-1 x`;
const VOICES_D = [
  ['Ld', 'treble', 'L2 Lead guitar', 'Lead', '%%MIDI program 29'],
  ['Vx', 'treble', 'L3 Shout / scream', 'Vox', '%%MIDI program 54'],
  ['Pd', 'treble', 'L2 Dây + synth (chỉ chorus)', 'Pad', '%%MIDI program 48'],
  ['Gt', 'bass', 'L1 Rhythm guitar (E chuẩn)', 'Gtr', '%%MIDI program 30'],
  ['Bs', 'bass', 'L1 Bass', 'Bass', '%%MIDI program 34'],
  ['Dr', 'perc', 'L0 Trống', 'Dr', '%%MIDI channel 10'],
];
function hdrD(x, title) {
  return `X:${x}\nT:${title}\nC:music-lead (Dante combat v1)\nM:4/4\nL:1/8\nQ:1/4=172\n${PERC_D}\n%%score ${VOICES_D.map(v => v[0]).join(' ')}\n` +
    VOICES_D.map(([id, clef, name, snm, midi]) => `V:${id} clef=${clef} name="${name}" snm="${snm}"\n${midi}`).join('\n') + '\nK:Em\n';
}
const R = n => Array(n).fill('z8');
const putD = (bar, pre) => !pre ? bar : bar.startsWith('(3') ? '(3' + pre + bar.slice(2) : pre + bar;
// sections: {label, ch: [chords], dyn: {id|all}, v: {id: bars}, mask?: {id: [fromBar]}}
function abcD(x, title, sections, endRepeat) {
  const out = Object.fromEntries(VOICES_D.map(v => [v[0], []]));
  sections.forEach(s => {
    VOICES_D.forEach(([id]) => {
      const src = s.v[id];
      const from = (s.from && s.from[id]) || 0;
      s.ch.forEach((c, i) => {
        const on = src && i >= from;
        let bar = on ? src[i] : 'z8';
        let pre = '';
        if (id === 'Ld') pre += (i === 0 ? `"^${s.label}"` : '') + `"${c}"`;
        if (on && i === from) pre += `!${s.dyn[id] || s.dyn.all}!`;
        out[id].push(putD(bar, pre));
      });
    });
  });
  const n = out.Ld.length, lines = [];
  for (let s = 0; s < n; s += 4)
    VOICES_D.forEach(([id]) => lines.push(`[V:${id}] ${s === 0 && endRepeat ? '|: ' : ''}${out[id].slice(s, s + 4).join(' | ')} ${s + 4 >= n ? (endRepeat ? ':|' : '|]') : '|'}`));
  return hdrD(x, title) + lines.join('\n');
}

// ---------- Hợp âm ----------
const V_CH = ['Em', 'Em', 'C', 'D', 'Em', 'Em', 'G', 'A', 'Em', 'Em', 'C', 'D', 'C', 'D', 'B', 'B'];
const P_CH = ['C', 'D', 'Em', 'B'];
const C_CH = ['G', 'D', 'Em', 'C', 'G', 'D', 'A', 'A', 'G', 'D', 'Em', 'C', 'Am', 'C', 'B', 'B'];
const K_CH = ['Em', 'Em', 'F', 'Em', 'Em', 'Em', 'F', 'B'];
const O_CH = ['C', 'Em'];

// ---------- L1: rhythm guitar + bass ----------
const PW = { Em: '[E,,B,,]', C: '[C,G,]', D: '[D,A,]', G: '[G,,D,]', A: '[A,,E,]', Am: '[A,,E,]', B: '[B,,F,]', F: '[=F,,C,]' };
const PW8 = { Em: '[E,,B,,E,]', C: '[C,G,C]', D: '[D,A,D]', G: '[G,,D,G,]', A: '[A,,E,A,]', Am: '[A,,E,A,]', B: '[B,,F,B,]' };
const RT = { Em: 'E,,', C: 'C,', D: 'D,', G: 'G,,', A: 'A,,', Am: 'A,,', B: 'B,,', F: '=F,,' };
const BR = { Em: 'E,,,', C: 'C,,', D: 'D,,', G: 'G,,,', A: 'A,,,', Am: 'A,,,', B: 'B,,,', F: '=F,,,' };
// Riff hook của Dante trên Em: chug - chug - STAB - chug - nghỉ - chug - trượt lên
const RIFF_EM = ["E,,E,, !accent![E,,B,,]E,, z E,, !accent![G,,D,]2", "E,,E,, !accent![E,,B,,]E,, z E,, [A,,E,][B,,F,]"];
const riffBar = (c, i) => c === 'Em' ? RIFF_EM[i % 2] : `!accent!${PW[c]}2 ${PW[c]}${RT[c]} z ${RT[c]} ${PW[c]}${RT[c]}`;
const bassBar = c => c === 'Em' ? `${BR.Em}2 ${BR.Em}${BR.Em} z ${BR.Em} G,,,2` : `${BR[c]}2 ${BR[c]}${BR[c]} z ${BR[c]} ${BR[c]}${BR[c]}`;
const V_GT = V_CH.map((c, i) => riffBar(c, i)), V_BS = V_CH.map(bassBar);
const P_GT = P_CH.map((c, i) => i < 3 ? `"^tremolo"${PW8[c]}8` : `${PW8[c]}4 z4`);
const P_BS = P_CH.map((c, i) => i < 3 ? Array(8).fill(BR[c]).join('') : `${BR[c]}4 z4`);
const C_GT = C_CH.map(c => `!accent!${PW8[c]}2 ${PW8[c]}${PW8[c]} !accent!${PW8[c]}2 ${PW8[c]}${PW8[c]}`);
const C_BS = C_CH.map(c => Array(8).fill(BR[c]).join(''));
const K_GT = K_CH.map(c => `"^half-time"${RT[c]}2 ${RT[c]}${RT[c]} z2 ${PW[c]}2`.replace('"^half-time"', ''));
const K_BS = K_CH.map(c => `${BR[c]}2 ${BR[c]}${BR[c]} z2 ${BR[c]}2`);
const O_GT = ['[C,G,C]8', '!fermata![E,,B,,E,]8'], O_BS = ['C,,8', '!fermata!E,,,8'];

// ---------- L0: trống ----------
const DV = '[F^g] ^g [c^g] [F^g] ^g [F^g] [c^g] ^g';
const DV1 = '[Fa] ^g [c^g] [F^g] ^g [F^g] [c^g] ^g';
const FILL = 'c c c c c/c/c/c/ c/c/c/c/';
const V_DR = V_CH.map((c, i) => i === 0 || i === 8 ? DV1 : i === 7 || i === 15 ? FILL : DV);
const P_DR = ['c2 c2 c2 c2', 'c c c c c c c c', 'c/c/c/c/ c/c/c/c/ c/c/c/c/ c/c/c/c/', '"^dừng 2 phách"c/c/c/c/ c/c/c/c/ [Fa]2 z2'];
const DC = '[Fb] F [cFb] F [Fb] F [cFb] F', DC1 = '[Fa] F [cF] F [Fb] F [cFb] F';
const C_DR = C_CH.map((c, i) => i === 0 || i === 8 ? DC1 : i === 15 ? FILL : DC);
const DK = '[F^g]2 [F^g] ^g [c^g]2 ^g [F^g]';
const K_DR = K_CH.map((c, i) => i === 0 ? '[Fa]2 [F^g] ^g [c^g]2 ^g [F^g]' : i === 7 ? FILL : DK);
const O_DR = ['c c c c c/c/c/c/ c c', '!fermata![Fa]8'];

// ---------- L2: lead ----------
// Motif Dante (ô 1-2): vào lệch phách, nốt A "bend" nửa cung đè lên phách 3 rồi trượt xuống: cú nhếch mép
const V_LD = ['z e g "^bend"a- a2 g e', 'b2 a g e2 d e', 'z2 G E c2 B G', 'A2 d f a2 f d',
  'z e g "^bend"a- a2 g e', 'b2 a g e4', "z d g b d'2 b g", '"^Dorian C#"^c2 A E A2 ^c e',
  'z e g "^bend"a- a2 g e', 'b2 a g e2 d e', 'z2 G E c2 B G', 'a2 f d A2 d f',
  'g2 e c G2 c e', 'f2 d A F2 A d', '"^pinch harm."B4- B2 ^d f', 'b2 a g f2 ^d B'];
const P_LD = ['C E G c e c G E', 'D F A d f d A F', 'E G B e g e B G', '!crescendo(!B,2 ^D2 F2 B2!crescendo)!'];
const C_LD = ['d2 g2 b3 a', 'a2 f2 d4', 'e2 g2 b2 g2', "c'4 g2 e2",
  "d2 g2 b3 d'", "d'4 a2 f2", "^c'2 e'2 a4", '"^bend"a4- a2 e ^c',
  'd2 g2 b3 a', 'a2 f2 d4', 'e2 g2 b2 g2', "e'4 c'2 g2",
  "c'2 a2 e2 a2", "g2 c'2 e'4", "^d'4 b2 f2", 'b2 a g f2 ^d2'];
const K_LD = ['z8', 'z8', '"^pinch harm."=f4 z4', 'z8', 'z8', 'z8', '=f2 e2 =f4', '"^whammy dive"b8'];
const O_LD = ["c'2 g2 e2 c2", '!fermata!"^pinch harm."e8'];

// ---------- L2 pad chorus, L3 shout/scream ----------
const PAD_D = { G: '[GBd]8', D: '[DFA]8', Em: '[EGB]8', C: '[CEG]8', A: '[^CEA]8', Am: '[CEA]8', B: '[B,^DF]8' };
const C_PD = C_CH.map(c => PAD_D[c]);
const SH = (p, t = 'HEY!') => `z6 "^${t}"${p}2`;
const C_VX = ['z8', SH('A'), 'z8', SH('G'), 'z8', SH('A'), 'z8', 'z4 "^SCREAM (SSS)"A4',
  'z8', SH('A'), 'z8', SH('G'), 'z8', SH('G'), 'z8', 'z4 "^SCREAM: Yeah! (SSS)"B4'];
const K_VX = ['z4 "^gầm (SSS)"E4', 'z8', 'z8', 'z8', 'z8', 'z8', 'z8', 'z8'];

const SEC = {
  verse: { label: 'VERSE (rank D-A)', ch: V_CH, dyn: { all: 'f', Ld: 'mf' }, v: { Ld: V_LD, Gt: V_GT, Bs: V_BS, Dr: V_DR } },
  pre: { label: 'PRE-CHORUS (chạm S, 1 lần)', ch: P_CH, dyn: { all: 'f' }, v: { Ld: P_LD, Gt: P_GT, Bs: P_BS, Dr: P_DR } },
  chorus: { label: 'CHORUS (rank S-SSS)', ch: C_CH, dyn: { all: 'ff' }, v: { Ld: C_LD, Vx: C_VX, Pd: C_PD, Gt: C_GT, Bs: C_BS, Dr: C_DR } },
  brk: { label: 'BREAKDOWN (half-time)', ch: K_CH, dyn: { all: 'ff', Ld: 'f' }, v: { Ld: K_LD, Vx: K_VX, Gt: K_GT, Bs: K_BS, Dr: K_DR } },
  out: { label: 'OUTRO', ch: O_CH, dyn: { all: 'ff' }, v: { Ld: O_LD, Pd: ['[CEG]8', '!fermata![EGB]8'], Gt: O_GT, Bs: O_BS, Dr: O_DR } },
};
const demoVerse = { ...SEC.verse, label: 'VERSE: ô 1-4 chỉ trống (D-C), ô 5-8 + riff (B), ô 9 + lead (A)', from: { Gt: 4, Bs: 4, Ld: 8 } };
const danteCombat = [
  abcD(1, 'Dante combat: DEMO nghe thử (verse -> pre -> chorus -> breakdown -> outro, ≈ 64s)', [demoVerse, SEC.pre, SEC.chorus, SEC.brk, SEC.out]),
  abcD(2, 'Stem: VERSE 16 ô (lặp)', [SEC.verse], true),
  abcD(3, 'Stem: PRE-CHORUS 4 ô', [SEC.pre]),
  abcD(4, 'Stem: CHORUS 16 ô (lặp)', [SEC.chorus], true),
  abcD(5, 'Stem: BREAKDOWN 8 ô', [SEC.brk]),
  abcD(6, 'Stem: OUTRO 2 ô', [SEC.out]),
].join('\n\n');

page({
  file: 'dante-combat.html', title: 'Dante Combat',
  meta: 'Bài combat riêng của Dante (D-011) · hard rock / metal · 172 BPM · E thứ + Dorian · 4/4 · Draft v1',
  concept: `<p><b>Không còn khung chung.</b> Đây là bài riêng của Dante: nhanh hơn (172), giọng khác (E thứ, dây E buông của guitar), groove khác (riff chug lệch phách), form khác (verse / pre / chorus / breakdown).</p>
  <p><b>Động cơ nhịp</b> là riff hook trên Em: <i>chug-chug-STAB-chug-nghỉ-chug-trượt lên G5</i>. Cú STAB rơi vào phách 2 nửa sau và chỗ nghỉ ở phách 3: đây là chỗ "nhếch mép". Kick trống khóa vào riff. <b>Motif lead</b> (verse ô 1-2): vào lệch phách, nốt A bend đè lên phách 3 rồi trượt xuống. Ô 8 dùng A trưởng (C#, Dorian) cho màu rock tự tin, không u ám.</p>
  <p><b>Chorus là phần thưởng</b> (như DMC5 chỉ mở chorus ở rank S): đổi sang G-D-Em-C, sáng và cao hơn verse (lead lên E6), double kick, power chord đầy, dây + synth phồng lên, "HEY!" kiểu gang shout ở SS, scream ở SSS. <b>Breakdown</b> half-time trên Em với F (b2, Phrygian): nặng, khi rank tụt khỏi S hoặc làm biến tấu sau 2 vòng chorus.</p>`,
  harmony: [
    ['Verse', '16', 'E thứ + Dorian', 'Em Em C D | Em Em G A | Em Em C D | C D B B (i i bVI bVII | i i bIII IV | ... | bVI bVII V V)', 'pedal E; A trưởng = Dorian; B trưởng (D#) kéo về Em'],
    ['Pre', '4', 'E thứ', 'C D Em B', 'tremolo đi lên, dừng 2 phách trước chorus'],
    ['Chorus', '16', 'G trưởng / E thứ', 'G D Em C | G D A A | G D Em C | Am C B B', 'bIII-bVII-i-bVI: rock anthem sáng; A trưởng ở ô 7-8 nâng lên'],
    ['Breakdown', '8', 'E Phrygian', 'Em Em F Em | Em Em F B', 'b2 (F) nghiến, half-time'],
    ['Outro', '2', 'E', 'C -> Em', 'bVI -> i'],
  ],
  orch: [
    ['Verse', '16', 'lead guitar (motif, gọi-đáp với riff)', '-', '-', 'bass khóa riff', 'rock beat, kick theo riff', 'f', 'nghỉ trong lead là cố ý: chừa chỗ cho riff và tiếng chém'],
    ['Pre', '4', 'lead rải hợp âm đi lên', '-', '-', 'bass 8th', 'snare dồn 4 -> 8 -> 16, dừng 2 phách', 'f -> ff', 'tremolo guitar'],
    ['Chorus', '16', 'lead (giai điệu anthem); SS: lead 2 nhân đôi 8vb', 'HEY! (SS), scream (SSS)', 'dây + synth sawtooth', 'bass 8th đều', 'double kick 8th + ride, crash ô 1, 9', 'ff', 'power chord đầy (gốc + 5 + 8)'],
    ['Breakdown', '8', 'pinch harmonic, whammy dive', 'gầm (SSS)', '-', 'bass theo chug', 'half-time: kick 1, snare 3', 'ff', 'drop thật nặng, palm mute'],
  ],
  adaptive: [
    ['L0 trống', 'Dr', 'vào trận (rank D-C)', 'đầu ô kế; verse chạy ngầm từ ô 1'],
    ['L1 riff + bass', 'Gt, Bs', 'rank B', 'vào đầu ô kế, fade 1 phách'],
    ['L2 lead (+ pad ở chorus)', 'Ld, Pd', 'rank A', 'như L1'],
    ['Section: verse -> pre -> chorus', 'tune 3 rồi 4', 'chạm rank S', 'chờ hết câu 4 ô của verse, chơi pre 4 ô, vào chorus ô 1. Chorus lặp khi còn >= S'],
    ['L3 shout', 'Vx (HEY!)', 'rank SS', 'đầu ô kế'],
    ['L3 scream + lead 2', 'Vx (scream), lead 8vb', 'rank SSS', 'đầu ô kế; stinger SSS'],
    ['Section: chorus -> breakdown -> verse', 'tune 5 rồi 2', 'tụt dưới S liên tục 4s; hoặc sau 2 vòng chorus (biến tấu)', 'vào breakdown ở đầu câu 4 ô kế; xong 8 ô về verse (hoặc chorus nếu vẫn >= S)'],
    ['Outro', 'tune 6', 'hết địch', 'đầu ô kế'],
  ],
  notes: [
    'Độ dài: verse 16 ô = 22.3s, chorus 16 ô = 22.3s, pre 4 ô = 5.6s, breakdown 8 ô = 11.2s ở 172 BPM.',
    'Stem: mỗi section xuất riêng 5 stem (Dr, Gt+Bs, Ld, Pd, Vx). Section đổi ở ranh 4 ô (FMOD transition marker hoặc PlayScheduled).',
    'Tầm: lead A3-E6; rhythm guitar E2 trở lên (chuẩn E, không drop); bass E1.',
    'Không trích riff hay giai điệu DMC; chỉ lấy cấu trúc (chorus mở ở rank S) và màu (rock swagger + vocal gầm).',
    'Synth trình duyệt chỉ để nghe nốt và nhịp; guitar méo GM nghe khác xa bản thu thật.',
  ],
  abc: danteCombat,
});
