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
    void ShowIdle() { tTimer.Text = FmtDig(); RefreshTimerControls(); }
    double DigSeconds() { string d = dig.PadLeft(6, '0'); return N(d.Substring(0, 2)) * 3600.0 + N(d.Substring(2, 2)) * 60.0 + N(d.Substring(4, 2)); }
    bool CanEdit() { return !alarmOn && !tPaused && timerDeadline == 0; }
    bool TimerActive() { return tPaused || timerDeadline != 0; }
    double TimerRemainingSeconds()
    {
        return timerDeadline == 0 ? 0 :
            Math.Max(0, (timerDeadline - System.Diagnostics.Stopwatch.GetTimestamp()) /
                (double)System.Diagnostics.Stopwatch.Frequency);
    }

    void ToggleTimerPop()
    {
        if (timerPop == null)
            timerPop = new PopTimer(StartPause, CancelTimer, StopAlarm, delegate
            {
                timerPopOn = false;
                btnTimerPop.Background = Rgb(38, 38, 38);
                UpdateCollapsed();
                RefreshTimerControls();
            });

        timerPopOn = !timerPopOn;
        if (timerPopOn)
        {
            timerPop.Show();
            timerPop.Place();
            timerPop.Raise();
        }
        else timerPop.Hide();
        btnTimerPop.Background = timerPopOn ? Accent : Rgb(38, 38, 38);
        UpdateCollapsed();
        RefreshTimerControls();
    }

    void AddDigit(int dgt)
    {
        if (dig.Length >= 6 || (dig.Length == 0 && dgt == 0)) return;
        dig += dgt.ToString(); ShowIdle();
    }

    string CountdownText()
    {
        if (timerDeadline != 0) return Hms(Math.Ceiling(TimerRemainingSeconds()));
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
        if (!editing && !tTimer.IsKeyboardFocusWithin &&
            (scanTextOutput == null || !scanTextOutput.IsKeyboardFocusWithin)) return;
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
        btnPause.Content = (timerDeadline != 0) ? "\u23F8" : "\u25B6";
        btnPause.ToolTip = tPaused ? "Lanjutkan timer" : (timerDeadline != 0 ? "Jeda timer" : "Mulai timer");
        RefreshTimerControls();
    }

    void RefreshTimerControls()
    {
        if (btnPause == null) return;
        bool canPause = !alarmOn && (TimerActive() || DigSeconds() > 0);
        bool canStop = TimerActive() && !alarmOn;
        btnPause.IsEnabled = canPause;
        btnCancelTimer.IsEnabled = canStop;
        btnPause.Opacity = canPause ? 1 : 0.45;
        btnCancelTimer.Opacity = canStop ? 1 : 0.45;
        if (timerPop != null)
            timerPop.Update(TimerActive() ? CountdownText() : (DigSeconds() > 0 ? FmtDig() : ""),
                timerDeadline != 0, tPaused, alarmOn, DigSeconds() > 0);
    }

    void StartSec(double sec)
    {
        StopAlarm();
        tPaused = false; tTimer.Opacity = 1; dig = "";
        timerDeadline = System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)Math.Ceiling(sec * System.Diagnostics.Stopwatch.Frequency);
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
        if (alarmOn) return;
        if (tPaused)
        {
            timerDeadline = System.Diagnostics.Stopwatch.GetTimestamp() +
                (long)Math.Ceiling(tLeft.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
            tPaused = false; tTimer.Opacity = 1;
            SetPauseBtn(); TimerTick();
        }
        else if (timerDeadline != 0)
        {
            double remaining = TimerRemainingSeconds();
            if (remaining <= 0) { TimerTick(); return; }
            tLeft = TimeSpan.FromSeconds(remaining);
            timerDeadline = 0; tPaused = true; tTimer.Opacity = 0.55;
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
        if (!TimerActive()) return;
        StopAlarm();
        timerDeadline = 0; tPaused = false; tTimer.Opacity = 1; dig = "";
        ShowIdle(); SetPauseBtn(); UpdateCollapsed();
    }

    void TimerTick()
    {
        if (timerDeadline == 0) { RefreshTimerControls(); return; }
        double sec = Math.Ceiling(TimerRemainingSeconds());
        if (sec <= 0)
        {
            timerDeadline = 0; tTimer.Text = "Selesai!"; SetPauseBtn();
            StartAlarm();
            RefreshTimerControls();
            SwitchTo(2); // buka tab Timer, tapi island tetap ciut: teks muncul di pil
        }
        else
        {
            string text = Hms(sec);
            if (tTimer.Text != text)
            {
                tTimer.Text = text;
                UpdateCollapsed();
            }
            RefreshTimerControls();
        }
    }

    // ---------- Alarm ----------
    void StartAlarm()
    {
        if (alarmOn) return;
        alarmOn = true;
        btnStopAlarm.Visibility = Visibility.Visible;
        UpdateCollapsed();
        RefreshTimerControls();
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
        if (timerDeadline == 0 && !tPaused) { dig = ""; ShowIdle(); }
        UpdateCollapsed();
        RefreshTimerControls();
    }


}
