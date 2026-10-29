using System.Net;
using System.Text;
using System;
using System.Collections.Generic;
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

    bool LoadGlowSetting()
    {
        try
        {
            using (Win32.RegistryKey key = Win32.Registry.CurrentUser.OpenSubKey(@"Software\WinIsle"))
                return key != null && Convert.ToInt32(key.GetValue("GlowEnabled", 0)) != 0;
        }
        catch (Exception ex)
        {
            Prog.Log(ex.ToString());
            return false;
        }
    }

    void ToggleGlow()
    {
        bool enabled = !glowEnabled;
        try
        {
            using (Win32.RegistryKey key = Win32.Registry.CurrentUser.CreateSubKey(@"Software\WinIsle"))
            {
                if (key == null) throw new InvalidOperationException("Tidak dapat membuka pengaturan WinIsle di registry.");
                key.SetValue("GlowEnabled", enabled ? 1 : 0, Win32.RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            Prog.Log(ex.ToString());
            MessageBox.Show("Pengaturan glow tidak dapat disimpan:\n" + ex.Message, "WinIsle", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        glowEnabled = enabled;
        if (bodyShape != null) bodyShape.Effect = enabled ? glowEffect : null;
        RefreshGlowBtn();
    }

    void RefreshGlowBtn()
    {
        if (btnGlow == null) return;
        btnGlow.Content = "Glow: " + (glowEnabled ? "nyala" : "mati");
        btnGlow.Background = glowEnabled ? Accent : Rgb(38, 38, 38);
    }

    void CheckForUpdates()
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "https://api.github.com/repos/MKsafr/WinIsle/releases/latest");
                request.UserAgent = "WinIsle";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;

                string json;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    json = reader.ReadToEnd();

                Dictionary<string, object> release =
                    new System.Web.Script.Serialization.JavaScriptSerializer()
                        .Deserialize<Dictionary<string, object>>(json);

                object tagValue, urlValue;
                if (release == null ||
                    !release.TryGetValue("tag_name", out tagValue) ||
                    !release.TryGetValue("html_url", out urlValue))
                    return;

                string tag = Convert.ToString(tagValue);
                string releaseUrl = Convert.ToString(urlValue);
                Version latest, current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

                if (!Version.TryParse(tag.TrimStart('v', 'V'), out latest) ||
                    latest.CompareTo(current) <= 0 ||
                    string.IsNullOrWhiteSpace(releaseUrl))
                    return;

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Versi baru " + tag + " tersedia. Buka halaman unduhan?",
                        "Pembaruan WinIsle",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(releaseUrl);
                }));
            }
            catch (Exception ex)
            {
                Prog.Log("Pemeriksaan update gagal: " + ex);
            }
        });
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
