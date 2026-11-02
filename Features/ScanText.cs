using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

partial class Island
{
    UIElement BuildScanTextPage()
    {
        scanTextStatus = T("Pilih area layar. Hasil bisa dikoreksi sebelum disalin.", 0.65, 10.5);
        scanTextStatus.TextWrapping = TextWrapping.Wrap;
        scanTextStatus.TextAlignment = TextAlignment.Center;
        scanTextStatus.Margin = new Thickness(2, 4, 2, 4);

        btnScanText = SB("Pilih area layar", delegate { BeginTextScan(); });
        btnScanText.HorizontalAlignment = HorizontalAlignment.Center;

        scanTextOutput = new TextBox
        {
            Height = 108,
            Margin = new Thickness(2, 5, 2, 3),
            Padding = new Thickness(6),
            IsReadOnly = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Rgb(24, 24, 24),
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            BorderBrush = Rgb(60, 60, 60),
            BorderThickness = new Thickness(1),
            FontFamily = FF,
            FontSize = 11
        };
        scanTextOutput.ToolTip = "Klik hasil untuk mengoreksi teks sebelum disalin.";
        scanTextOutput.PreviewMouseLeftButtonDown += delegate
        {
            if (scanTextOutput.IsKeyboardFocusWithin) return;
            if (!editing) prevFg = GetForegroundWindow();
            SetActivatable(true);
            Activate();
            scanTextOutput.Focus();
        };
        scanTextOutput.GotKeyboardFocus += delegate { editing = true; };
        scanTextOutput.LostKeyboardFocus += delegate { editing = false; SetActivatable(false); };

        btnCopyScanText = SB("Salin hasil", delegate { CopyScannedText(); });
        btnCopyScanText.HorizontalAlignment = HorizontalAlignment.Center;
        btnCopyScanText.IsEnabled = false;

        return Col(scanTextStatus, btnScanText, scanTextOutput, btnCopyScanText);
    }

    void BeginTextScan()
    {
        if (scanTextTimer != null || !btnScanText.IsEnabled) return;
        scanTextReturnTab = currentTab == ScanTextTab ? lastNonScanTab : currentTab;
        string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scantext.ps1");
        if (!File.Exists(script))
        {
            scanTextStatus.Text = "scantext.ps1 tidak ditemukan di folder aplikasi.";
            Prog.Log("Scan Teks gagal: " + script + " tidak ditemukan.");
            return;
        }

        try
        {
            scanTextClipboardSequence = GetClipboardSequenceNumber();
            scanTextDeadline = DateTime.Now.AddSeconds(60);
            scanTextPickerSeen = false;
            scanTextPickerClosedAt = DateTime.MinValue;
            scanTextOutput.Clear();
            btnCopyScanText.IsEnabled = false;
            btnScanText.IsEnabled = false;
            scanTextStatus.Text = "Pilih area layar di Snipping Tool...";
            Process.Start(new ProcessStartInfo { FileName = "ms-screenclip:", UseShellExecute = true });

            scanTextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            scanTextTimer.Tick += delegate { CheckTextScanClipboard(); };
            scanTextTimer.Start();
        }
        catch (Exception ex)
        {
            FinishTextScan("Tidak dapat memulai pemilihan area: " + ex.Message);
            Prog.Log("Scan Teks gagal memulai pemilihan area: " + ex);
        }
    }

    void CheckTextScanClipboard()
    {
        if (GetClipboardSequenceNumber() != scanTextClipboardSequence)
        {
            string imagePath = null;
            try
            {
                if (Clipboard.ContainsImage())
                {
                    BitmapSource image = Clipboard.GetImage();
                    if (image != null)
                    {
                        imagePath = Path.Combine(Path.GetTempPath(), "WinIsle-ScanText-" + Guid.NewGuid().ToString("N") + ".png");
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        using (FileStream output = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            encoder.Save(output);

                        scanTextTimer.Stop();
                        scanTextTimer = null;
                        scanTextPickerSeen = false;
                        scanTextPickerClosedAt = DateTime.MinValue;
                        scanTextStatus.Text = "Mengenali teks secara lokal...";
                        RunTextOcr(imagePath);
                        return;
                    }
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard bisa dikunci sebentar oleh Snipping Tool; coba lagi pada tick berikutnya.
            }
            catch (Exception ex)
            {
                if (imagePath != null)
                    try { File.Delete(imagePath); } catch (Exception cleanupError) { Prog.Log("Gagal menghapus gambar Scan Teks: " + cleanupError); }
                FinishTextScan("Gagal membaca gambar dari clipboard: " + ex.Message);
                Prog.Log("Scan Teks gagal membaca gambar: " + ex);
                return;
            }
        }

        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
        {
            FinishTextScan("Pemindaian dibatalkan.");
            return;
        }
        if (IsScreenClipForeground())
        {
            scanTextPickerSeen = true;
            scanTextPickerClosedAt = DateTime.MinValue;
        }
        else if (scanTextPickerSeen)
        {
            if (scanTextPickerClosedAt == DateTime.MinValue)
                scanTextPickerClosedAt = DateTime.Now;
            else if ((DateTime.Now - scanTextPickerClosedAt).TotalMilliseconds >= ScanTextCancelGraceMilliseconds)
            {
                FinishTextScan("Pemilihan area ditutup. Pemindaian dibatalkan.");
                return;
            }
        }
        if (DateTime.Now >= scanTextDeadline)
        {
            FinishTextScan("Pemilihan area dibatalkan atau waktunya habis.");
        }
    }

    bool IsScreenClipForeground()
    {
        uint processId;
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || GetWindowThreadProcessId(foreground, out processId) == 0 || processId == 0)
            return false;

        try
        {
            using (Process process = Process.GetProcessById((int)processId))
            {
                return process.ProcessName.Equals("SnippingTool", StringComparison.OrdinalIgnoreCase) ||
                    process.ProcessName.Equals("ScreenClippingHost", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    void RunTextOcr(string imagePath)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string result = "";
            Exception failure = null;
            try
            {
                string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scantext.ps1");
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -ImagePath \"" + imagePath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                StringBuilder output = new StringBuilder(), error = new StringBuilder();
                using (Process process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (output) output.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (error) error.AppendLine(e.Data);
                    };
                    if (!process.Start()) throw new InvalidOperationException("PowerShell tidak dapat dijalankan.");
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(45000))
                    {
                        process.Kill();
                        throw new TimeoutException("Pengenalan teks melewati batas waktu 45 detik.");
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        string detail;
                        lock (error) detail = error.ToString().Trim();
                        throw new InvalidOperationException(detail.Length > 0 ? detail : "Windows OCR gagal.");
                    }
                    lock (output) result = output.ToString().TrimEnd('\r', '\n');
                }
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try { File.Delete(imagePath); }
                catch (Exception ex) { Prog.Log("Gagal menghapus gambar sementara Scan Teks: " + ex); }
            }

            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (failure != null)
                {
                    FinishTextScan("Pemindaian gagal: " + failure.Message);
                    Prog.Log("Scan Teks gagal: " + failure);
                    return;
                }
                scanTextOutput.Text = result;
                btnCopyScanText.IsEnabled = result.Trim().Length > 0;
                bool hasResult = result.Trim().Length > 0;
                FinishTextScan(hasResult ? "Selesai. Hasil diproses di perangkat ini." : "Tidak ada teks yang dikenali.");
                if (hasResult) ShowScanTextFeedback();
            }));
        });
    }

    void ShowScanTextFeedback()
    {
        scanTextReturnTab = currentTab == ScanTextTab ? scanTextReturnTab : currentTab;
        scanTextMiniMessage = "Teks sudah dipindai";
        scanTextAutoOpenPending = true;
        scanTextHoverArmed = false;
        scanTextRestorePending = true;
        scanTextRestoreOnClose = false;

        if (scanTextFeedbackTimer != null) scanTextFeedbackTimer.Stop();
        scanTextFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ScanTextFeedbackSeconds) };
        scanTextFeedbackTimer.Tick += delegate
        {
            scanTextFeedbackTimer.Stop();
            scanTextFeedbackTimer = null;
            scanTextMiniMessage = "";
            scanTextAutoOpenPending = false;
            scanTextHoverArmed = false;
            UpdateCollapsed();

            if (!scanTextRestorePending) return;
            if (currentTab != ScanTextTab)
            {
                scanTextRestorePending = false;
                return;
            }
            if (open) scanTextRestoreOnClose = true;
            else RestoreScanTextTab();
        };
        scanTextFeedbackTimer.Start();

        SwitchTo(ScanTextTab);
        SetOpen(false);
        UpdateCollapsed();
    }

    void RestoreScanTextTab()
    {
        scanTextRestoreOnClose = false;
        scanTextRestorePending = false;
        if (currentTab == ScanTextTab) SwitchTo(scanTextReturnTab);
    }

    void FinishTextScan(string message)
    {
        if (scanTextTimer != null)
        {
            scanTextTimer.Stop();
            scanTextTimer = null;
        }
        scanTextPickerSeen = false;
        scanTextPickerClosedAt = DateTime.MinValue;
        scanTextStatus.Text = message;
        btnScanText.IsEnabled = true;
    }

    void CopyScannedText()
    {
        if (string.IsNullOrWhiteSpace(scanTextOutput.Text)) return;
        try
        {
            Clipboard.SetText(scanTextOutput.Text);
            scanTextStatus.Text = "Hasil disalin ke clipboard.";
        }
        catch (Exception ex)
        {
            scanTextStatus.Text = "Tidak dapat menyalin hasil: " + ex.Message;
            Prog.Log("Gagal menyalin hasil Scan Teks: " + ex);
        }
    }
}
