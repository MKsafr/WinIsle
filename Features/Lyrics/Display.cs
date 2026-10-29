using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

partial class Island
{
    // =====================================================================
    //  LIRIK
    // =====================================================================
    void SetOffset(double v)
    {
        offset = Math.Round(v * 2) / 2;
        offsets[lkey] = offset;
        ShowOff();
    }

    void ShowOff() { lOff.Text = (offset >= 0 ? "+" : "") + offset.ToString("0.0", IC) + "s"; curIdx = -2; popIdx = -2; }

    void SetStat(string t)
    {
        romKind = 0; popIdx = -2; UpdateRomButtons();
        lyricConnectionError = t == "Tidak bisa terhubung ke LRCLIB. Periksa internet.";
        if (btnLyricRefresh != null) btnLyricRefresh.IsEnabled = lyricConnectionError;
        lStat.Text = t; lStat.Visibility = Visibility.Visible;
        lSync.Visibility = Visibility.Collapsed; lPlain.Visibility = Visibility.Collapsed;
    }

    void ApplyLyrics(LRes r)
    {
        lyricConnectionError = false;
        if (btnLyricRefresh != null) btnLyricRefresh.IsEnabled = false;
        lines.Clear(); lsynced = false; curIdx = -2; popIdx = -2; plainSrc = ""; plainRom = ""; romKind = 0; UpdateRomButtons();
        if (r == null) { SetStat("Lirik tidak ditemukan"); return; }
        if (r.err) { SetStat("Tidak bisa terhubung ke LRCLIB. Periksa internet."); return; }
        if (r.instr) { SetStat("\u266A Instrumental"); return; }
        if (r.synced.Length > 0) ParseLrc(r.synced);
        if (lines.Count > 0)
        {
            lsynced = true;
            lStat.Visibility = Visibility.Collapsed; lPlain.Visibility = Visibility.Collapsed; lSync.Visibility = Visibility.Visible;
            StartRomanize();
            return;
        }
        if (r.plain.Length > 0)
        {
            plainSrc = StripPlainTranslations(r.plain); lPlainT.Text = plainSrc; lPlain.ScrollToTop();
            lStat.Visibility = Visibility.Collapsed; lSync.Visibility = Visibility.Collapsed; lPlain.Visibility = Visibility.Visible;
            lHint.Text = "";
            StartRomanize();
            return;
        }
        SetStat("Lirik tidak ditemukan");
    }

    static string StripPlainTranslations(string lyrics)
    {
        string[] lines = lyrics.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = StripTranslation(lines[i]);
        return string.Join("\n", lines);
    }

    static string StripTranslation(string line)
    {
        int translation = line.IndexOf('^');
        return translation >= 0 ? line.Substring(0, translation).TrimEnd() : line;
    }

    void ParseLrc(string src)
    {
        foreach (string raw in src.Split('\n'))
        {
            MatchCollection ms = Ts.Matches(raw);
            if (ms.Count == 0) continue;
            string rawText = Wt.Replace(Ts.Replace(raw, ""), "").Trim();
            string txt = StripTranslation(rawText).Trim();
            if (rawText.Length > 0 && txt.Length == 0) continue;
            if (txt.Length == 0) txt = "\u266A";
            foreach (Match m in ms)
                lines.Add(new LL { t = int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value, IC), s = txt });
        }
        lines.Sort(delegate(LL x, LL y) { return x.t.CompareTo(y.t); });
    }

    string Disp(LL l) { return (romOn && romKind > 0 && !string.IsNullOrEmpty(l.r)) ? l.r : l.s; }

    void LyricTick()
    {
        bool ui = open && currentTab == 1;
        if (!ui && !popOn) return;
        if (!lsynced || lines.Count == 0)
        {
            if (ui && lHint.Text.Length > 0) lHint.Text = "";
            if (popOn && pop != null) pop.SetText("");
            return;
        }
        double p = CurPos();
        double pos = (p < 0 ? 0 : p) + offset;
        int lo = 0, hi = lines.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (lines[mid].t <= pos) lo = mid + 1;
            else hi = mid;
        }
        int idx = lo - 1;

        if (ui)
        {
            string hint = p < 0 ? "Pemutar tidak melaporkan posisi lagu, lirik tidak bisa bergerak otomatis." : "";
            if (lHint.Text != hint) lHint.Text = hint;
            if (idx != curIdx)
            {
                int prev = curIdx;
                curIdx = idx;
                for (int k = 0; k < 5; k++)
                {
                    int j = idx - 2 + k;
                    ly[k].Text = (j >= 0 && j < lines.Count) ? Disp(lines[j]) : "";
                }
                if (prev != -2) AnimLine(idx > prev);
            }
        }
        if (popOn && pop != null && idx != popIdx)
        {
            popIdx = idx;
            pop.SetText(idx >= 0 ? Disp(lines[idx]) : "");
        }
    }

    // Baris lirik meluncur; baris baru menyala, baris lama meredup
    void AnimLine(bool fwd)
    {
        TimeSpan du = TimeSpan.FromMilliseconds(380);
        CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        lyTr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fwd ? 18 : -18, 0, du) { EasingFunction = ease });
        ly[2].BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, du));
        ly[fwd ? 1 : 3].BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.4, du));
        ly[fwd ? 4 : 0].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.4, du));
    }


}
