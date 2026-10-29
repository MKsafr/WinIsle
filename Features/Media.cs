using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

partial class Island
{
    // =====================================================================
    //  MEDIA (proses PowerShell tunggal yang terus berjalan)
    // =====================================================================
    static string ScriptPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media.ps1"); }

    void StartMedia()
    {
        if (quitting) return;
        if (!File.Exists(ScriptPath())) { SetStat("media.ps1 tidak ditemukan. Taruh satu folder dengan WinIsle.exe"); return; }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ScriptPath() + "\" -ParentPid " + Process.GetCurrentProcess().Id);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true; psi.StandardOutputEncoding = Encoding.UTF8;
            Process pr = new Process { StartInfo = psi, EnableRaisingEvents = true };
            pr.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                string line = e.Data;
                Dispatcher.BeginInvoke(new Action(delegate { try { ParseMedia(line); } catch (Exception ex) { Prog.Log(ex.ToString()); } }));
            };
            pr.Exited += delegate
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (quitting) return;
                        if ((DateTime.Now - mediaStarted).TotalSeconds < 6) mediaFails++; else mediaFails = 0;
                        if (mediaFails >= 4) { SetStat("Gagal menjalankan media.ps1 (lihat TUTORIAL)"); return; }
                        DispatcherTimer rt = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                        rt.Tick += delegate { rt.Stop(); StartMedia(); };
                        rt.Start();
                    }));
                }
                catch { }
            };
            mediaStarted = DateTime.Now;
            pr.Start();
            pr.BeginOutputReadLine();
            mediaProc = pr;
        }
        catch (Exception ex) { Prog.Log(ex.ToString()); }
    }

    void KillMedia()
    {
        try { if (mediaProc != null && !mediaProc.HasExited) mediaProc.Kill(); } catch { }
    }

    void ParseMedia(string line)
    {
        line = line.Trim('\uFEFF', ' ', '\r', '\n', '\t');
        if (line.Length == 0) return;
        string ti = "", ar = "", al = ""; bool pl = false; double pos = -1, dur = 0;
        Dictionary<string, object> d = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(line) as Dictionary<string, object>;
        if (d != null && d.ContainsKey("title"))
        {
            ti = Str(d, "title"); ar = Str(d, "artist"); al = Str(d, "album");
            pl = d.ContainsKey("playing") && d["playing"] is bool && (bool)d["playing"];
            if (!double.TryParse(Str(d, "pos"), NumberStyles.Float, IC, out pos)) pos = -1;
            if (!double.TryParse(Str(d, "dur"), NumberStyles.Float, IC, out dur)) dur = 0;
        }
        OnMedia(ti, ar, al, pl, pos, dur);
    }

    void OnMedia(string ti, string ar, string al, bool pl, double pos, double dur)
    {
        string title = ti.Length > 0 ? ti : "Tidak ada media";
        if (tTitle.Text != title) tTitle.Text = title;
        curTi = ti;
        curAlbum = al;
        if (tArtist.Text != ar) tArtist.Text = ar;
        string collapsed = (pl && ti.Length > 0) ? ti : "";
        if (mediaColl != collapsed) { mediaColl = collapsed; UpdateCollapsed(); }
        string play = pl ? "\u23F8" : "\u25B6";
        if (!Equals(btnPlay.Content, play)) btnPlay.Content = play;
        curDur = dur > 0 ? dur : 0;
        if (DateTime.Now >= seekHoldUntil)
        {
            playingNow = pl;
            basePos = (pos >= 0 && pos <= 36000 && (dur <= 0 || pos <= dur + 30)) ? pos : -1;
            baseAt = DateTime.Now;
        }

        string key = ti + "\u0001" + ar;
        if (key == lkey) return;
        lkey = key; lines.Clear(); lsynced = false; curIdx = -2;
        double o; offset = offsets.TryGetValue(key, out o) ? o : 0; ShowOff();
        if (ti.Length == 0) { SetStat("Putar lagu untuk menampilkan lirik"); return; }
        LRes cr;
        if (cache.TryGetValue(key, out cr)) { ApplyLyrics(cr); return; }
        SearchLyricsAsync(ti, ar, al, dur, key);
    }

    void RetryLyrics()
    {
        if (!lyricConnectionError || string.IsNullOrEmpty(curTi)) return;
        string key = curTi + "\u0001" + tArtist.Text;
        cache.Remove(key);
        SearchLyricsAsync(curTi, tArtist.Text, curAlbum, curDur, key);
    }

    void SearchLyricsAsync(string title, string artist, string album, double duration, string key)
    {
        SetStat("Mencari lirik...");
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            LRes r = null;
            try { r = Fetch(title, artist, album, duration); } catch { }
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (r != null && !r.err) cache[key] = r;
                if (lkey == key) ApplyLyrics(r);
            }));
        });
    }

    double CurPos()
    {
        if (basePos < 0) return -1;
        double p = basePos + (playingNow ? (DateTime.Now - baseAt).TotalSeconds : 0);
        if (curDur > 0 && p > curDur) p = curDur;
        return p;
    }

    void ProgressTick()
    {
        if (!open || currentTab != 0 || isDragging) return;
        double p = CurPos();
        if (curDur <= 0 || p < 0)
        {
            seekFill.Width = 0;
            if (tPos.Text != "0:00") tPos.Text = "0:00";
            string duration = curDur > 0 ? Fmt(curDur) : "0:00";
            if (tDur.Text != duration) tDur.Text = duration;
            return;
        }
        seekFill.Width = seekBar.ActualWidth * Math.Min(1, p / curDur);
        string position = Fmt(p), durationText = Fmt(curDur);
        if (tPos.Text != position) tPos.Text = position;
        if (tDur.Text != durationText) tDur.Text = durationText;
    }

    void DragTo(double x)
    {
        double w = seekBar.ActualWidth;
        if (w <= 0) return;
        dragRatio = Math.Max(0, Math.Min(1, x / w));
        seekFill.Width = w * dragRatio;
        tPos.Text = Fmt(dragRatio * curDur);
    }

    void SeekTo(double sec)
    {
        if (curDur <= 0) return;
        sec = Math.Max(0, Math.Min(curDur - 1, sec));
        basePos = sec; baseAt = DateTime.Now; seekHoldUntil = DateTime.Now.AddSeconds(2.5);
        if (!File.Exists(ScriptPath())) return;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ScriptPath() + "\" -SeekTo " + sec.ToString("0.0", IC));
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch { }
    }

    void SkipTrack(bool next)
    {
        if (!File.Exists(ScriptPath()))
        {
            Prog.Log("media.ps1 tidak ditemukan; tidak dapat mengubah track.");
            return;
        }
        try
        {
            string action = next ? "-SkipNext" : "-SkipPrevious";
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ScriptPath() + "\" " + action);
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch (Exception ex) { Prog.Log(ex.ToString()); }
    }


}
