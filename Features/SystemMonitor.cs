using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

partial class Island
{
    // =====================================================================
    //  SISTEM: jaringan, suhu, baterai
    // =====================================================================
    const double BatW = 31;   // lebar isi ikon baterai saat penuh
    TextBlock tNet, tNetLbl, batPct, batBolt;
    Border batFill;
    static readonly SolidColorBrush Green = new SolidColorBrush(Color.FromRgb(52, 199, 89)), Orange = new SolidColorBrush(Color.FromRgb(255, 179, 64));
    long netPrevRx = -1, netPrevTx;
    DateTime netPrevAt = DateTime.MinValue;
    double netDown, netUp;

    UIElement BuildBatteryCard()
    {
        batFill = new Border { Background = Green, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2), Width = 0 };
        batBolt = T("", 1, 10); batBolt.Foreground = Brushes.Black; batBolt.HorizontalAlignment = HorizontalAlignment.Center; batBolt.VerticalAlignment = VerticalAlignment.Center;
        Grid inner = new Grid(); inner.Children.Add(batFill); inner.Children.Add(batBolt);
        Border body = new Border { Width = 38, Height = 19, BorderBrush = Rgb(170, 170, 170), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(5), Child = inner };
        Border nub = new Border { Width = 3, Height = 8, Background = Rgb(170, 170, 170), CornerRadius = new CornerRadius(0, 2, 2, 0), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 0, 0, 0) };
        StackPanel ic = Row(body, nub); ic.VerticalAlignment = VerticalAlignment.Center; ic.Margin = new Thickness(0, 0, 10, 0);
        batPct = T("", 1, 18); batPct.FontWeight = FontWeights.SemiBold; batPct.VerticalAlignment = VerticalAlignment.Center;
        tBat = T("", 0.65, 10.5); tBat.MaxWidth = 90; tBat.Margin = new Thickness(8, 3, 0, 0); tBat.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Background = Rgb(26, 26, 26), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Child = Row(ic, batPct, tBat) };
    }

    static string Rate(double b)
    {
        if (b >= 1048576) return (b / 1048576).ToString("0.0", IC) + " MB/s";
        if (b >= 1024) return ((int)(b / 1024)) + " KB/s";
        return ((int)b) + " B/s";
    }

    // Kecepatan = selisih byte per detik. Hanya adaptor yang aktif dan punya gateway (menghindari hitung ganda adaptor virtual).
    void NetUpdate()
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;
                if (ni.GetIPProperties().GatewayAddresses.Count == 0) continue;
                System.Net.NetworkInformation.IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                rx += st.BytesReceived; tx += st.BytesSent;
            }
        }
        catch { }
        DateTime now = DateTime.Now; double dt = (now - netPrevAt).TotalSeconds;
        if (netPrevRx >= 0 && dt > 0.2 && dt < 5) { netDown = Math.Max(0, (rx - netPrevRx) / dt); netUp = Math.Max(0, (tx - netPrevTx) / dt); }
        else { netDown = 0; netUp = 0; }
        netPrevRx = rx; netPrevTx = tx; netPrevAt = now;
    }

    void UpdateNetBattery()
    {
        NetUpdate();
        tNet.Text = "\u2193 " + Rate(netDown) + "     \u2191 " + Rate(netUp);

        Forms.PowerStatus ps = Forms.SystemInformation.PowerStatus;
        if ((ps.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery) != 0)
        {
            batPct.FontSize = 13; batPct.Text = "Tanpa baterai"; tBat.Text = ""; batFill.Width = 0; batBolt.Text = "\U0001F50C";
            return;
        }
        float f = ps.BatteryLifePercent; if (f < 0 || f > 1) f = 0;
        int pc = (int)Math.Round(f * 100);
        bool plug = ps.PowerLineStatus == Forms.PowerLineStatus.Online;
        bool chg = (ps.BatteryChargeStatus & Forms.BatteryChargeStatus.Charging) != 0;
        batPct.FontSize = 18; batPct.Text = pc + "%";
        batFill.Width = BatW * f;
        batFill.Background = (chg || plug) ? Green : (pc <= 15 ? Reddish : (pc <= 30 ? Orange : Green));
        batBolt.Text = chg ? "\u26A1" : (plug ? "\U0001F50C" : "");
        int secs = ps.BatteryLifeRemaining;
        if (chg) tBat.Text = "Mengisi";
        else if (plug) tBat.Text = pc >= 100 ? "Penuh" : "Terhubung";
        else if (secs > 0) tBat.Text = "\u2248 " + (secs / 3600) + "j " + ((secs % 3600) / 60) + "m";
        else tBat.Text = "Baterai";
    }

    static ProgressBar NewBar()
    {
        return new ProgressBar { Height = 6, Maximum = 100, Margin = new Thickness(0, 3, 0, 6), Foreground = Accent, Background = Rgb(55, 55, 55), BorderThickness = new Thickness(0) };
    }


    // =====================================================================
    //  TIAP DETIK: jam, CPU, RAM, disk, baterai
    // =====================================================================
    void Sec()
    {
        RefreshClock();
        if (calToday != DateTime.Today) { calToday = DateTime.Today; calY = calToday.Year; calM = calToday.Month; calSel = null; BuildCal(); }

        long idle, kern, usr;
        if (GetSystemTimes(out idle, out kern, out usr))
        {
            long tot = kern + usr;
            if (prevTot > 0)
            {
                long dTot = tot - prevTot, dIdle = idle - prevIdle;
                if (dTot > 0) cpuPct = Math.Max(0, Math.Min(100, 100.0 * (dTot - dIdle) / dTot));
            }
            prevTot = tot; prevIdle = idle;
        }
        if (!open || currentTab != 6) return;

        pCpu.Value = cpuPct; tCpu.Text = "CPU  " + (int)Math.Round(cpuPct) + "%";
        MEM m = new MEM(); m.dwLength = (uint)Marshal.SizeOf(typeof(MEM));
        if (GlobalMemoryStatusEx(ref m))
        {
            double tot = m.total / 1073741824.0, used = (m.total - m.avail) / 1073741824.0;
            pRam.Value = m.dwMemoryLoad;
            tRam.Text = "RAM  " + m.dwMemoryLoad + "%   (" + used.ToString("0.0", IC) + " / " + tot.ToString("0.0", IC) + " GB)";
        }
        try
        {
            StringBuilder sbd = new StringBuilder(); int shown = 0;
            foreach (DriveInfo di in DriveInfo.GetDrives())
            {
                if (di.DriveType != DriveType.Fixed || !di.IsReady) continue;
                if (shown > 0) sbd.Append('\n');
                sbd.Append("Disk " + di.Name.TrimEnd('\\') + "  bebas " + (di.AvailableFreeSpace / 1073741824) + " / " + (di.TotalSize / 1073741824) + " GB");
                if (++shown >= 3) break;
            }
            tDisk.Text = sbd.ToString();
        }
        catch { }
        UpdateNetBattery();
    }


}
