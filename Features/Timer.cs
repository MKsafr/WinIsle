using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

partial class Island
{
    // =====================================================================
    //  TIMER
    // =====================================================================
    static int N(string x) { int v; return int.TryParse(x, out v) ? v : 0; }

    static string Hms(double sec)
    {
        int t = (int)Math.Max(0, sec);
        return (t / 3600).ToString("00") + ":" + ((t % 3600) / 60).ToString("00") + ":" + (t % 60).ToString("00");
    }

    // Angka diketik dari kanan (seperti timer HP): 1 5 -> 00:00:15, lalu 0 0 -> 00:15:00
    string FmtDig() { string d = dig.PadLeft(6, '0'); return d.Substring(0, 2) + ":" + d.Substring(2, 2) + ":" + d.Substring(4, 2); }
    void ShowIdle() { tTimer.Text = FmtDig(); }
    double DigSeconds() { string d = dig.PadLeft(6, '0'); return N(d.Substring(0, 2)) * 3600.0 + N(d.Substring(2, 2)) * 60.0 + N(d.Substring(4, 2)); }
    bool CanEdit() { return !alarmOn && !tPaused && endT == DateTime.MinValue; }

    void AddDigit(int dgt)
    {
        if (dig.Length >= 6 || (dig.Length == 0 && dgt == 0)) return;
        dig += dgt.ToString(); ShowIdle();
    }

    string CountdownText()
    {
        if (endT != DateTime.MinValue) return Hms(Math.Ceiling((endT - DateTime.Now).TotalSeconds));
        if (tPaused) return Hms(Math.Ceiling(tLeft.TotalSeconds));
        return "";
    }

    // Window island tidak bisa diaktifkan (NOACTIVATE). Saat mengetik, izinkan sementara supaya keyboard masuk.
    void SetActivatable(bool on)
    {
        if (hwnd == IntPtr.Zero) return;
        int ex = GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, on ? (ex & ~0x08000000) : (ex | 0x08000000));
    }

    void ReleaseEdit() { EndEdit(true); }

    // Keluar dari mode ketik timer. restoreFg=true: kembalikan fokus keyboard ke aplikasi sebelumnya.
    void EndEdit(bool restoreFg)
    {
        if (!editing && !tTimer.IsKeyboardFocusWithin) return;
        if (restoreFg && prevFg != IntPtr.Zero && prevFg != hwnd) SetForegroundWindow(prevFg);
        Keyboard.ClearFocus();
        editing = false; tTimer.Foreground = Brushes.White; SetActivatable(false);
    }

    static bool IsMod(Key k)
    {
        return k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt || k == Key.System;
    }

    void SetPauseBtn()
    {
        btnPause.Content = (endT != DateTime.MinValue) ? "\u23F8" : "\u25B6";
        btnPause.ToolTip = tPaused ? "Lanjutkan timer" : (endT != DateTime.MinValue ? "Jeda timer" : "Mulai timer");
    }

    void StartSec(double sec)
    {
        StopAlarm();
        tPaused = false; tTimer.Opacity = 1; dig = "";
        endT = DateTime.Now.AddSeconds(sec);
        SetPauseBtn();
        TimerTick();
        ReleaseEdit();
    }

    void SelectTimer(int minutes)
    {
        if (!CanEdit()) return;
        dig = (minutes * 100).ToString();
        ShowIdle();
        SetPauseBtn();
    }

    // Mulai (dari angka yang diketik) / Jeda / Lanjut
    void StartPause()
    {
        if (tPaused)
        {
            endT = DateTime.Now.Add(tLeft); tPaused = false; tTimer.Opacity = 1;
            SetPauseBtn(); TimerTick();
        }
        else if (endT != DateTime.MinValue)
        {
            tLeft = endT - DateTime.Now;
            if (tLeft < TimeSpan.Zero) tLeft = TimeSpan.Zero;
            endT = DateTime.MinValue; tPaused = true; tTimer.Opacity = 0.55;
            SetPauseBtn();
            tTimer.Text = Hms(Math.Ceiling(tLeft.TotalSeconds));
            UpdateCollapsed();
        }
        else
        {
            double sec = DigSeconds();
            if (sec > 0) StartSec(sec);
        }
    }

    void CancelTimer()
    {
        StopAlarm();
        endT = DateTime.MinValue; tPaused = false; tTimer.Opacity = 1; dig = "";
        ShowIdle(); SetPauseBtn(); UpdateCollapsed();
    }

    void TimerTick()
    {
        if (endT == DateTime.MinValue) return;
        double sec = Math.Ceiling((endT - DateTime.Now).TotalSeconds);
        if (sec <= 0)
        {
            endT = DateTime.MinValue; tTimer.Text = "Selesai!"; SetPauseBtn();
            StartAlarm();
            SwitchTo(2); // buka tab Timer, tapi island tetap ciut: teks muncul di pil
        }
        else
        {
            string text = Hms(sec);
            if (tTimer.Text == text) return;
            tTimer.Text = text;
            UpdateCollapsed();
        }
    }

    // ---------- Alarm ----------
    void StartAlarm()
    {
        if (alarmOn) return;
        alarmOn = true;
        btnStopAlarm.Visibility = Visibility.Visible;
        UpdateCollapsed();
        PauseAllMedia();   // jeda semua lagu/video yang sedang diputar supaya alarm langsung terdengar
        System.Threading.Thread th = new System.Threading.Thread(AlarmLoop);
        th.IsBackground = true; th.Start();
    }

    // Jeda semua sesi media Windows yang sedang berstatus Playing (Spotify, YouTube di browser, pemutar video, dst.)
    // lewat media.ps1 -PauseAll. Jalan di proses terpisah supaya tidak menahan UI. Tidak otomatis melanjutkan setelah alarm.
    void PauseAllMedia()
    {
        if (!File.Exists(ScriptPath())) return;
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ScriptPath() + "\" -PauseAll");
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch (Exception ex) { Prog.Log(ex.ToString()); }
    }

    void AlarmLoop()
    {
        while (alarmOn)
        {
            for (int i = 0; i < 3 && alarmOn; i++)
            {
                try { Console.Beep(1000, 160); }
                catch { System.Media.SystemSounds.Exclamation.Play(); System.Threading.Thread.Sleep(160); }
                System.Threading.Thread.Sleep(70);
            }
            for (int w = 0; w < 8 && alarmOn; w++) System.Threading.Thread.Sleep(100);
        }
    }

    void StopAlarm()
    {
        if (!alarmOn) return;
        alarmOn = false;
        btnStopAlarm.Visibility = Visibility.Collapsed;
        if (endT == DateTime.MinValue && !tPaused) { dig = ""; ShowIdle(); }
        UpdateCollapsed();
    }


}
