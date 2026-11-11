using System.Net;
using System.Text;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Win32 = Microsoft.Win32;

partial class Island
{
    // =====================================================================
    //  KONTROL
    // =====================================================================
    static readonly double[] WidgetScales = { 0.5, 0.75, 1, 1.5, 2 };
    static readonly object UpdateLogLock = new object();

    static string WidgetScaleLabel(double scale)
    {
        return scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
    }

    double EffectiveWidgetScale()
    {
        Rect workArea = SystemParameters.WorkArea;
        double fitScale = Math.Min(workArea.Width / WW, workArea.Height / WH);
        return Math.Min(widgetScale, Math.Max(0.01, fitScale));
    }

    double LoadWidgetScale()
    {
        try
        {
            using (Win32.RegistryKey key = Win32.Registry.CurrentUser.OpenSubKey(@"Software\WinIsle"))
            {
                if (key == null) return 1;
                object configuredScale = key.GetValue("WidgetScale");
                if (configuredScale == null) return 1;
                double scale;
                if (!double.TryParse(Convert.ToString(configuredScale, System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out scale))
                {
                    Prog.Log("Pengaturan skala widget tidak valid; menggunakan skala 1x.");
                    return 1;
                }
                foreach (double supportedScale in WidgetScales)
                    if (scale == supportedScale) return scale;
                Prog.Log("Pengaturan skala widget tidak valid; menggunakan skala 1x.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Prog.Log(ex.ToString());
            return 1;
        }
    }

    void SetWidgetScale(double scale)
    {
        bool supported = false;
        foreach (double supportedScale in WidgetScales)
            if (scale == supportedScale) { supported = true; break; }
        if (!supported || scale == widgetScale) return;

        try
        {
            using (Win32.RegistryKey key = Win32.Registry.CurrentUser.CreateSubKey(@"Software\WinIsle"))
            {
                if (key == null) throw new InvalidOperationException("Tidak dapat membuka pengaturan WinIsle di registry.");
                key.SetValue("WidgetScale", scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), Win32.RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            Prog.Log(ex.ToString());
            MessageBox.Show("Pengaturan skala widget tidak dapat disimpan:\n" + ex.Message, "WinIsle", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        widgetScale = scale;
        ApplyRenderedScale(EffectiveWidgetScale());
        Reposition();
        RefreshScaleButtons();
    }

    void ApplyRenderedScale(double scale)
    {
        renderScale = scale;
        Width = WW * renderScale;
        Height = WH * renderScale;
        if (isl == null) return;
        isl.BeginAnimation(WidthProperty, null);
        isl.BeginAnimation(HeightProperty, null);
        ((Grid)isl.Child).LayoutTransform = new System.Windows.Media.ScaleTransform(renderScale, renderScale);
        isl.Margin = new Thickness(12 * renderScale, 12 * renderScale, 0, 12 * renderScale);
        isl.CornerRadius = new CornerRadius((open ? 22 : 12) * renderScale, 0, 0, (open ? 22 : 12) * renderScale);
        isl.Width = (open ? OW : CW) * renderScale;
        isl.Height = (open ? OH : collH) * renderScale;
        if (glowEffect != null) glowEffect.BlurRadius = 12 * renderScale;
        UpdateBodyShape();
    }

    void RefreshScaleButtons()
    {
        if (scaleButtons == null) return;
        for (int i = 0; i < scaleButtons.Length; i++)
        {
            bool selected = WidgetScales[i] == widgetScale;
            scaleButtons[i].Background = selected ? Accent : Rgb(38, 38, 38);
            scaleButtons[i].Opacity = selected ? 1 : 0.72;
        }
    }

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

                object tagValue, assetsValue;
                if (release == null ||
                    !release.TryGetValue("tag_name", out tagValue) ||
                    !release.TryGetValue("assets", out assetsValue))
                    return;

                string tag = Convert.ToString(tagValue);
                Version latest, current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

                if (!Version.TryParse(tag.TrimStart('v', 'V'), out latest) ||
                    latest.CompareTo(current) <= 0)
                    return;

                string assetName = "WinIsle-" + tag + ".zip";
                string downloadUrl = null, digest = null;
                System.Collections.IList assets = assetsValue as System.Collections.IList;
                if (assets == null) throw new InvalidDataException("Daftar aset release tidak valid.");
                foreach (object value in assets)
                {
                    Dictionary<string, object> asset = value as Dictionary<string, object>;
                    object nameValue, urlValue, digestValue;
                    if (asset == null || !asset.TryGetValue("name", out nameValue) ||
                        !assetName.Equals(Convert.ToString(nameValue), StringComparison.Ordinal) ||
                        !asset.TryGetValue("browser_download_url", out urlValue) ||
                        !asset.TryGetValue("digest", out digestValue))
                        continue;
                    downloadUrl = Convert.ToString(urlValue);
                    digest = Convert.ToString(digestValue);
                    break;
                }
                Uri assetUri;
                if (string.IsNullOrWhiteSpace(downloadUrl) ||
                    !Uri.TryCreate(downloadUrl, UriKind.Absolute, out assetUri) ||
                    assetUri.Scheme != Uri.UriSchemeHttps ||
                    !assetUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(digest) ||
                    !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Aset ZIP atau digest SHA-256 release tidak ditemukan.");

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Versi baru " + tag + " tersedia. Unduh dan pasang sekarang? WinIsle akan ditutup selama pembaruan.",
                        "Pembaruan WinIsle",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        DownloadAndInstallUpdate(downloadUrl, digest, latest);
                }));
            }
            catch (Exception ex)
            {
                LogUpdate("Pemeriksaan update gagal: " + ex);
                Prog.Log("Pemeriksaan update gagal: " + ex);
            }
        });
    }

    static string QuoteProcessArgument(string value)
    {
        StringBuilder quoted = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"')
            {
                quoted.Append('\\', backslashes * 2 + 1);
                quoted.Append('"');
            }
            else
            {
                quoted.Append('\\', backslashes);
                quoted.Append(c);
            }
            backslashes = 0;
        }
        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    static void LogUpdate(string message)
    {
        try
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinIsle", "Updates");
            Directory.CreateDirectory(logDirectory);
            lock (UpdateLogLock)
                File.AppendAllText(Path.Combine(logDirectory, "updater.log"),
                    DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Prog.Log("Gagal menulis log updater: " + ex);
        }
    }

    void DownloadAndInstallUpdate(string downloadUrl, string expectedDigest, Version version)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string updateDir = null;
            try
            {
                updateDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinIsle", "Updates", version.ToString(3) + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(updateDir);
                LogUpdate("Pembaruan " + version.ToString(3) + ": mulai mengunduh paket.");
                string zipPath = Path.Combine(updateDir, "WinIsle.zip");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(downloadUrl);
                request.UserAgent = "WinIsle";
                request.Timeout = 120000;
                request.ReadWriteTimeout = 120000;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += count;
                        if (total > 200L * 1024 * 1024)
                            throw new InvalidDataException("File update melebihi batas ukuran 200 MB.");
                        output.Write(buffer, 0, count);
                    }
                }
                LogUpdate("Pembaruan " + version.ToString(3) + ": unduhan selesai (" +
                    new FileInfo(zipPath).Length + " byte).");

                string actualDigest;
                using (SHA256 sha = SHA256.Create())
                using (FileStream input = File.OpenRead(zipPath))
                    actualDigest = "sha256:" + BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (!actualDigest.Equals(expectedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Verifikasi SHA-256 ZIP update gagal.");
                LogUpdate("Pembaruan " + version.ToString(3) + ": verifikasi SHA-256 berhasil.");

                string updaterPath = Path.Combine(updateDir, "WinIsleUpdater.ps1");
                using (FileStream input = File.OpenRead(zipPath))
                using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry updater = archive.GetEntry("WinIsleUpdater.ps1");
                    if (updater == null || updater.Length <= 0 || updater.Length > 256 * 1024)
                        throw new InvalidDataException("WinIsleUpdater.ps1 tidak ada di dalam ZIP atau ukurannya tidak valid.");
                    using (Stream source = updater.Open())
                    using (FileStream target = new FileStream(updaterPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        source.CopyTo(target);
                }

                string args = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " +
                    QuoteProcessArgument(updaterPath) + " -ZipPath " + QuoteProcessArgument(zipPath) +
                    " -InstallDirectory " + QuoteProcessArgument(AppDomain.CurrentDomain.BaseDirectory) +
                    " -ParentPid " + System.Diagnostics.Process.GetCurrentProcess().Id;
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = updateDir;
                using (System.Diagnostics.Process installer = System.Diagnostics.Process.Start(psi))
                {
                    if (installer == null) throw new InvalidOperationException("Proses updater tidak dapat dijalankan.");
                    LogUpdate("Pembaruan " + version.ToString(3) + ": helper updater dijalankan (PID " +
                        installer.Id + ").");
                }

                Dispatcher.BeginInvoke(new Action(delegate { ExitApp(); }));
            }
            catch (Exception ex)
            {
                LogUpdate("Pemasangan update gagal: " + ex);
                Prog.Log("Pemasangan update gagal: " + ex);
                if (updateDir != null)
                {
                    try { Directory.Delete(updateDir, true); }
                    catch (Exception cleanupError)
                    {
                        LogUpdate("Pembersihan file update gagal: " + cleanupError);
                        Prog.Log(cleanupError.ToString());
                    }
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    MessageBox.Show("Update tidak dapat dipasang. WinIsle tidak diubah.\n\n" + ex.Message,
                        "Pembaruan WinIsle", MessageBoxButton.OK, MessageBoxImage.Error);
                }));
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
