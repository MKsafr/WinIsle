(() => {
  'use strict';

  const LOG = (...a) => console.log('[Vors lirik]', ...a);

  // Nama track yang jelas bukan lirik (komentar, terjemahan, deskripsi audio, dsb.)
  const BAD_NAME = /commentar|comment|解説|コメンタリ|コメント|translat|訳|번역|해설|traduc|audio.?desc|description|making|behind/i;

  let lastId = null;
  let last = null; // { v, body }

  const vid = () => new URLSearchParams(location.search).get('v');

  // Kelompok aksara satu baris: 'ko' (hangul), 'cjk' (kana/kanji), 'la' (latin), '' (lainnya)
  const G = (s) =>
    /[\uac00-\ud7af\u1100-\u11ff]/.test(s) ? 'ko' :
    /[\u3040-\u30ff\u4e00-\u9fff]/.test(s) ? 'cjk' :
    /[A-Za-z]/.test(s) ? 'la' : '';
  const langGroup = (code) => (/^(ja|zh)/i.test(code || '') ? 'cjk' : /^ko/i.test(code || '') ? 'ko' : 'la');
  const wlen = (s) => { let n = 0; for (const ch of s) n += ch.charCodeAt(0) > 0x2e80 ? 2 : 1; return n; };
  const median = (a) => { const b = [...a].sort((x, y) => x - y); return b.length ? b[Math.floor(b.length / 2)] : 0; };
  const nameOf = (t) => (t.name && (t.name.simpleText || (t.name.runs || []).map((x) => x.text).join(''))) || '';

  // Ambil objek JSON ytInitialPlayerResponse dari HTML halaman watch
  function extractPlayerResponse(html) {
    const m = /ytInitialPlayerResponse\s*=\s*\{/.exec(html);
    if (!m) return null;
    const i = m.index + m[0].length - 1;
    let depth = 0, inStr = false, esc = false;
    for (let k = i; k < html.length; k++) {
      const c = html[k];
      if (inStr) { if (esc) esc = false; else if (c === '\\') esc = true; else if (c === '"') inStr = false; continue; }
      if (c === '"') inStr = true;
      else if (c === '{') depth++;
      else if (c === '}') {
        depth--;
        if (depth === 0) { try { return JSON.parse(html.slice(i, k + 1)); } catch (e) { return null; } }
      }
    }
    return null;
  }

  async function fetchTrack(tr, id) {
    const tryUrl = async (u) => {
      try {
        const url = new URL(u, location.origin);
        url.searchParams.set('fmt', 'json3');
        const r = await fetch(url.toString(), { credentials: 'same-origin' });
        const t = await r.text();
        if (!t || t[0] !== '{') return null;
        const j = JSON.parse(t);
        return j && j.events ? j : null;
      } catch (e) { return null; }
    };
    let j = await tryUrl(tr.baseUrl);
    if (j) return j;
    // Cadangan: URL yang dipakai pemutar sendiri (membawa token), ada kalau CC pernah dinyalakan
    const ents = performance.getEntriesByType('resource').filter((e) =>
      e.name.includes('/api/timedtext') && e.name.includes('v=' + id) && e.name.includes('lang=' + tr.languageCode));
    for (let k = ents.length - 1; k >= 0; k--) { j = await tryUrl(ents[k].name); if (j) return j; }
    return null;
  }

  // Ubah JSON caption menjadi daftar baris {t, e, s}; buang [Music], ♪, dan baris terjemahan di cue dwibahasa
  function parseCues(j, group) {
    const cues = [];
    for (const ev of j.events || []) {
      if (!ev.segs) continue;
      const t = (ev.tStartMs || 0) / 1000;
      const e = t + (ev.dDurationMs || 0) / 1000;
      const raw = ev.segs.map((s) => s.utf8 || '').join('');
      let lines = raw.split(/\r?\n/)
        .map((x) => x.replace(/^[\s♪♫♬]+|[\s♪♫♬]+$/g, '').trim())
        .filter(Boolean)
        .filter((l) => !/^[\[\(（【][^\]\)）】]*[\]\)）】]$/.test(l));
      if (!lines.length) continue;
      if (group !== 'la' && lines.length > 1 && lines.some((l) => G(l) === group)) lines = lines.filter((l) => G(l) === group);
      cues.push({ t, e, s: lines.join(' ') });
    }
    cues.sort((a, b) => a.t - b.t);
    return cues;
  }

  // Apakah isi caption ini lirik lagu (bukan komentar/narasi)?
  function evaluate(cues, dur) {
    const n = cues.length;
    if (n < 8) return { ok: false, why: 'terlalu sedikit baris (' + n + ')' };
    const med = median(cues.map((c) => wlen(c.s)));
    const first = cues[0].t, end = cues[n - 1].e || cues[n - 1].t;
    const cover = dur > 0 ? Math.min(1, (end - first) / dur) : 0.5;
    const rate = dur > 0 ? n / (dur / 60) : 6;
    const periodEnd = cues.filter((c) => /[.。]\s*$/.test(c.s)).length / n;
    const sentence = cues.filter((c) => /[.。]\s+\S/.test(c.s)).length / n;
    const uniq = new Set(cues.map((c) => c.s.toLowerCase())).size / n;
    const info = 'baris=' + n + ' median=' + med + ' cakupan=' + cover.toFixed(2) + ' akhir.titik=' + periodEnd.toFixed(2);
    if (med > 90) return { ok: false, why: 'baris terlalu panjang (' + info + ')' };
    if (periodEnd > 0.5 || sentence > 0.3) return { ok: false, why: 'seperti kalimat/komentar (' + info + ')' };
    if (cover < 0.3) return { ok: false, why: 'cakupan waktu kecil (' + info + ')' };
    const score = (n >= 12 ? 2 : 1) + (cover >= 0.5 ? 2 : cover >= 0.4 ? 1 : 0) + (med <= 45 ? 2 : med <= 70 ? 1 : 0) +
      (periodEnd <= 0.2 ? 1 : 0) + (rate >= 4 ? 1 : 0) + (uniq < 0.95 ? 1 : 0);
    return { ok: score >= 6, score, why: 'skor ' + score + '/9 (' + info + ')' };
  }

  // Format LRC; beri baris kosong (♪) kalau ada jeda panjang antar baris
  function toLrc(cues) {
    const f = (t) => {
      const cs = Math.round(Math.max(0, t) * 100);
      const m = Math.floor(cs / 6000), s = (cs % 6000) / 100;
      return '[' + String(m).padStart(2, '0') + ':' + s.toFixed(2).padStart(5, '0') + ']';
    };
    let out = '';
    for (let i = 0; i < cues.length; i++) {
      const c = cues[i], nx = cues[i + 1];
      out += f(c.t) + c.s + '\n';
      if (nx && c.e > c.t && nx.t - c.e > 6) out += f(c.e) + '\n';
    }
    return out;
  }

  function send(body) {
    try { chrome.runtime.sendMessage({ type: 'vors-lyrics', body }, () => void chrome.runtime.lastError); } catch (e) {}
  }

  async function run(id) {
    LOG('memeriksa caption', id);
    const html = await (await fetch('/watch?v=' + id, { credentials: 'same-origin' })).text();
    if (vid() !== id) return;
    const pr = extractPlayerResponse(html);
    if (!pr) { LOG('data pemutar tidak ditemukan'); return; }
    const vd = pr.videoDetails || {};
    const title = vd.title || '', author = vd.author || '', dur = parseFloat(vd.lengthSeconds) || 0;
    const all = (pr.captions && pr.captions.playerCaptionsTracklistRenderer && pr.captions.playerCaptionsTracklistRenderer.captionTracks) || [];
    const manual = all.filter((t) => t.kind !== 'asr'); // buang caption otomatis (ASR)
    if (!manual.length) { LOG('tidak ada caption manual'); return; }

    let cand = manual.filter((t) => {
      const bad = BAD_NAME.test(nameOf(t));
      if (bad) LOG('lewati track "' + nameOf(t) + '" (komentar/terjemahan)');
      return !bad;
    });
    const tg = G(title + ' ' + author); // bahasa lagu menurut judul
    if (tg === 'cjk' || tg === 'ko') {
      cand = cand.filter((t) => langGroup(t.languageCode) === tg); // hanya bahasa asli, bukan terjemahan
    } else if (new Set(cand.map((t) => (t.languageCode || '').split('-')[0])).size > 1) {
      LOG('beberapa bahasa & bahasa lagu tidak jelas -> lewati caption'); return;
    }
    if (!cand.length) { LOG('tidak ada track bahasa asli'); return; }

    let best = null;
    for (const tr of cand) {
      const j = await fetchTrack(tr, id);
      if (vid() !== id) return;
      if (!j) { LOG('track "' + nameOf(tr) + '" tidak bisa diunduh'); continue; }
      const cues = parseCues(j, langGroup(tr.languageCode));
      const ev = evaluate(cues, dur);
      LOG('track "' + nameOf(tr) + '" [' + tr.languageCode + ']:', ev.ok ? 'LIRIK' : 'DITOLAK', ev.why);
      if (ev.ok && (!best || ev.score > best.score)) best = { score: ev.score, cues };
    }
    if (!best) return;
    const body = JSON.stringify({ title, dur, lrc: toLrc(best.cues) });
    last = { v: id, body };
    send(body);
    setTimeout(() => { if (last && last.v === id) send(last.body); }, 4000);
    LOG('lirik caption dikirim ke island (' + best.cues.length + ' baris)');
  }

  setInterval(() => {
    const id = vid();
    if (location.pathname !== '/watch' || !id || id === lastId) return;
    lastId = id; last = null;
    run(id).catch((e) => LOG('gagal', e));
  }, 1500);

  // Kirim ulang berkala (kalau island baru dijalankan saat video sudah berjalan); island mengabaikan duplikat
  setInterval(() => { if (last && vid() === last.v) send(last.body); }, 30000);
})();
