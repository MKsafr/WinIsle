using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

partial class Island
{
    // =====================================================================
    //  LIRIK LATIN + POP-OUT
    // =====================================================================
    void UpdateRomButtons()
    {
        if (btnRom == null) return;
        btnRom.Visibility = Visibility.Visible;
        btnRom.Background = romOn ? Accent : Rgb(38, 38, 38);
        btnPop.Background = popOn ? Accent : Rgb(38, 38, 38);
    }

    void ToggleRom()
    {
        romOn = !romOn;
        UpdateRomButtons();
        RefreshLyricsText();
    }

    void RefreshLyricsText()
    {
        curIdx = -2; popIdx = -2;
        if (!lsynced && lPlain.Visibility == Visibility.Visible)
            lPlainT.Text = (romOn && romKind > 0 && plainRom.Length > 0) ? plainRom : plainSrc;
    }

    void TogglePop()
    {
        popOn = !popOn;
        if (popOn)
        {
            if (pop == null) pop = new PopLyric();
            pop.Reset(); pop.Place(); pop.Show(); pop.Raise();
            popIdx = -2;
        }
        else if (pop != null) pop.Hide();
        UpdateRomButtons();
    }

    static int DetectScript(List<string> src)
    {
        int hangul = 0, kana = 0;
        foreach (string s in src)
            foreach (char c in s)
            {
                if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x1100 && c <= 0x11FF) || (c >= 0x3131 && c <= 0x318E)) hangul++;
                else if (c >= 0x3040 && c <= 0x30FF) kana++;
            }
        return (hangul >= 3 || kana >= 3) ? (hangul >= kana ? 2 : 1) : 0;
    }

    void StartRomanize()
    {
        List<string> src = new List<string>();
        if (lsynced) { foreach (LL l in lines) src.Add(l.s.Normalize(NormalizationForm.FormKC)); }
        else foreach (string x in plainSrc.Replace("\r", "").Split('\n')) src.Add(x.Normalize(NormalizationForm.FormKC));
        romKind = DetectScript(src);
        UpdateRomButtons();
        if (romKind == 0) return;

        // Tiap baris dipecah per aksara (Jepang / Korea / lainnya), jadi lagu multi-bahasa dilatinkan sesuai bahasa tiap potongan.
        List<List<RSeg>> segs = new List<List<RSeg>>();
        List<string> quick = new List<string>();
        List<string> uniq = new List<string>();
        foreach (string x in src)
        {
            List<RSeg> sg = SplitScripts(x);
            segs.Add(sg); quick.Add(JoinRom(sg, null));
            foreach (RSeg r in sg) if (r.k == 1 && HasJa(r.t) && !uniq.Contains(r.t)) uniq.Add(r.t);
        }
        ApplyRom(quick);
        if (uniq.Count == 0) return;   // tidak ada bagian Jepang (Korea sudah final)

        // Bagian Jepang: kanji dibaca dan kata dipisah lewat transliterasi online; cadangan: pembaca bawaan Windows (kana.ps1).
        string k0 = lkey;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            List<string> on = null;
            Action<int, string> onResult = delegate(int index, string result)
            {
                Dictionary<string, string> snapshot;
                lock (map)
                {
                    map[uniq[index]] = result;
                    snapshot = new Dictionary<string, string>(map);
                }
                List<string> partial = new List<string>();
                for (int i = 0; i < segs.Count; i++) partial.Add(JoinRom(segs[i], snapshot));
                Dispatcher.BeginInvoke(new Action(delegate { if (lkey == k0) ApplyRom(partial); }));
            };
            try { on = RomajiOnlineAll(uniq, onResult); } catch { }
            List<string> unresolved = new List<string>();
            if (on != null)
            {
                for (int i = 0; i < uniq.Count; i++)
                {
                    if (on[i] != null) lock (map) map[uniq[i]] = on[i];
                    else unresolved.Add(uniq[i]);
                }
            }
            else unresolved.AddRange(uniq);
            if (unresolved.Count > 0)
            {
                List<string> yomi = null;
                try { yomi = ReadingsViaPs(unresolved); } catch { }
                if (yomi == null || yomi.Count != unresolved.Count)
                {
                    if (map.Count == 0) return;
                }
                else
                    for (int i = 0; i < unresolved.Count; i++)
                        lock (map) map[unresolved[i]] = KanaToRomaji(yomi[i]);
            }
            Dictionary<string, string> finalMap;
            lock (map) finalMap = new Dictionary<string, string>(map);
            List<string> res = new List<string>();
            for (int i = 0; i < segs.Count; i++) res.Add(JoinRom(segs[i], finalMap));
            Dispatcher.BeginInvoke(new Action(delegate { if (lkey == k0) ApplyRom(res); }));
        });
    }

    void ApplyRom(List<string> res)
    {
        if (lsynced)
        {
            if (res.Count != lines.Count) return;
            for (int i = 0; i < lines.Count; i++) lines[i].r = res[i];
        }
        else plainRom = string.Join("\n", res.ToArray());
        RefreshLyricsText();
    }

    // Bacaan kanji (hiragana) dari Windows JapanesePhoneticAnalyzer, dijalankan oleh kana.ps1. Satu baris masuk = satu baris keluar.
    static List<string> ReadingsViaPs(List<string> src)
    {
        string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kana.ps1");
        if (!File.Exists(script)) return null;
        string tmp = Path.Combine(Path.GetTempPath(), "vwi_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllLines(tmp, src.ToArray(), new UTF8Encoding(false));
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -InFile \"" + tmp + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true; psi.StandardOutputEncoding = Encoding.UTF8;
            StringBuilder sb = new StringBuilder();
            using (Process pr = new Process { StartInfo = psi })
            {
                pr.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) { sb.Append(e.Data).Append('\n'); } };
                pr.Start(); pr.BeginOutputReadLine();
                if (!pr.WaitForExit(25000)) { try { pr.Kill(); } catch { } return null; }
                pr.WaitForExit();
            }
            string all; lock (sb) { all = sb.ToString(); }
            all = all.TrimStart('\uFEFF');
            List<string> res = new List<string>(all.Split('\n'));
            if (res.Count > 0 && res[res.Count - 1].Length == 0) res.RemoveAt(res.Count - 1);
            return res;
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    class RSeg { public int k; public string t; }   // k: 1 = Jepang, 2 = Korea, 0 = lainnya (latin, angka), -1 = spasi/tanda baca

    static int ScriptOf(char c)
    {
        if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x1100 && c <= 0x11FF) || (c >= 0x3131 && c <= 0x318E)) return 2;
        if ((c >= 0x3040 && c <= 0x30FF && c != 0x30FB) || (c >= 0x4E00 && c <= 0x9FFF) || c == 0x3005 || (c >= 0x31F0 && c <= 0x31FF)) return 1;
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || (c >= 0x3000 && c <= 0x303F) || (c >= 0xFF00 && c <= 0xFF0F)) return -1;
        return 0;
    }

    // Pecah satu baris menjadi potongan per aksara. Spasi/tanda baca di antara dua potongan sejenis ikut di dalam potongan itu.
    static List<RSeg> SplitScripts(string line)
    {
        List<RSeg> res = new List<RSeg>();
        StringBuilder run = new StringBuilder(), pn = new StringBuilder();
        int cur = -2;
        foreach (char c in line)
        {
            int k = ScriptOf(c);
            if (k == -1) { pn.Append(c); continue; }
            if (k == cur) { run.Append(pn.ToString()).Append(c); pn.Length = 0; continue; }
            if (cur != -2) res.Add(new RSeg { k = cur, t = run.ToString() });
            if (pn.Length > 0) res.Add(new RSeg { k = -1, t = pn.ToString() });
            pn.Length = 0; run.Length = 0; run.Append(c); cur = k;
        }
        if (cur != -2) res.Add(new RSeg { k = cur, t = run.ToString() });
        if (pn.Length > 0) res.Add(new RSeg { k = -1, t = pn.ToString() });
        return res;
    }

    // Gabungkan potongan: Korea -> romanisasi Korea, Jepang -> romaji (dari peta online kalau ada), lainnya apa adanya.
    static string JoinRom(List<RSeg> sg, Dictionary<string, string> jp)
    {
        StringBuilder o = new StringBuilder();
        foreach (RSeg s in sg)
        {
            string t;
            if (s.k == 2) t = RomanizeKo(s.t);
            else if (s.k == 1) { string m; t = (jp != null && jp.TryGetValue(s.t, out m)) ? m : KanaToRomaji(s.t); }
            else if (s.k == -1) { StringBuilder p = new StringBuilder(); foreach (char c in s.t) p.Append(JpPunct(c)); t = p.ToString(); }
            else t = s.t;
            if ((s.k == 1 || s.k == 2) && o.Length > 0 && t.Length > 0) t = char.ToLowerInvariant(t[0]) + t.Substring(1); // huruf besar hanya di awal baris
            if (o.Length > 0 && t.Length > 0 && char.IsLetterOrDigit(o[o.Length - 1]) && char.IsLetterOrDigit(t[0])) o.Append(' ');
            o.Append(t);
        }
        return Cap(o.ToString());
    }

    static bool HasJa(string s)
    {
        foreach (char c in s) if ((c >= 0x3040 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF)) return true;
        return false;
    }

    // Romaji satu baris lewat transliterasi Google (endpoint tidak resmi, butuh internet).
    // Hasilnya sudah membaca kanji dan memisahkan kata dengan spasi. null = gagal.
        static string RomajiOnline(string line)
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
            line = line.Normalize(NormalizationForm.FormKC);
            string[] urls = {
            "https://clients5.google.com/translate_a/single?client=dict-chrome-ex&sl=ja&tl=en&dt=t&dt=rm&q=" + E(line),
            "https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=en&dt=t&dt=rm&q=" + E(line)
        };
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        foreach (string u in urls)
        {
            bool f;
            string js = Http(u, out f, UA, null);
            if (js == null) continue;
            System.Collections.IList top = ser.DeserializeObject(js) as System.Collections.IList;
            if (top == null || top.Count == 0) continue;
            System.Collections.IList segs = top[0] as System.Collections.IList;
            if (segs == null) continue;
            for (int i = segs.Count - 1; i >= 0; i--)
            {
                System.Collections.IList sg = segs[i] as System.Collections.IList;
                if (sg == null || sg.Count < 3 || sg[0] != null || sg[1] != null) continue;
                string r = sg.Count > 3 ? sg[3] as string : null;
                if (string.IsNullOrEmpty(r)) r = sg[2] as string;
                if (!string.IsNullOrEmpty(r) && !HasJa(r))
                {
                    r = r.Replace("\u0101", "a").Replace("\u012B", "i").Replace("\u016B", "u").Replace("\u0113", "e").Replace("\u014D", "o").Replace("\u0100", "A").Replace("\u012A", "I").Replace("\u016A", "U").Replace("\u0112", "E").Replace("\u014C", "O");
                    return r.Trim();
                }
            }
        }
        return null;
    }

    // Romaji semua baris (2 permintaan paralel). Elemen null = baris itu tidak bisa diubah (pakai hasil cepat).
    // Mengembalikan null kalau separuh lebih baris gagal (offline / diblokir).
    static List<string> RomajiOnlineAll(List<string> src, Action<int, string> onResult)
    {
        string[] res = new string[src.Count];
        int next = -1, tot = 0, okc = 0;
        lock (RomajiCacheLock)
            for (int i = 0; i < src.Count; i++)
                if (RomajiCache.TryGetValue(src[i], out res[i]))
                {
                    System.Threading.Interlocked.Increment(ref okc);
                    onResult(i, res[i]);
                }
        List<System.Threading.Thread> ths = new List<System.Threading.Thread>();
        for (int w = 0; w < 4; w++)
        {
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                while (true)
                {
                    int i = System.Threading.Interlocked.Increment(ref next);
                    if (i >= src.Count) return;
                    if (!HasJa(src[i])) continue;
                    System.Threading.Interlocked.Increment(ref tot);
                    if (res[i] != null) { System.Threading.Interlocked.Increment(ref okc); continue; }
                    string r = null;
                    for (int attempt = 0; attempt < 2 && r == null; attempt++)
                    {
                        if (attempt > 0) System.Threading.Thread.Sleep(300);
                        try { r = RomajiOnline(src[i]); } catch { }
                    }
                    if (r != null)
                    {
                        res[i] = r;
                        lock (RomajiCacheLock)
                            if (RomajiCache.Count < 2000) RomajiCache[src[i]] = r;
                        System.Threading.Interlocked.Increment(ref okc);
                        onResult(i, r);
                    }
                }
            });
            t.IsBackground = true; t.Start(); ths.Add(t);
        }
        DateTime end = DateTime.Now.AddSeconds(40);
        foreach (System.Threading.Thread t in ths)
        {
            double ms = (end - DateTime.Now).TotalMilliseconds;
            if (ms > 0) t.Join((int)ms);
        }
        if (tot == 0 || okc * 2 < tot) return null;
        return new List<string>(res);
    }

    static string Cap(string s)
    {
        s = s.Trim();
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    }

    // ---------- Kana -> romaji (Hepburn) ----------
    static readonly Dictionary<char, string> Kana = BuildKana();
    static readonly Dictionary<char, string> SmallCombo = BuildSmallCombo();
    static readonly Dictionary<char, string> Kanji = new Dictionary<char, string>
    {
        { '\u898B', "mi" }, { '\u624B', "te" }, { '\u76EE', "me" }, { '\u6C17', "ki" }, { '\u65E5', "hi" },
        { '\u4EBA', "hito" }, { '\u5FC3', "kokoro" }, { '\u611B', "ai" }, { '\u604B', "koi" }, { '\u5922', "yume" },
        { '\u591C', "yoru" }, { '\u4ECA', "ima" }, { '\u79C1', "watashi" }, { '\u50D5', "boku" }, { '\u541B', "kimi" },
        { '\u97F3', "oto" }, { '\u6B4C', "uta" }, { '\u7A7A', "sora" }, { '\u98A8', "kaze" }, { '\u96E8', "ame" },
        { '\u82B1', "hana" }, { '\u661F', "hoshi" }, { '\u6708', "tsuki" }, { '\u5149', "hikari" }, { '\u9053', "michi" },
        { '\u58F0', "koe" }, { '\u60F3', "omoi" }, { '\u6C9F', "namida" }, { '\u679C', "hate" }, { '\u4FE1', "shin" },
        { '\u5C55', "ten" }, { '\u958B', "kai" }, { '\u524D', "mae" }, { '\u5F8C', "ato" }, { '\u4E0A', "ue" },
        { '\u4E0B', "shita" }, { '\u4E2D', "naka" }, { '\u4F55', "nani" }, { '\u8AB0', "dare" }, { '\u6642', "toki" }
    };
    

    static Dictionary<char, string> BuildKana()
    {
        Dictionary<char, string> d = new Dictionary<char, string>();
        string all = "あa いi うu えe おo かka きki くku けke こko さsa しshi すsu せse そso たta ちchi つtsu てte とto なna にni ぬnu ねne のno " +
                     "はha ひhi ふfu へhe ほho まma みmi むmu めme もmo やya ゆyu よyo らra りri るru れre ろro わwa ゐi ゑe をo " +
                     "がga ぎgi ぐgu げge ごgo ざza じji ずzu ぜze ぞzo だda ぢji づzu でde どdo ばba びbi ぶbu べbe ぼbo ぱpa ぴpi ぷpu ぺpe ぽpo " +
                     "ゔvu ぁa ぃi ぅu ぇe ぉo ゎwa ゃya ゅyu ょyo";
        foreach (string tok in all.Split(' ')) d[tok[0]] = tok.Substring(1);
        return d;
    }

    static Dictionary<char, string> BuildSmallCombo()
    {
        Dictionary<char, string> d = new Dictionary<char, string>();
        d['ふ'] = "f"; d['て'] = "t"; d['で'] = "d"; d['う'] = "w"; d['ゔ'] = "v";
        d['し'] = "sh"; d['じ'] = "j"; d['ち'] = "ch"; d['つ'] = "ts"; d['い'] = "y";
        return d;
    }

    static char NormKana(char c) { return (c >= 0x30A1 && c <= 0x30F6) ? (char)(c - 0x60) : c; }

    static string JpPunct(char c)
    {
        switch (c)
        {
            case '\u3001': return ",";
            case '\u3002': return ".";
            case '\u30FB': return " ";
            case '\u3000': return " ";
            case '\u300C': case '\u300D': case '\u300E': case '\u300F': return "\"";
            case '\uFF01': return "!";
            case '\uFF1F': return "?";
            case '\u301C': case '\uFF5E': return "~";
            default: return c.ToString();
        }
    }

    static string KanaToRomaji(string src)
    {
        StringBuilder o = new StringBuilder();
        bool sokuon = false;
        for (int i = 0; i < src.Length; i++)
        {
            char c = NormKana(src[i]);
            if (c == '\u3063') { sokuon = true; continue; }                       // small tsu: gandakan konsonan berikutnya
            if (c == '\u30FC')                                                    // tanda panjang: ulang vokal terakhir
            {
                if (o.Length > 0 && "aiueo".IndexOf(o[o.Length - 1]) >= 0) o.Append(o[o.Length - 1]);
                continue;
            }
            if (c == '\u3093')                                                    // n
            {
                char n2 = i + 1 < src.Length ? NormKana(src[i + 1]) : '\0';
                o.Append("あいうえおやゆよ".IndexOf(n2) >= 0 && n2 != '\0' ? "n'" : "n");
                continue;
            }
            string b;
            if (!Kana.TryGetValue(c, out b) && !Kanji.TryGetValue(c, out b)) { sokuon = false; o.Append(JpPunct(src[i])); continue; }

            char nx = i + 1 < src.Length ? NormKana(src[i + 1]) : '\0';
            string r = b; int used = 1;
            if ((nx == '\u3083' || nx == '\u3085' || nx == '\u3087') && b.Length >= 2 && b[b.Length - 1] == 'i')
            {
                string stem = b.Substring(0, b.Length - 1);
                string v = nx == '\u3083' ? "a" : (nx == '\u3085' ? "u" : "o");
                r = (stem == "sh" || stem == "ch" || stem == "j") ? stem + v : stem + "y" + v;
                used = 2;
            }
            else if (nx == '\u3041' || nx == '\u3043' || nx == '\u3045' || nx == '\u3047' || nx == '\u3049')
            {
                string cons;
                if (SmallCombo.TryGetValue(c, out cons)) { r = cons + Kana[nx]; used = 2; }
            }
            if (sokuon)
            {
                sokuon = false;
                if (r.StartsWith("ch")) r = "t" + r;
                else if ("aiueo".IndexOf(r[0]) < 0) r = r[0] + r;
            }
            o.Append(r);
            i += used - 1;
        }
        return Cap(o.ToString());
    }

    // ---------- Hangul -> romanisasi (Revised Romanization + aturan bunyi dasar) ----------
    class KT { public bool syl; public int i, m, f; public string lit; }

    static readonly string[] KoIni = { "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" };
    static readonly string[] KoMed = { "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i" };
    // indeks batchim -> batchim wakil saat bunyi di akhir/sebelum konsonan
    static readonly int[] KoNeu = { 0, 1, 1, 1, 4, 4, 4, 7, 8, 1, 16, 8, 8, 8, 17, 8, 16, 17, 17, 7, 7, 21, 7, 7, 1, 7, 17, 7 };
    // batchim tunggal -> konsonan awal saat dipindah ke suku kata berikutnya (liaison)
    static readonly int[] KoLia = { -1, 0, 1, -1, 2, -1, -1, 3, 5, -1, -1, -1, -1, -1, -1, -1, 6, 7, -1, 9, 10, -1, 12, 14, 15, 16, 17, -1 };
    // batchim ganda: bagian yang tinggal (keep) dan yang pindah (move, -1 = tidak ada / hilang)
    static readonly int[] KoKeep = { 0, 0, 0, 1, 0, 4, 4, 0, 0, 8, 8, 8, 8, 8, 8, 8, 0, 0, 17, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    static readonly int[] KoMove = { -1, -1, -1, 9, -1, 12, -1, -1, -1, 0, 6, 7, 9, 16, 17, -1, -1, -1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

    static string KoFinStr(int f)
    {
        switch (f) { case 1: return "k"; case 4: return "n"; case 7: return "t"; case 8: return "l"; case 16: return "m"; case 17: return "p"; case 21: return "ng"; default: return ""; }
    }

    static string RomanizeKo(string s)
    {
        List<KT> ts = new List<KT>();
        foreach (char c in s)
        {
            KT t = new KT();
            if (c >= 0xAC00 && c <= 0xD7A3) { int x = c - 0xAC00; t.syl = true; t.i = x / 588; t.m = (x % 588) / 28; t.f = x % 28; }
            else t.lit = c.ToString();
            ts.Add(t);
        }
        for (int k = 0; k < ts.Count; k++)
        {
            KT a = ts[k];
            if (!a.syl || a.f == 0) continue;
            KT b = (k + 1 < ts.Count && ts[k + 1].syl) ? ts[k + 1] : null;
            if (b == null) { a.f = KoNeu[a.f]; continue; }

            if (b.i == 11)                                   // suku kata berikut diawali vokal: batchim pindah (liaison)
            {
                if (a.f == 21) continue;                     // ng tetap
                if (a.f == 27) { a.f = 0; continue; }        // h senyap
                if (KoKeep[a.f] != 0)                        // batchim ganda
                {
                    int mv = KoMove[a.f]; a.f = KoKeep[a.f];
                    if (mv >= 0) b.i = mv;
                    continue;
                }
                int ni = KoLia[a.f];
                if (b.m == 20 && (a.f == 7 || a.f == 25)) ni = (a.f == 7) ? 12 : 14;   // palatalisasi: d/t + i -> j/ch
                if (ni >= 0) { b.i = ni; a.f = 0; }
                continue;
            }
            if (a.f == 27 && (b.i == 0 || b.i == 3 || b.i == 12)) { b.i = b.i == 0 ? 15 : (b.i == 3 ? 16 : 14); a.f = 0; continue; }       // h + g/d/j -> k/t/ch
            if (b.i == 18 && (a.f == 1 || a.f == 7 || a.f == 17 || a.f == 22)) { b.i = a.f == 1 ? 15 : (a.f == 7 ? 16 : (a.f == 17 ? 17 : 14)); a.f = 0; continue; }  // g/d/b/j + h

            int nf = KoNeu[a.f]; a.f = nf;
            if (b.i == 5 && (nf == 21 || nf == 16 || nf == 1 || nf == 7 || nf == 17)) b.i = 2;   // r -> n setelah ng/m/k/t/p
            if (b.i == 2 || b.i == 6)                                                            // nasalisasi k/t/p sebelum n/m
            {
                if (nf == 1) a.f = 21; else if (nf == 7) a.f = 4; else if (nf == 17) a.f = 16;
            }
            if (nf == 4 && b.i == 5) a.f = 8;                                                    // n + r -> ll
            if (nf == 8 && b.i == 2) b.i = 5;                                                    // l + n -> ll
        }
        StringBuilder o = new StringBuilder();
        int prevFin = 0;
        foreach (KT t in ts)
        {
            if (!t.syl) { o.Append(t.lit); prevFin = 0; continue; }
            string ini = KoIni[t.i];
            if (t.i == 5 && prevFin == 8) ini = "l";
            o.Append(ini).Append(KoMed[t.m]).Append(KoFinStr(t.f));
            prevFin = t.f;
        }
        return Cap(o.ToString());
    }


}
