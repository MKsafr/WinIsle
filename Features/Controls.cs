using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Win32 = Microsoft.Win32;

partial class Island
{
    // =====================================================================
    //  KONTROL
    // =====================================================================
    void ToggleAwake()
    {
        awake = !awake;
        SetThreadExecutionState(awake ? 0x80000003u : 0x80000000u);
        btnAwake.Content = "\u2615 Keep awake: " + (awake ? "nyala" : "mati");
        btnAwake.Background = awake ? Accent : Rgb(38, 38, 38);
    }

    bool AutoStartOn()
    {
        try
        {
            using (Win32.RegistryKey k = Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("WinIsle") != null;
        }
        catch { return false; }
    }

    // Pertama kali dijalankan: nyalakan "Mulai otomatis" (sekali saja; selanjutnya ikut pilihan di tab Kontrol).
    // Tiap dijalankan: kalau sudah aktif tapi lokasi exe berpindah, perbarui path-nya.
    void AutoStartInit()
    {
        try
        {
            string exe = "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"";
            using (Win32.RegistryKey app = Win32.Registry.CurrentUser.CreateSubKey(@"Software\WinIsle"))
            using (Win32.RegistryKey run = Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (app == null || run == null) return;
                if (app.GetValue("AutoInit") == null) { app.SetValue("AutoInit", 1); run.SetValue("WinIsle", exe); }
                else
                {
                    object cur = run.GetValue("WinIsle");
                    if (cur != null && !exe.Equals(Convert.ToString(cur))) run.SetValue("WinIsle", exe);
                }
            }
        }
        catch { }
    }

    void ToggleAutostart()
    {
        try
        {
            using (Win32.RegistryKey k = Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (AutoStartOn()) k.DeleteValue("WinIsle", false);
                else k.SetValue("WinIsle", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
            }
        }
        catch { }
        RefreshAutoBtn();
    }

    void RefreshAutoBtn()
    {
        bool on = AutoStartOn();
        btnAuto.Content = "\U0001F680 Mulai otomatis: " + (on ? "nyala" : "mati");
        btnAuto.Background = on ? Accent : Rgb(38, 38, 38);
    }


}
