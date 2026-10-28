// VorsWin Island - versi Windows dari ide Dynamic Island milik Vorssaint (GPL-3.0).
// C# 5 + WPF tanpa XAML. Dikompilasi dengan csc.exe bawaan Windows (lihat build.bat).
// Island menempel di tepi KANAN layar, di tengah secara vertikal.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Win32 = Microsoft.Win32;

static class Prog
{
    public static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), DateTime.Now.ToString("s") + " " + s + Environment.NewLine); }
        catch { }
    }

    [STAThread]
    static void Main()
    {
        bool created;
        System.Threading.Mutex m = new System.Threading.Mutex(true, "VorsWinIsland", out created);
        if (!created) return;
        Application app = new Application();
        app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
        {
            Log(e.Exception.ToString());
            e.Handled = true;
        };
        app.Run(new Island());
        GC.KeepAlive(m);
    }
}

// ---------- Windows Core Audio (COM) untuk mixer volume per aplikasi ----------
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IntPtr client);
    void UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    void OpenPropertyStore(int access, out IntPtr props);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out int state);
}

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionManager2
{
    void GetAudioSessionControl(ref Guid g, int flags, out IAudioSessionControl c);
    void GetSimpleAudioVolume(ref Guid g, int flags, out ISimpleAudioVolume v);
    void GetSessionEnumerator(out IAudioSessionEnumerator e);
    void RegisterSessionNotification(IntPtr n);
    void UnregisterSessionNotification(IntPtr n);
    void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr n);
    void UnregisterDuckNotification(IntPtr n);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionEnumerator
{
    void GetCount(out int n);
    void GetSession(int i, out IAudioSessionControl c);
}

[ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string n);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string n, ref Guid ctx);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string p);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string p, ref Guid ctx);
    void GetGroupingParam(out Guid g);
    void SetGroupingParam(ref Guid g, ref Guid ctx);
    void RegisterAudioSessionNotification(IntPtr n);
    void UnregisterAudioSessionNotification(IntPtr n);
}

[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl2
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string n);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string n, ref Guid ctx);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string p);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string p, ref Guid ctx);
    void GetGroupingParam(out Guid g);
    void SetGroupingParam(ref Guid g, ref Guid ctx);
    void RegisterAudioSessionNotification(IntPtr n);
    void UnregisterAudioSessionNotification(IntPtr n);
    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetProcessId(out uint pid);
    [PreserveSig] int IsSystemSoundsSession();
    void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool opt);
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISimpleAudioVolume
{
    void SetMasterVolume(float level, ref Guid ctx);
    void GetMasterVolume(out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr n);
    void UnregisterControlChangeNotify(IntPtr n);
    void GetChannelCount(out uint c);
    void SetMasterVolumeLevel(float level, ref Guid ctx);
    void SetMasterVolumeLevelScalar(float level, ref Guid ctx);
    void GetMasterVolumeLevel(out float level);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
    void SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
    void GetChannelVolumeLevel(uint ch, out float level);
    void GetChannelVolumeLevelScalar(uint ch, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

class Island : Window
{
    // Ukuran (dalam DIP). C* = ciut (mengecil), O* = open (membesar), W* = ukuran window transparan.
    // CH = tinggi pil saat tidak ada lagu, CHT = tinggi pil saat lagu diputar (ada judul vertikal), TH = tinggi area judul.
    const double CW = 36, CH = 84, CHT = 232, TH = 140, OW = 240, OH = 272, WW = 262, WH = 296;
    const double TitleSpeed = 22;   // kecepatan judul bergerak (piksel per detik), makin kecil makin lambat
    const bool TitleUp = false;     // true = judul bergerak dari bawah ke atas, false = dari atas ke bawah
    const double TitleDelay = 2;    // jeda (detik) sebelum judul mulai bergerak setelah lagu/media berganti
    const double TitleGap = 28;     // jarak (piksel) antara akhir judul dan awal judul berikutnya
    const int MaxClip = 8;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // ---------- Win32 ----------
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PT p);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint m, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr h);
    [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, uint fl, UIntPtr ex);
    [DllImport("user32.dll")] static extern bool LockWorkStation();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint f);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEM m);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    struct PT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    struct MEM { public uint dwLength, dwMemoryLoad; public ulong total, avail, pageTotal, pageAvail, virtTotal, virtAvail, virtExt; }

    // ---------- Tipe bantu ----------
    class LL { public double t; public string s, r; }   // s = teks asli, r = versi latin
    class LRes { public string synced = "", plain = ""; public bool instr, err; }

    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly Regex Ts = new Regex(@"\[(\d+):(\d+(?:\.\d+)?)\]");
    static readonly Regex Wt = new Regex(@"<\d+:\d+(?:\.\d+)?>");
    static readonly FontFamily FF = new FontFamily("Segoe UI, Segoe UI Symbol, Segoe UI Emoji");
    static readonly SolidColorBrush Accent = new SolidColorBrush(Color.FromRgb(30, 144, 255));
    static readonly SolidColorBrush Reddish = new SolidColorBrush(Color.FromRgb(255, 105, 97));

    // ---------- UI ----------
    Border isl, seekFill, titleHost;
    Canvas titleCanvas;
    Grid miniRoot, expRoot, seekBar;
    UniformGrid calGrid;
    UIElement[] pages = new UIElement[8];
    Border[] dots = new Border[8];
    ScrollViewer clipScroll;
    Button btnPlay, btnAwake, btnAuto, calTitleBtn;
    TextBlock clockFull, tabTitle, topClock, botClock, titleText;
    TextBlock tTitle, tArtist, tPos, tDur, lStat, lOff, lPlainT, lHint, tCpu, tRam, tDisk, tBat;
    TextBlock[] ly = new TextBlock[5];
    StackPanel lSync, clipList;
    ScrollViewer lPlain;
    ProgressBar pCpu, pRam;
    readonly string[] tabNames = { "\u266A Musik", "\U0001F3A4 Lirik", "\u23F1 Timer", "\U0001F50A Suara", "\U0001F4C5 Kalender", "\U0001F4CB Clipboard", "\U0001F4CA Sistem", "\u2699 Kontrol" };

    // ---------- State ----------
    List<LL> lines = new List<LL>();
    Dictionary<string, double> offsets = new Dictionary<string, double>();
    Dictionary<string, LRes> cache = new Dictionary<string, LRes>();
    string lkey = "";
    // Lirik dari caption YouTube (dikirim ekstensi browser lewat localhost)
    class YtL { public string n, lrc; public double dur; public DateTime at; }
    List<YtL> ytStore = new List<YtL>();
    HashSet<string> ytDone = new HashSet<string>();
    string curTi = "";
    const int YtPort = 47834;
    bool lsynced, playingNow, open, back, awake, isDragging, quitting, clipDirty = true;
    double basePos = -1, offset, curDur, dragRatio, cpuPct, collH = CH, wheelAcc;
    string titleShown = "";
    bool titleOn;
    DateTime lastWheel = DateTime.MinValue, lastWheelEvt = DateTime.MinValue;
    DateTime baseAt = DateTime.Now, seekHoldUntil = DateTime.MinValue, outSince = DateTime.MinValue;
    DateTime alertUntil = DateTime.MinValue, endT = DateTime.MinValue, calToday = DateTime.Today, mediaStarted = DateTime.MinValue;
    int curIdx = -2, currentTab, calY, calM, clipTries, mediaFails;
    long prevTot, prevIdle;
    List<string> hist = new List<string>();
    IntPtr hwnd;
    Forms.NotifyIcon tray;
    Process mediaProc;
    TranslateTransform lyTr = new TranslateTransform();

    // --- Lirik latin (romaji / romanisasi Korea) & pop-out ---
    Button btnRom, btnPop;
    int romKind;                       // 0 = tidak perlu, 1 = Jepang, 2 = Korea
    bool romOn, popOn;                 // romOn = pilihan pengguna, diingat selama aplikasi berjalan
    string plainSrc = "", plainRom = "";
    int popIdx = -2;
    PopLyric pop;
    TextBlock dateFull;
    const string AlarmMsg = "Timer telah selesai";
    bool tPaused, editing; TimeSpan tLeft; volatile bool alarmOn;
    Button btnPause, btnStopAlarm; TextBox tTimer;
    string mediaColl = "", dig = "", cdShown = ""; IntPtr prevFg;
    System.Windows.Documents.Run rTitle, rCd, rTitle2, rCd2;
    TextBlock titleText2;
    CultureInfo idCulture;

    // ---------- Pembuat kontrol ----------
    static ControlTemplate btnTpl;
    static ControlTemplate BtnTemplate()
    {
        if (btnTpl == null)
            btnTpl = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
                "<Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='10' Padding='{TemplateBinding Padding}'>" +
                "<ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.75'/></Trigger>" +
                "<Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.5'/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate>");
        return btnTpl;
    }

    static SolidColorBrush Rgb(byte r, byte g, byte b) { return new SolidColorBrush(Color.FromRgb(r, g, b)); }

    static TextBlock T(string s, double op, double sz)
    {
        return new TextBlock { Text = s, Foreground = Brushes.White, Opacity = op, FontSize = sz, FontFamily = FF, TextTrimming = TextTrimming.CharacterEllipsis };
    }

    static Button B(string s, Action a)
    {
        Button b = new Button
        {
            Content = s, Template = BtnTemplate(), Margin = new Thickness(2), Padding = new Thickness(10, 5, 10, 5),
            Background = Rgb(38, 38, 38), Foreground = Brushes.White, Focusable = false, Cursor = Cursors.Hand, FontFamily = FF, FontSize = 13
        };
        b.Click += delegate { a(); };
        return b;
    }

    // Tombol versi ringkas
    static Button SB(string s, Action a)
    {
        Button b = B(s, a);
        b.Padding = new Thickness(8, 3, 8, 3); b.FontSize = 12; b.Margin = new Thickness(2, 1, 2, 1);
        return b;
    }

    static StackPanel Col(params UIElement[] c)
    {
        StackPanel p = new StackPanel();
        foreach (UIElement e in c) p.Children.Add(e);
        return p;
    }

    static StackPanel Row(params UIElement[] c)
    {
        StackPanel p = Col(c);
        p.Orientation = Orientation.Horizontal;
        return p;
    }

    static void PressKey(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }

    static string Str(Dictionary<string, object> d, string k)
    {
        object v;
        return (d != null && d.TryGetValue(k, out v) && v != null) ? Convert.ToString(v, IC) : "";
    }

    static string Fmt(double s)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, s));
        if (t.TotalHours >= 1) return ((int)t.TotalHours) + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        return ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00");
    }

    // =====================================================================
    //  KONSTRUKTOR
    // =====================================================================
    public Island()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = WW; Height = WH; Title = "VorsWin Island";
        Reposition();
        try { idCulture = new CultureInfo("id-ID"); } catch { idCulture = CultureInfo.CurrentCulture; }
        calY = DateTime.Today.Year; calM = DateTime.Today.Month;

        AutoStartInit();
        StartYtServer();
        BuildMini();
        BuildPages();
        BuildExpanded();

        Grid root = new Grid();
        root.Children.Add(miniRoot);
        root.Children.Add(expRoot);
        isl = new Border
        {
            Background = Brushes.Black, CornerRadius = new CornerRadius(26, 0, 0, 26), Width = CW, Height = CH,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true, Child = root
        };
        Content = isl;
        isl.PreviewMouseWheel += Wheel;
        isl.PreviewMouseDown += delegate { if (editing && !tTimer.IsMouseOver) EndEdit(true); };
        Deactivated += delegate { if (editing) EndEdit(false); };

        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Visible = true, Text = "VorsWin Island" };
        Forms.ContextMenuStrip cm = new Forms.ContextMenuStrip();
        cm.Items.Add("Ke belakang / ke depan (Ctrl+Alt+I)", null, delegate { ToggleBack(); });
        cm.Items.Add("Keluar", null, delegate { ExitApp(); });
        tray.ContextMenuStrip = cm;
        tray.MouseClick += delegate(object s, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) ToggleBack(); };

        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { Reposition(); };

        Tick(80, Hover);
        Tick(1000, Sec);
        Tick(120, LyricTick);
        Tick(250, ProgressTick);
        Tick(250, TimerTick);
        Tick(250, ClipTick);
        Tick(500, MixTick);
        Tick(1500, KeepOnTop);

        BuildCal();
        RenderClip();
        UpdateTabs();
        RefreshClock();
        StartMedia();
    }

    void Reposition()
    {
        Rect wa = SystemParameters.WorkArea;
        double l = wa.Right - WW, t = wa.Top + (wa.Height - WH) / 2;
        if (Left != l) Left = l;
        if (Top != t) Top = t;
    }

    void KeepOnTop()
    {
        Reposition();
        if (!back && hwnd != IntPtr.Zero) SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13);
        if (popOn && pop != null) { pop.Place(); pop.Raise(); }
    }

    // ---------- Tampilan ciut: jam vertikal + judul lagu vertikal ----------
    void BuildMini()
    {
        topClock = T("00", 1, 15); botClock = T("00", 1, 15);
        topClock.FontWeight = FontWeights.SemiBold; botClock.FontWeight = FontWeights.SemiBold;
        topClock.HorizontalAlignment = HorizontalAlignment.Center; botClock.HorizontalAlignment = HorizontalAlignment.Center;
        Border sep = new Border { Height = 2, Width = 14, Background = Brushes.White, Opacity = 0.5, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 3, 0, 3) };
        StackPanel st = Col(topClock, sep, botClock);
        st.VerticalAlignment = VerticalAlignment.Top;
        st.Margin = new Thickness(0, 18, 3, 0);

        // Judul lagu: teks diputar -90 derajat (dibaca dari bawah ke atas), dipotong di area setinggi TH.
        titleText = T("", 1, 12);
        titleText.TextTrimming = TextTrimming.None;
        titleText.LayoutTransform = new RotateTransform(-90);
        titleText.Inlines.Clear();
        rTitle = new System.Windows.Documents.Run(); rCd = new System.Windows.Documents.Run();
        titleText.Inlines.Add(rTitle); titleText.Inlines.Add(rCd);
        titleCanvas = new Canvas { Width = CW - 3, Height = TH };
        titleCanvas.Children.Add(titleText);
        titleText2 = T("", 1, 12);
        titleText2.TextTrimming = TextTrimming.None;
        titleText2.LayoutTransform = new RotateTransform(-90);
        titleText2.Inlines.Clear();
        rTitle2 = new System.Windows.Documents.Run(); rCd2 = new System.Windows.Documents.Run();
        titleText2.Inlines.Add(rTitle2); titleText2.Inlines.Add(rCd2);
        titleText2.Visibility = Visibility.Collapsed;
        titleCanvas.Children.Add(titleText2);
        titleHost = new Border
        {
            Width = CW - 3, Height = TH, Margin = new Thickness(0, 76, 0, 0), VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true, Child = titleCanvas, Visibility = Visibility.Collapsed
        };

        miniRoot = new Grid { Width = CW, HorizontalAlignment = HorizontalAlignment.Right };
        miniRoot.Children.Add(st);
        miniRoot.Children.Add(titleHost);
    }

    static DoubleAnimation Marquee(double from, double to, TimeSpan du)
    {
        return new DoubleAnimation(from, to, du) { BeginTime = TimeSpan.FromSeconds(TitleDelay), RepeatBehavior = RepeatBehavior.Forever };
    }

    // Judul panjang: langsung tampil dari awal teks, diam TitleDelay detik, lalu bergerak terus.
    // Ada dua salinan teks (diberi jarak TitleGap) sehingga begitu satu selesai, yang berikutnya sudah menyambung.
    void SetupTitle(string bt, bool hasCd)
    {
        titleText.BeginAnimation(Canvas.TopProperty, null);
        titleText2.BeginAnimation(Canvas.TopProperty, null);
        string c = hasCd ? (bt.Length > 0 ? "   " : "") + "\u23F1 00:00:00" : "";
        rTitle.Text = bt; rCd.Text = c; rTitle2.Text = bt; rCd2.Text = c;
        titleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double len = titleText.DesiredSize.Height, thick = titleText.DesiredSize.Width;
        double left = Math.Max(0, (CW - 3 - thick) / 2);
        Canvas.SetLeft(titleText, left); Canvas.SetLeft(titleText2, left);
        if (len <= TH - 6)
        {
            Canvas.SetTop(titleText, (TH - len) / 2); // muat: diam di tengah
            titleText2.Visibility = Visibility.Collapsed;
            return;
        }
        double p = len + TitleGap;                 // jarak satu putaran
        double top0 = TitleUp ? 0 : TH - len;      // posisi awal: bagian awal judul sudah terlihat
        double d = TitleUp ? -p : p;
        titleText2.Visibility = Visibility.Visible;
        Canvas.SetTop(titleText, top0); Canvas.SetTop(titleText2, top0 - d);
        TimeSpan du = TimeSpan.FromSeconds(p / TitleSpeed);
        titleText.BeginAnimation(Canvas.TopProperty, Marquee(top0, top0 + d, du));
        titleText2.BeginAnimation(Canvas.TopProperty, Marquee(top0 - d, top0, du));
    }

    // Teks pil ciut: judul lagu (atau pesan alarm) + hitung mundur timer kalau ada.
    void UpdateCollapsed()
    {
        string bt = alarmOn ? AlarmMsg : mediaColl;
        string cd = alarmOn ? "" : CountdownText();
        bool hasCd = cd.Length > 0;
        string key = bt + "\u0001" + (hasCd ? "1" : "0");
        if (key != titleShown) { titleShown = key; cdShown = ""; if (bt.Length > 0 || hasCd) SetupTitle(bt, hasCd); }
        if (hasCd && cd != cdShown) { cdShown = cd; rCd.Text = (bt.Length > 0 ? "   " : "") + (tPaused ? "\u23F8 " : "\u23F1 ") + cd; rCd2.Text = rCd.Text; }
        bool show = bt.Length > 0 || hasCd;
        if (show == titleOn) return;
        titleOn = show;
        collH = show ? CHT : CH;
        titleHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!open)
            isl.BeginAnimation(HeightProperty, new DoubleAnimation(collH, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // ---------- Tujuh halaman (ukuran ringkas) ----------
    void BuildPages()
    {
        // 0: Musik
        tTitle = T("Tidak ada media", 1, 14); tTitle.FontWeight = FontWeights.SemiBold; tTitle.TextWrapping = TextWrapping.Wrap;
        tTitle.TextTrimming = TextTrimming.None; tTitle.TextAlignment = TextAlignment.Center; tTitle.MaxHeight = 40; tTitle.Margin = new Thickness(0, 0, 0, 2);
        tArtist = T("", 0.6, 11); tArtist.TextAlignment = TextAlignment.Center; tArtist.Margin = new Thickness(0, 0, 0, 10);
        seekBar = new Grid { Height = 16, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        Border seekTrack = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Rgb(70, 70, 70), VerticalAlignment = VerticalAlignment.Center };
        seekFill = new Border { Height = 4, Width = 0, CornerRadius = new CornerRadius(2), Background = Accent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        seekBar.Children.Add(seekTrack); seekBar.Children.Add(seekFill);
        seekBar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
        {
            if (curDur <= 0) return;
            isDragging = true; seekBar.CaptureMouse(); DragTo(e.GetPosition(seekBar).X);
        };
        seekBar.MouseMove += delegate(object s, MouseEventArgs e) { if (isDragging) DragTo(e.GetPosition(seekBar).X); };
        seekBar.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
        {
            if (!isDragging) return;
            isDragging = false; seekBar.ReleaseMouseCapture(); SeekTo(dragRatio * curDur);
        };
        tPos = T("0:00", 0.5, 10); tDur = T("0:00", 0.5, 10);
        tPos.HorizontalAlignment = HorizontalAlignment.Left; tDur.HorizontalAlignment = HorizontalAlignment.Right;
        Grid timeGrid = new Grid(); timeGrid.Children.Add(tPos); timeGrid.Children.Add(tDur);
        btnPlay = SB("\u25B6", delegate { PressKey(0xB3); playingNow = !playingNow; btnPlay.Content = playingNow ? "\u23F8" : "\u25B6"; baseAt = DateTime.Now; });
        btnPlay.Padding = new Thickness(16, 3, 16, 3);
        StackPanel ctrl = Row(SB("\u23EE", delegate { PressKey(0xB1); }), btnPlay, SB("\u23ED", delegate { PressKey(0xB0); }));
        ctrl.HorizontalAlignment = HorizontalAlignment.Center; ctrl.Margin = new Thickness(0, 10, 0, 0);
        pages[0] = Col(tTitle, tArtist, seekBar, timeGrid, ctrl);
        ((StackPanel)pages[0]).Margin = new Thickness(0, 12, 0, 0);

        // 1: Lirik
        for (int i = 0; i < 5; i++)
        {
            ly[i] = T("", i == 2 ? 1 : 0.4, i == 2 ? 13 : 10.5);
            ly[i].HorizontalAlignment = HorizontalAlignment.Center; ly[i].Margin = new Thickness(0, 2, 0, 2);
            ly[i].TextWrapping = TextWrapping.Wrap; ly[i].TextTrimming = TextTrimming.None; ly[i].TextAlignment = TextAlignment.Center;
            if (i == 2) ly[i].FontWeight = FontWeights.SemiBold;
        }
        lSync = Col(ly); lSync.VerticalAlignment = VerticalAlignment.Center; lSync.Visibility = Visibility.Collapsed; lSync.RenderTransform = lyTr;
        lStat = T("Putar lagu untuk menampilkan lirik", 0.6, 12); lStat.HorizontalAlignment = HorizontalAlignment.Center; lStat.VerticalAlignment = VerticalAlignment.Center;
        lStat.TextWrapping = TextWrapping.Wrap; lStat.TextAlignment = TextAlignment.Center; lStat.TextTrimming = TextTrimming.None;
        lPlainT = T("", 0.85, 11.5); lPlainT.TextWrapping = TextWrapping.Wrap; lPlainT.TextAlignment = TextAlignment.Center; lPlainT.TextTrimming = TextTrimming.None;
        lPlain = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = lPlainT, Visibility = Visibility.Collapsed };
        Grid lyricArea = new Grid { Height = 126, ClipToBounds = true };
        lyricArea.Children.Add(lStat); lyricArea.Children.Add(lSync); lyricArea.Children.Add(lPlain);
        lOff = T("0.0s", 0.7, 11); lOff.VerticalAlignment = VerticalAlignment.Center; lOff.TextAlignment = TextAlignment.Center; lOff.MinWidth = 36;
        btnRom = SB("Aa", delegate { ToggleRom(); }); btnRom.ToolTip = "Lirik latin (romaji / romanisasi)"; btnRom.Visibility = Visibility.Collapsed;
        btnPop = SB("\u2750", delegate { TogglePop(); }); btnPop.ToolTip = "Pop out: lirik melayang di dekat bawah layar";
        StackPanel offRow = Row(btnRom, SB("\u23EA", delegate { SetOffset(offset - 0.5); }), lOff, SB("\u23E9", delegate { SetOffset(offset + 0.5); }), btnPop);
        offRow.HorizontalAlignment = HorizontalAlignment.Center; offRow.Margin = new Thickness(0, 4, 0, 0);
        lHint = T("", 0.4, 9.5); lHint.TextWrapping = TextWrapping.Wrap; lHint.TextAlignment = TextAlignment.Center; lHint.TextTrimming = TextTrimming.None;
        pages[1] = Col(lyricArea, offRow, lHint);

        // 4: Kalender
        calGrid = new UniformGrid { Columns = 7, Rows = 7, Height = 132 };
        calTitleBtn = SB("", delegate { calY = DateTime.Today.Year; calM = DateTime.Today.Month; BuildCal(); });
        calTitleBtn.Background = Brushes.Transparent; calTitleBtn.FontWeight = FontWeights.SemiBold; calTitleBtn.ToolTip = "Kembali ke bulan ini";
        Button prev = SB("\u25C0", delegate { MoveMonth(-1); });
        Button next = SB("\u25B6", delegate { MoveMonth(1); });
        Grid calNav = new Grid { Margin = new Thickness(0, 4, 0, 6) };
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(prev, 0); Grid.SetColumn(calTitleBtn, 1); Grid.SetColumn(next, 2);
        calNav.Children.Add(prev); calNav.Children.Add(calTitleBtn); calNav.Children.Add(next);
        pages[4] = Col(calNav, calGrid);

        // 2: Timer
        tTimer = new TextBox
        {
            Text = "00:00:00", FontSize = 34, FontFamily = FF, Foreground = Brushes.White, Background = Brushes.Transparent,
            CaretBrush = Brushes.Transparent, SelectionBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10), AllowDrop = false, IsUndoEnabled = false
        };
        tTimer.ContextMenu = null;
        tTimer.PreviewMouseLeftButtonDown += delegate
        {
            if (!CanEdit() || editing) return;
            prevFg = GetForegroundWindow();
            SetActivatable(true); Activate(); tTimer.Focus();
        };
        tTimer.GotKeyboardFocus += delegate { editing = true; tTimer.Foreground = Accent; };
        tTimer.LostKeyboardFocus += delegate { editing = false; tTimer.Foreground = Brushes.White; SetActivatable(false); };
        tTimer.PreviewKeyDown += delegate(object s, KeyEventArgs e)
        {
            e.Handled = true;
            if (!CanEdit()) return;
            Key kk = e.Key;
            bool plain = (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt)) == 0;
            if (plain && kk >= Key.D0 && kk <= Key.D9) AddDigit(kk - Key.D0);
            else if (plain && kk >= Key.NumPad0 && kk <= Key.NumPad9) AddDigit(kk - Key.NumPad0);
            else if (kk == Key.Back) { if (dig.Length > 0) { dig = dig.Substring(0, dig.Length - 1); ShowIdle(); } }
            else if (kk == Key.Delete) { dig = ""; ShowIdle(); }
            else if (kk == Key.Enter) StartPause();
            else if (kk == Key.Escape) ReleaseEdit();
            else if (!IsMod(kk)) EndEdit(true);
        };
        StackPanel presets = Row(SB("5m", delegate { StartTimer(5); }), SB("10m", delegate { StartTimer(10); }), SB("15m", delegate { StartTimer(15); }), SB("25m", delegate { StartTimer(25); }));
        presets.HorizontalAlignment = HorizontalAlignment.Center;
        btnPause = SB("\u25B6 Mulai", delegate { StartPause(); });
        StackPanel tctl = Row(btnPause, SB("+1m", delegate { AddTimer(1); }), SB("Batal", delegate { CancelTimer(); }));
        tctl.HorizontalAlignment = HorizontalAlignment.Center; tctl.Margin = new Thickness(0, 6, 0, 0);
        btnStopAlarm = SB("\U0001F515 Matikan alarm", delegate { StopAlarm(); });
        btnStopAlarm.Background = Accent; btnStopAlarm.HorizontalAlignment = HorizontalAlignment.Center; btnStopAlarm.Margin = new Thickness(0, 6, 0, 0);
        btnStopAlarm.Visibility = Visibility.Collapsed;
        pages[2] = Col(tTimer, presets, tctl, btnStopAlarm);

        // 5: Clipboard
        clipList = new StackPanel();
        clipScroll = new ScrollViewer { Height = 124, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = clipList };
        TextBlock clipHint = T("Klik item untuk menyalin ulang", 0.5, 10.5); clipHint.Margin = new Thickness(0, 4, 0, 4); clipHint.HorizontalAlignment = HorizontalAlignment.Center;
        Button clipClear = SB("Hapus riwayat", delegate { hist.Clear(); RenderClip(); });
        clipClear.Margin = new Thickness(0, 6, 0, 0);
        pages[5] = Col(clipHint, clipScroll, clipClear);

        // 6: Sistem
        tCpu = T("CPU", 1, 12); tRam = T("RAM", 1, 12); tDisk = T("", 0.7, 10.5);
        pCpu = NewBar(); pRam = NewBar();
        tNetLbl = T("Internet (\u2193 unduh, \u2191 unggah)", 0.6, 10.5); tNetLbl.Margin = new Thickness(0, 4, 0, 1);
        tNet = T("", 0.9, 12);
        tNet.Margin = new Thickness(0, 0, 0, 4); tDisk.Margin = new Thickness(0, 0, 0, 8);
        StackPanel sys = Col(tCpu, pCpu, tRam, pRam, tNetLbl, tNet, tDisk, BuildBatteryCard());
        sys.Margin = new Thickness(0, 4, 0, 0);
        pages[6] = sys;

        // 7: Kontrol
        btnAwake = SB("\u2615 Keep awake: mati", delegate { ToggleAwake(); });
        btnAuto = SB("", delegate { ToggleAutostart(); });
        RefreshAutoBtn();
        StackPanel volRow = Row(SB("\U0001F509 \u2212", delegate { PressKey(0xAE); }), SB("\U0001F507", delegate { PressKey(0xAD); }), SB("\U0001F50A +", delegate { PressKey(0xAF); }));
        volRow.HorizontalAlignment = HorizontalAlignment.Center;
        StackPanel ctl = Col(
            btnAwake,
            SB("\U0001F512 Kunci layar", delegate { LockWorkStation(); }),
            SB("\u2B07 Ke belakang", delegate { ToggleBack(); }),
            btnAuto,
            volRow,
            SB("\u2716 Keluar", delegate { ExitApp(); }));
        ctl.Margin = new Thickness(0, 2, 0, 0);
        pages[7] = ctl;

        // 3: Suara (mixer volume per aplikasi)
        pages[3] = BuildMixerPage();
    }

    // =====================================================================
    //  SUARA: mixer volume per aplikasi (Windows Core Audio)
    //  Satu bar per aplikasi yang punya sesi audio. Geser = atur volume, klik ikon = mute.
    // =====================================================================
    class MixRow
    {
        public string Key, Name;
        public bool Muted, Dragging, IsMaster;
        public double Val = -1;
        public List<ISimpleAudioVolume> Vols = new List<ISimpleAudioVolume>();
        public IAudioEndpointVolume Master;
        public Grid Root, Bar;
        public Button Icon;
        public Border Fill, Thumb;
        public TextBlock Pct;
    }

    Dictionary<string, MixRow> mixRows = new Dictionary<string, MixRow>();
    MixRow masterRow;
    StackPanel mixList;
    TextBlock mixEmpty;
    ScrollViewer mixScroll;
    Guid mixCtx = Guid.Empty;

    UIElement BuildMixerPage()
    {
        masterRow = MakeRow("master", "Volume utama", null, "\U0001F50A");
        masterRow.IsMaster = true;
        mixList = new StackPanel();
        mixEmpty = T("Belum ada aplikasi yang mengeluarkan suara", 0.5, 10.5);
        mixEmpty.TextWrapping = TextWrapping.Wrap; mixEmpty.TextAlignment = TextAlignment.Center; mixEmpty.Margin = new Thickness(0, 14, 0, 0);
        mixList.Children.Add(mixEmpty);
        mixScroll = new ScrollViewer { Height = 116, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = mixList };
        TextBlock hint = T("Geser untuk atur volume \u2022 klik ikon untuk mute", 0.5, 10);
        hint.HorizontalAlignment = HorizontalAlignment.Center; hint.Margin = new Thickness(0, 0, 0, 3);
        Border sep = new Border { Height = 1, Background = Rgb(50, 50, 50), Margin = new Thickness(0, 3, 0, 3) };
        return Col(hint, masterRow.Root, sep, mixScroll);
    }

    MixRow MakeRow(string key, string name, ImageSource icon, string glyph)
    {
        MixRow r = new MixRow { Key = key, Name = name };
        r.Icon = new Button
        {
            Width = 26, Height = 26, Padding = new Thickness(2), Background = Brushes.Transparent, Foreground = Brushes.White,
            Focusable = false, Cursor = Cursors.Hand, ToolTip = name, Template = BtnTemplate(), FontFamily = FF, FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (icon != null) r.Icon.Content = new Image { Source = icon, Width = 18, Height = 18 };
        else r.Icon.Content = glyph;
        r.Icon.Click += delegate { SetRowMute(r, !r.Muted); };

        Border track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Rgb(58, 58, 58), VerticalAlignment = VerticalAlignment.Center };
        r.Fill = new Border { Height = 4, Width = 0, CornerRadius = new CornerRadius(2), Background = Accent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        r.Thumb = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-5, 0, 0, 0) };
        r.Bar = new Grid { Height = 22, Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 4, 0) };
        r.Bar.Children.Add(track); r.Bar.Children.Add(r.Fill); r.Bar.Children.Add(r.Thumb);
        r.Bar.SizeChanged += delegate { ShowRow(r); };
        r.Bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
        {
            r.Dragging = true; isDragging = true; r.Bar.CaptureMouse(); MixDrag(r, e);
        };
        r.Bar.MouseMove += delegate(object s, MouseEventArgs e) { if (r.Dragging) MixDrag(r, e); };
        r.Bar.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
        {
            if (!r.Dragging) return;
            MixDrag(r, e); r.Dragging = false; isDragging = false; r.Bar.ReleaseMouseCapture();
        };

        r.Pct = T("0", 0.8, 11); r.Pct.Width = 30; r.Pct.TextAlignment = TextAlignment.Right; r.Pct.VerticalAlignment = VerticalAlignment.Center;
        r.Root = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        r.Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        r.Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        r.Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(r.Bar, 1); Grid.SetColumn(r.Pct, 2);
        r.Root.Children.Add(r.Icon); r.Root.Children.Add(r.Bar); r.Root.Children.Add(r.Pct);
        return r;
    }

    void ShowRow(MixRow r)
    {
        double w = r.Bar.ActualWidth, v = Math.Max(0, Math.Min(1, r.Val < 0 ? 0 : r.Val));
        r.Fill.Width = v * w; r.Thumb.Margin = new Thickness(v * w - 5, 0, 0, 0);
        r.Fill.Background = r.Muted ? Rgb(110, 110, 110) : Accent;
        r.Pct.Text = r.Muted ? "\U0001F507" : ((int)Math.Round(v * 100)).ToString();
        r.Icon.Opacity = r.Muted ? 0.35 : 1;
    }

    void MixDrag(MixRow r, MouseEventArgs e)
    {
        double w = r.Bar.ActualWidth;
        if (w <= 0) return;
        SetRowVol(r, Math.Max(0, Math.Min(1, e.GetPosition(r.Bar).X / w)));
    }

    void SetRowVol(MixRow r, double v)
    {
        try
        {
            if (r.IsMaster) { if (r.Master != null) r.Master.SetMasterVolumeLevelScalar((float)v, ref mixCtx); }
            else foreach (ISimpleAudioVolume sv in r.Vols) { try { sv.SetMasterVolume((float)v, ref mixCtx); } catch { } }
        }
        catch { }
        r.Val = v;
        if (r.Muted && v > 0) SetRowMute(r, false); else ShowRow(r);
    }

    void SetRowMute(MixRow r, bool m)
    {
        try
        {
            if (r.IsMaster) { if (r.Master != null) r.Master.SetMute(m, ref mixCtx); }
            else foreach (ISimpleAudioVolume sv in r.Vols) { try { sv.SetMute(m, ref mixCtx); } catch { } }
        }
        catch { }
        r.Muted = m; ShowRow(r);
    }

    static ImageSource IconOf(string path)
    {
        try
        {
            using (System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
            {
                if (ic == null) return null;
                System.Windows.Media.Imaging.BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ic.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
        }
        catch { return null; }
    }

    void RebuildMixList()
    {
        List<MixRow> l = new List<MixRow>(mixRows.Values);
        l.Sort(delegate(MixRow a, MixRow b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
        mixList.Children.Clear();
        if (l.Count == 0) mixList.Children.Add(mixEmpty);
        foreach (MixRow r in l) mixList.Children.Add(r.Root);
    }

    // Dibaca ulang tiap 0,5 detik selama tab Suara terbuka, jadi perubahan dari luar (mis. tombol volume keyboard) ikut tampil.
    void MixTick()
    {
        if (!open || currentTab != 3) return;
        try
        {
            IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice dev; en.GetDefaultAudioEndpoint(0, 1, out dev);   // 0 = output, 1 = multimedia
            object o;
            Guid gv = typeof(IAudioEndpointVolume).GUID;
            dev.Activate(ref gv, 23, IntPtr.Zero, out o);
            masterRow.Master = (IAudioEndpointVolume)o;
            if (!masterRow.Dragging)
            {
                float mv; bool mm;
                masterRow.Master.GetMasterVolumeLevelScalar(out mv); masterRow.Master.GetMute(out mm);
                masterRow.Val = mv; masterRow.Muted = mm; ShowRow(masterRow);
            }

            Guid gs = typeof(IAudioSessionManager2).GUID;
            dev.Activate(ref gs, 23, IntPtr.Zero, out o);
            IAudioSessionManager2 mgr = (IAudioSessionManager2)o;
            IAudioSessionEnumerator se; mgr.GetSessionEnumerator(out se);
            int n; se.GetCount(out n);

            // Gabungkan semua sesi milik aplikasi yang sama (mis. banyak proses Chrome) jadi satu bar.
            Dictionary<string, List<ISimpleAudioVolume>> cur = new Dictionary<string, List<ISimpleAudioVolume>>();
            Dictionary<string, string> paths = new Dictionary<string, string>(), names = new Dictionary<string, string>();
            for (int i = 0; i < n; i++)
            {
                try
                {
                    IAudioSessionControl c; se.GetSession(i, out c);
                    IAudioSessionControl2 c2 = (IAudioSessionControl2)c;
                    int st; c.GetState(out st);
                    uint pid; c2.GetProcessId(out pid);
                    if (st == 2 || pid == 0 || c2.IsSystemSoundsSession() == 0) continue;   // 2 = expired
                    Process p = Process.GetProcessById((int)pid);
                    string nm = p.ProcessName, path = "";
                    try { path = p.MainModule.FileName; } catch { }
                    string key = nm.ToLowerInvariant();
                    if (!cur.ContainsKey(key)) { cur[key] = new List<ISimpleAudioVolume>(); names[key] = nm; paths[key] = path; }
                    else if (paths[key] == "" && path != "") paths[key] = path;
                    cur[key].Add((ISimpleAudioVolume)c);
                }
                catch { }
            }

            bool changed = false;
            List<string> gone = new List<string>();
            foreach (string k in mixRows.Keys) if (!cur.ContainsKey(k)) gone.Add(k);
            foreach (string k in gone) { mixRows.Remove(k); changed = true; }

            foreach (KeyValuePair<string, List<ISimpleAudioVolume>> kv in cur)
            {
                MixRow r;
                if (!mixRows.TryGetValue(kv.Key, out r))
                {
                    string fd = names[kv.Key], pth = paths[kv.Key];
                    ImageSource ic = null;
                    if (pth != "")
                    {
                        try { string d = FileVersionInfo.GetVersionInfo(pth).FileDescription; if (!string.IsNullOrEmpty(d)) fd = d.Trim(); } catch { }
                        ic = IconOf(pth);
                    }
                    if (fd.Length == 0) fd = names[kv.Key];
                    r = MakeRow(kv.Key, fd, ic, fd.Substring(0, 1).ToUpper());
                    mixRows[kv.Key] = r; changed = true;
                }
                r.Vols = kv.Value;
                if (r.Dragging) continue;
                float mx = 0; bool am = false;
                foreach (ISimpleAudioVolume sv in r.Vols)
                {
                    try { float f; bool m; sv.GetMasterVolume(out f); sv.GetMute(out m); if (f > mx) mx = f; if (m) am = true; } catch { }
                }
                r.Val = mx; r.Muted = am; ShowRow(r);
            }
            if (changed) RebuildMixList();
        }
        catch { }
    }

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

    // ---------- Tampilan membesar: tombol atas, header, halaman, tombol bawah, titik penunjuk ----------
    void BuildExpanded()
    {
        Button up = B("\u25B2", delegate { Step(-1, true); });
        Button down = B("\u25BC", delegate { Step(1, true); });
        up.ToolTip = "Fitur sebelumnya"; down.ToolTip = "Fitur berikutnya";
        up.Padding = new Thickness(0, 1, 0, 1); down.Padding = new Thickness(0, 1, 0, 1);
        up.FontSize = 10; down.FontSize = 10;
        up.Margin = new Thickness(50, 6, 50, 2); down.Margin = new Thickness(50, 2, 50, 7);

        clockFull = T("", 1, 16); clockFull.FontWeight = FontWeights.SemiBold;
        clockFull.HorizontalAlignment = HorizontalAlignment.Left; clockFull.VerticalAlignment = VerticalAlignment.Center;
        tabTitle = T("", 0.7, 11); tabTitle.HorizontalAlignment = HorizontalAlignment.Right; tabTitle.VerticalAlignment = VerticalAlignment.Center;
        Grid header = new Grid { Margin = new Thickness(26, 2, 14, 2) };
        dateFull = T("", 0.55, 10); dateFull.VerticalAlignment = VerticalAlignment.Center; dateFull.Margin = new Thickness(6, 3, 0, 0);
        StackPanel clk = Row(clockFull, dateFull); clk.HorizontalAlignment = HorizontalAlignment.Left; clk.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(clk); header.Children.Add(tabTitle);

        Grid host = new Grid { Margin = new Thickness(26, 2, 14, 2) };
        foreach (UIElement p in pages) { p.Visibility = Visibility.Collapsed; p.RenderTransform = new TranslateTransform(); host.Children.Add(p); }
        pages[0].Visibility = Visibility.Visible;

        // Titik penunjuk posisi fitur (klik titik untuk loncat ke fitur itu)
        StackPanel dotsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6, 0, 0, 0) };
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            Border dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(4), Background = Rgb(110, 110, 110), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            dots[i] = dot;
            Grid hit = new Grid { Width = 14, Height = 13, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tabNames[i] };
            hit.Children.Add(dot);
            hit.MouseLeftButtonDown += delegate { SwitchTo(idx); };
            dotsPanel.Children.Add(hit);
        }

        expRoot = new Grid { Width = OW, Height = OH, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, Opacity = 0 };
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(up, 0); Grid.SetRow(header, 1); Grid.SetRow(host, 2); Grid.SetRow(down, 3);
        Grid.SetRow(dotsPanel, 1); Grid.SetRowSpan(dotsPanel, 2);
        expRoot.Children.Add(up); expRoot.Children.Add(header); expRoot.Children.Add(host); expRoot.Children.Add(down); expRoot.Children.Add(dotsPanel);
    }

    void Tick(int ms, Action a)
    {
        DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += delegate { try { a(); } catch (Exception ex) { Prog.Log(ex.ToString()); } };
        t.Start();
    }

    // =====================================================================
    //  WINDOW: gaya, hotkey, hook
    // =====================================================================
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000080); // NOACTIVATE + TOOLWINDOW
        HwndSource.FromHwnd(hwnd).AddHook(Hook);
        RegisterHotKey(hwnd, 1, 0x4003, 0x49); // Ctrl+Alt+I
        AddClipboardFormatListener(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try { UnregisterHotKey(hwnd, 1); RemoveClipboardFormatListener(hwnd); } catch { }
        KillMedia();
        try { if (pop != null) pop.Close(); } catch { }
        try { tray.Visible = false; tray.Dispose(); } catch { }
    }

    IntPtr Hook(IntPtr h, int m, IntPtr w, IntPtr l, ref bool handled)
    {
        if (m == 0x0312) { ToggleBack(); handled = true; }          // WM_HOTKEY
        else if (m == 0x031D) { clipDirty = true; clipTries = 0; }  // WM_CLIPBOARDUPDATE
        return IntPtr.Zero;
    }

    void ExitApp()
    {
        quitting = true;
        KillMedia();
        try { tray.Visible = false; } catch { }
        Application.Current.Shutdown();
    }

    void ToggleBack()
    {
        back = !back;
        if (back) { SetOpen(false); Topmost = false; SetWindowPos(hwnd, (IntPtr)1, 0, 0, 0, 0, 0x13); }
        else { Topmost = true; SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13); }
    }

    // =====================================================================
    //  HOVER, BUKA/TUTUP, TAB
    // =====================================================================
    void Hover()
    {
        if (back) return;
        PT p; GetCursorPos(out p);
        PresentationSource src = PresentationSource.FromVisual(this);
        if (src == null) return;
        Point d = src.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
        double w = open ? OW : CW, h = open ? OH : collH, m = open ? 14 : 4;
        double x0 = Left + WW - w - m, x1 = Left + WW + m, y0 = Top + (WH - h) / 2 - m, y1 = Top + (WH + h) / 2 + m;
        bool inside = d.X >= x0 && d.X <= x1 && d.Y >= y0 && d.Y <= y1;
        if (editing && !inside && (GetAsyncKeyState(0x01) & 0x8000) != 0) EndEdit(false); // klik di luar island
        if (isDragging || editing) inside = true;
        if (inside) { outSince = DateTime.MinValue; if (!open) SetOpen(true); }
        else if (open && DateTime.Now > alertUntil)
        {
            if (outSince == DateTime.MinValue) outSince = DateTime.Now;
            else if ((DateTime.Now - outSince).TotalMilliseconds > 300) SetOpen(false);
        }
    }

    void SetOpen(bool o)
    {
        if (o == open) return;
        open = o; outSince = DateTime.MinValue;
        CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        TimeSpan du = TimeSpan.FromMilliseconds(240);
        isl.BeginAnimation(WidthProperty, new DoubleAnimation(o ? OW : CW, du) { EasingFunction = ease });
        isl.BeginAnimation(HeightProperty, new DoubleAnimation(o ? OH : collH, du) { EasingFunction = ease });
        RefreshClock();
        if (o)
        {
            expRoot.Visibility = Visibility.Visible;
            expRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)));
            DoubleAnimation hideMini = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            hideMini.Completed += delegate { if (open) miniRoot.Visibility = Visibility.Collapsed; };
            miniRoot.BeginAnimation(OpacityProperty, hideMini);
            curIdx = -2;
        }
        else
        {
            miniRoot.Visibility = Visibility.Visible;
            DoubleAnimation fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            fade.Completed += delegate { if (!open) expRoot.Visibility = Visibility.Collapsed; };
            expRoot.BeginAnimation(OpacityProperty, fade);
            miniRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)));
        }
    }

    // Geser fitur satu langkah. wrap=true: dari fitur terakhir lanjut ke pertama (dan sebaliknya).
    void Step(int dir, bool wrap)
    {
        int n = currentTab + dir;
        if (n < 0) n = wrap ? pages.Length - 1 : 0;
        if (n >= pages.Length) n = wrap ? 0 : pages.Length - 1;
        SwitchTo(n);
    }

    // Roda mouse di atas island: ganti fitur. Kalau kursor di atas daftar yang bisa digulir
    // (lirik tanpa waktu / clipboard) dan daftarnya belum mentok, roda menggulir daftar itu dulu.
    void Wheel(object sender, MouseWheelEventArgs e)
    {
        if (!open) return;
        DependencyObject o = e.OriginalSource as DependencyObject;
        ScrollViewer sv = null;
        while (o != null)
        {
            if (o == lPlain || o == clipScroll || o == mixScroll) { sv = (ScrollViewer)o; break; }
            o = (o is Visual) ? VisualTreeHelper.GetParent(o) : null;
        }
        if (sv != null && sv.IsVisible && sv.ScrollableHeight > 0)
        {
            bool canUp = sv.VerticalOffset > 0.5, canDown = sv.VerticalOffset < sv.ScrollableHeight - 0.5;
            if ((e.Delta > 0 && canUp) || (e.Delta < 0 && canDown)) return;
        }
        e.Handled = true;
        DateTime now = DateTime.Now;
        if ((now - lastWheel).TotalMilliseconds < 250) return; // abaikan sisa putaran setelah pindah
        if ((now - lastWheelEvt).TotalMilliseconds > 350 || Math.Sign(e.Delta) != Math.Sign(wheelAcc)) wheelAcc = 0;
        lastWheelEvt = now;
        wheelAcc += e.Delta;
        if (Math.Abs(wheelAcc) < 60) return;
        int dir = wheelAcc < 0 ? 1 : -1;
        wheelAcc = 0; lastWheel = now;
        Step(dir, true);
    }

    int switchTok;

    void SwitchTo(int idx)
    {
        if (idx < 0) idx = pages.Length - 1;
        if (idx >= pages.Length) idx = 0;
        if (idx == currentTab) return;
        UIElement old = pages[currentTab], nw = pages[idx];
        int tk = ++switchTok;
        currentTab = idx; curIdx = -2;
        UpdateTabs();
        foreach (UIElement p in pages)
            if (p != old && p != nw) { p.BeginAnimation(OpacityProperty, null); p.Visibility = Visibility.Collapsed; }

        // tanpa animasi kalau island tertutup atau halaman lama sudah tidak tampak
        if (!open || old.Visibility != Visibility.Visible)
        {
            old.Visibility = Visibility.Collapsed; ShowPage(nw, !open); return;
        }
        // halaman lama fade + naik ke atas, lalu halaman baru fade masuk dari bawah
        TimeSpan du = TimeSpan.FromMilliseconds(110);
        DoubleAnimation fo = new DoubleAnimation(0, du);
        fo.Completed += delegate
        {
            if (tk != switchTok) return;
            old.Visibility = Visibility.Collapsed; ShowPage(nw, false);
        };
        old.BeginAnimation(OpacityProperty, fo);
        ((TranslateTransform)old.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-12, du) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
    }

    void ShowPage(UIElement p, bool instant)
    {
        TranslateTransform t = (TranslateTransform)p.RenderTransform;
        p.Visibility = Visibility.Visible;
        if (instant)
        {
            p.BeginAnimation(OpacityProperty, null); t.BeginAnimation(TranslateTransform.YProperty, null);
            p.Opacity = 1; t.Y = 0; return;
        }
        p.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        t.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    void UpdateTabs()
    {
        tabTitle.Text = tabNames[currentTab];
        for (int i = 0; i < dots.Length; i++)
        {
            bool on = (i == currentTab);
            dots[i].Background = on ? (Brush)Accent : (Brush)Rgb(110, 110, 110);
            dots[i].Width = on ? 8 : 5; dots[i].Height = on ? 8 : 5;
        }
    }

    void RefreshClock()
    {
        DateTime t = DateTime.Now;
        topClock.Text = t.ToString("HH"); botClock.Text = t.ToString("mm");
        clockFull.Text = t.ToString("HH:mm");
        dateFull.Text = t.ToString("ddd, d MMM", idCulture);
    }

    // =====================================================================
    //  TIAP DETIK: jam, CPU, RAM, disk, baterai
    // =====================================================================
    void Sec()
    {
        RefreshClock();
        if (calToday != DateTime.Today) { calToday = DateTime.Today; calY = calToday.Year; calM = calToday.Month; BuildCal(); }

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

    // =====================================================================
    //  KALENDER
    // =====================================================================
    void MoveMonth(int delta)
    {
        calM += delta;
        if (calM > 12) { calM = 1; calY++; }
        if (calM < 1) { calM = 12; calY--; }
        BuildCal();
    }

    void BuildCal()
    {
        calGrid.Children.Clear();
        string[] dn = { "Sn", "Sl", "Rb", "Km", "Jm", "Sb", "Mg" };
        for (int i = 0; i < 7; i++)
        {
            TextBlock h = T(dn[i], i == 6 ? 0.9 : 0.5, 10);
            h.HorizontalAlignment = HorizontalAlignment.Center; h.VerticalAlignment = VerticalAlignment.Center;
            if (i == 6) h.Foreground = Reddish;
            calGrid.Children.Add(h);
        }
        DateTime first = new DateTime(calY, calM, 1);
        int off = ((int)first.DayOfWeek + 6) % 7;
        for (int i = 0; i < off; i++) calGrid.Children.Add(new Border());
        DateTime today = DateTime.Today;
        int dim = DateTime.DaysInMonth(calY, calM);
        for (int day = 1; day <= dim; day++)
        {
            TextBlock t = T(day.ToString(), 1, 11);
            t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            Border b = new Border { Child = t, CornerRadius = new CornerRadius(9), Margin = new Thickness(1) };
            bool isToday = (calY == today.Year && calM == today.Month && day == today.Day);
            if (isToday) b.Background = Accent;
            else if ((off + day - 1) % 7 == 6) t.Foreground = Reddish;
            calGrid.Children.Add(b);
        }
        calTitleBtn.Content = first.ToString("MMMM yyyy", idCulture);
    }

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
        btnPause.Content = tPaused ? "\u25B6 Lanjut" : (endT != DateTime.MinValue ? "\u23F8 Jeda" : "\u25B6 Mulai");
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

    void StartTimer(int minutes) { StartSec(minutes * 60.0); }

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

    void AddTimer(int minutes)
    {
        if (tPaused) { tLeft = tLeft.Add(TimeSpan.FromMinutes(minutes)); tTimer.Text = Hms(Math.Ceiling(tLeft.TotalSeconds)); UpdateCollapsed(); }
        else if (endT == DateTime.MinValue) StartSec(DigSeconds() + minutes * 60.0);
        else { endT = endT.AddMinutes(minutes); TimerTick(); }
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
        else { tTimer.Text = Hms(sec); UpdateCollapsed(); }
    }

    // ---------- Alarm ----------
    void StartAlarm()
    {
        if (alarmOn) return;
        alarmOn = true;
        btnStopAlarm.Visibility = Visibility.Visible;
        UpdateCollapsed();
        System.Threading.Thread th = new System.Threading.Thread(AlarmLoop);
        th.IsBackground = true; th.Start();
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

    // =====================================================================
    //  CLIPBOARD
    // =====================================================================
    void ClipTick()
    {
        if (!clipDirty) return;
        try
        {
            string ct = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            clipDirty = false;
            if (ct.Trim().Length == 0) return;
            hist.Remove(ct); hist.Insert(0, ct);
            while (hist.Count > MaxClip) hist.RemoveAt(hist.Count - 1);
            RenderClip();
        }
        catch { if (++clipTries > 20) clipDirty = false; } // clipboard sedang dikunci aplikasi lain: coba lagi
    }

    void RenderClip()
    {
        clipList.Children.Clear();
        if (hist.Count == 0)
        {
            TextBlock e = T("Riwayat kosong", 0.4, 12); e.HorizontalAlignment = HorizontalAlignment.Center; e.Margin = new Thickness(0, 40, 0, 0);
            clipList.Children.Add(e);
            return;
        }
        foreach (string h in hist)
        {
            string x = h;
            string show = x.Replace("\r", " ").Replace("\n", " ").Trim();
            Button b = B("", delegate { try { Clipboard.SetText(x); } catch { } });
            b.Content = T(show, 1, 11);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Padding = new Thickness(8, 3, 8, 3); b.Margin = new Thickness(0, 1, 0, 1);
            clipList.Children.Add(b);
        }
    }

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
                return k != null && k.GetValue("VorsWinIsland") != null;
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
            using (Win32.RegistryKey app = Win32.Registry.CurrentUser.CreateSubKey(@"Software\VorsWinIsland"))
            using (Win32.RegistryKey run = Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (app == null || run == null) return;
                if (app.GetValue("AutoInit") == null) { app.SetValue("AutoInit", 1); run.SetValue("VorsWinIsland", exe); }
                else
                {
                    object cur = run.GetValue("VorsWinIsland");
                    if (cur != null && !exe.Equals(Convert.ToString(cur))) run.SetValue("VorsWinIsland", exe);
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
                if (AutoStartOn()) k.DeleteValue("VorsWinIsland", false);
                else k.SetValue("VorsWinIsland", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
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

    // =====================================================================
    //  MEDIA (proses PowerShell tunggal yang terus berjalan)
    // =====================================================================
    static string ScriptPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media.ps1"); }

    void StartMedia()
    {
        if (quitting) return;
        if (!File.Exists(ScriptPath())) { SetStat("media.ps1 tidak ditemukan. Taruh satu folder dengan VorsWinIsland.exe"); return; }
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
        tTitle.Text = ti.Length > 0 ? ti : "Tidak ada media";
        curTi = ti;
        tArtist.Text = ar;
        mediaColl = (pl && ti.Length > 0) ? ti : ""; UpdateCollapsed();
        btnPlay.Content = pl ? "\u23F8" : "\u25B6";
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
        string yl = FindYt(ti, dur);   // prioritas 1: caption YouTube
        if (yl != null) { LRes yr = new LRes { synced = yl }; cache[key] = yr; ytDone.Add(key); ApplyLyrics(yr); return; }
        LRes cr;
        if (cache.TryGetValue(key, out cr)) { ApplyLyrics(cr); return; }
        SetStat("Mencari lirik...");
        string t0 = ti, a0 = ar, b0 = al, k0 = key; double d0 = dur;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            LRes r = null;
            try { r = Fetch(t0, a0, b0, d0); } catch { }
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (ytDone.Contains(k0)) return;   // caption YouTube sudah dipakai, jangan ditimpa
                if (r != null && !r.err) cache[k0] = r;
                if (lkey == k0) ApplyLyrics(r);
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
            seekFill.Width = 0; tPos.Text = "0:00"; tDur.Text = curDur > 0 ? Fmt(curDur) : "0:00";
            return;
        }
        seekFill.Width = seekBar.ActualWidth * Math.Min(1, p / curDur);
        tPos.Text = Fmt(p); tDur.Text = Fmt(curDur);
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

    // =====================================================================
    //  LIRIK
    // =====================================================================
    // ---------- Caption YouTube (dari ekstensi) ----------
    static string Norm(string s) { return Regex.Replace((s ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", ""); }

    static bool YtSame(string n, double dur, YtL y)
    {
        if (n.Length == 0 || y.n.Length == 0) return false;
        if (n != y.n && !(Math.Min(n.Length, y.n.Length) >= 6 && (n.IndexOf(y.n) >= 0 || y.n.IndexOf(n) >= 0))) return false;
        if (dur > 0 && y.dur > 0 && Math.Abs(dur - y.dur) > 3) return false;
        return true;
    }

    string FindYt(string ti, double dur)
    {
        string n = Norm(ti);
        for (int i = ytStore.Count - 1; i >= 0; i--)
        {
            if ((DateTime.Now - ytStore[i].at).TotalMinutes > 30) continue;
            if (YtSame(n, dur, ytStore[i])) return ytStore[i].lrc;
        }
        return null;
    }

    void OnYtPayload(string title, double dur, string lrc)
    {
        string n = Norm(title);
        if (n.Length == 0 || lrc.Length == 0) return;
        for (int i = ytStore.Count - 1; i >= 0; i--)
            if (ytStore[i].n == n)
            {
                if (ytStore[i].lrc == lrc) { ytStore[i].at = DateTime.Now; return; } // kiriman ulang yang sama
                ytStore.RemoveAt(i);
            }
        YtL y = new YtL { n = n, lrc = lrc, dur = dur, at = DateTime.Now };
        ytStore.Add(y);
        if (ytStore.Count > 30) ytStore.RemoveAt(0);
        if (string.IsNullOrEmpty(lkey) || curTi.Length == 0 || !YtSame(Norm(curTi), curDur, y)) return;
        LRes r = new LRes { synced = lrc };
        cache[lkey] = r; ytDone.Add(lkey); ApplyLyrics(r);
    }

    // Server HTTP mini di 127.0.0.1 (tanpa hak admin). Hanya menerima kiriman dari ekstensi (header X-Vors).
    void StartYtServer()
    {
        try
        {
            System.Net.Sockets.TcpListener l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, YtPort);
            l.Start();
            System.Threading.Thread th = new System.Threading.Thread(delegate()
            {
                while (true)
                {
                    System.Net.Sockets.TcpClient c = null;
                    try { c = l.AcceptTcpClient(); HandleYt(c); }
                    catch { }
                    finally { if (c != null) { try { c.Close(); } catch { } } }
                }
            });
            th.IsBackground = true; th.Start();
        }
        catch { } // port dipakai program lain: fitur caption mati, sisanya normal
    }

    static int HdrEnd(byte[] b, int n)
    {
        for (int i = 0; i + 3 < n; i++) if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
        return -1;
    }

    void HandleYt(System.Net.Sockets.TcpClient c)
    {
        c.ReceiveTimeout = 4000; c.SendTimeout = 4000;
        Stream st = c.GetStream();
        MemoryStream ms = new MemoryStream(); byte[] buf = new byte[16384]; int he = -1;
        while (he < 0 && ms.Length < 65536)
        {
            int n = st.Read(buf, 0, buf.Length); if (n <= 0) break;
            ms.Write(buf, 0, n); he = HdrEnd(ms.GetBuffer(), (int)ms.Length);
        }
        string reply = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok";
        try
        {
            if (he < 0) return;
            string head = Encoding.ASCII.GetString(ms.GetBuffer(), 0, he);
            Match cl = Regex.Match(head, @"(?im)^content-length:\s*(\d+)");
            bool tok = Regex.IsMatch(head, @"(?im)^x-vors:\s*island\s*$");
            if (!tok || !head.StartsWith("POST") || !cl.Success) { reply = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"; return; }
            int len = int.Parse(cl.Groups[1].Value);
            if (len <= 0 || len > 3000000) { reply = "HTTP/1.1 413 Too Large\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"; return; }
            int have = (int)ms.Length - (he + 4);
            byte[] body = new byte[len];
            int got = Math.Min(have, len);
            Array.Copy(ms.GetBuffer(), he + 4, body, 0, got);
            while (got < len) { int n = st.Read(body, got, len - got); if (n <= 0) break; got += n; }
            if (got < len) return;
            System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> d = ser.DeserializeObject(Encoding.UTF8.GetString(body)) as Dictionary<string, object>;
            if (d == null) return;
            string title = Str(d, "title"), lrc = Str(d, "lrc"); double dur;
            if (!double.TryParse(Str(d, "dur"), NumberStyles.Float, IC, out dur)) dur = 0;
            Dispatcher.BeginInvoke(new Action(delegate { OnYtPayload(title, dur, lrc); }));
        }
        catch { }
        finally { try { byte[] rb = Encoding.ASCII.GetBytes(reply); st.Write(rb, 0, rb.Length); } catch { } }
    }

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
        lStat.Text = t; lStat.Visibility = Visibility.Visible;
        lSync.Visibility = Visibility.Collapsed; lPlain.Visibility = Visibility.Collapsed;
    }

    void ApplyLyrics(LRes r)
    {
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
            plainSrc = r.plain; lPlainT.Text = r.plain; lPlain.ScrollToTop();
            lStat.Visibility = Visibility.Collapsed; lSync.Visibility = Visibility.Collapsed; lPlain.Visibility = Visibility.Visible;
            lHint.Text = "";
            StartRomanize();
            return;
        }
        SetStat("Lirik tidak ditemukan");
    }

    void ParseLrc(string src)
    {
        foreach (string raw in src.Split('\n'))
        {
            MatchCollection ms = Ts.Matches(raw);
            if (ms.Count == 0) continue;
            string txt = Wt.Replace(Ts.Replace(raw, ""), "").Trim();
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
        int idx = -1;
        for (int i = 0; i < lines.Count; i++) { if (lines[i].t <= pos) idx = i; else break; }

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

    // =====================================================================
    //  LIRIK LATIN + POP-OUT
    // =====================================================================
    void UpdateRomButtons()
    {
        if (btnRom == null) return;
        btnRom.Visibility = romKind > 0 ? Visibility.Visible : Visibility.Collapsed;
        btnRom.Background = (romOn && romKind > 0) ? Accent : Rgb(38, 38, 38);
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
        if (lsynced) { foreach (LL l in lines) src.Add(l.s); }
        else foreach (string x in plainSrc.Replace("\r", "").Split('\n')) src.Add(x);
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
            try { on = RomajiOnlineAll(uniq); } catch { }
            if (on != null)
            {
                for (int i = 0; i < uniq.Count; i++) if (on[i] != null) map[uniq[i]] = on[i];
            }
            else
            {
                List<string> yomi = null;
                try { yomi = ReadingsViaPs(uniq); } catch { }
                if (yomi == null || yomi.Count != uniq.Count) return;
                for (int i = 0; i < uniq.Count; i++) map[uniq[i]] = KanaToRomaji(yomi[i]);
            }
            List<string> res = new List<string>();
            for (int i = 0; i < segs.Count; i++) res.Add(JoinRom(segs[i], map));
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
        bool f;
        string js = Http("https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=en&dt=t&dt=rm&q=" + E(line), out f, UA, null);
        if (js == null) return null;
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        System.Collections.IList top = ser.DeserializeObject(js) as System.Collections.IList;
        if (top == null || top.Count == 0) return null;
        System.Collections.IList segs = top[0] as System.Collections.IList;
        if (segs == null) return null;
        for (int i = segs.Count - 1; i >= 0; i--)
        {
            System.Collections.IList sg = segs[i] as System.Collections.IList;
            if (sg == null || sg.Count < 3 || sg[0] != null || sg[1] != null) continue;
            string r = sg.Count > 3 ? sg[3] as string : null;
            if (string.IsNullOrEmpty(r)) r = sg[2] as string;
            if (!string.IsNullOrEmpty(r) && !HasJa(r)) return r;
        }
        return null;
    }

    // Romaji semua baris (4 permintaan paralel). Elemen null = baris itu tidak bisa diubah (pakai hasil cepat).
    // Mengembalikan null kalau separuh lebih baris gagal (offline / diblokir).
    static List<string> RomajiOnlineAll(List<string> src)
    {
        string[] res = new string[src.Count];
        int next = -1, tot = 0, okc = 0;
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
                    string r = null;
                    try { r = RomajiOnline(src[i]); } catch { }
                    if (r != null) { res[i] = r; System.Threading.Interlocked.Increment(ref okc); }
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
            if (!Kana.TryGetValue(c, out b)) { sokuon = false; o.Append(JpPunct(src[i])); continue; }

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

    // ---------- LRCLIB ----------
    static string E(string s) { return Uri.EscapeDataString(s); }

    static string CleanTitle(string t)
    {
        string s = Regex.Replace(t, @"\s*[\(\[][^\)\]]*[\)\]]", "");
        s = Regex.Replace(s, @"(?i)\s*[-\u2013|]\s*(official|lyrics?|video|audio|topic|remaster).*$", "");
        s = s.Trim();
        return s.Length > 0 ? s : t;
    }

    static string Http(string url, out bool failed) { return Http(url, out failed, "VorsWinIsland/1.0", null); }

    static string Http(string url, out bool failed, string ua, string referer)
    {
        failed = false;
        try
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.UserAgent = ua; rq.Timeout = 8000; rq.ReadWriteTimeout = 8000;
            if (referer != null) rq.Referer = referer;
            using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
            using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        catch (WebException ex)
        {
            HttpWebResponse hr = ex.Response as HttpWebResponse;
            if (hr != null && (int)hr.StatusCode >= 400 && (int)hr.StatusCode < 500) return null; // mis. 404 = tidak ada
            failed = true; return null;
        }
        catch { failed = true; return null; }
    }

    static LRes ToRes(Dictionary<string, object> d)
    {
        if (d == null) return null;
        LRes r = new LRes();
        r.synced = Str(d, "syncedLyrics"); r.plain = Str(d, "plainLyrics");
        object ins;
        r.instr = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins;
        return r;
    }

    static bool Has(LRes r) { return r != null && (r.instr || r.synced.Length > 0 || r.plain.Length > 0); }

    static LRes Pick(object o, double dur)
    {
        System.Collections.IEnumerable arr = o as System.Collections.IEnumerable;
        if (o is Dictionary<string, object> || arr == null) return null;
        Dictionary<string, object> best = null; double bs = double.MaxValue;
        foreach (object it in arr)
        {
            Dictionary<string, object> d = it as Dictionary<string, object>;
            if (d == null) continue;
            bool sy = Str(d, "syncedLyrics").Length > 0, pl = Str(d, "plainLyrics").Length > 0;
            object ins; bool instr = d.TryGetValue("instrumental", out ins) && ins is bool && (bool)ins;
            if (!sy && !pl && !instr) continue;
            double dd = 0; double.TryParse(Str(d, "duration"), NumberStyles.Float, IC, out dd);
            if (dur > 0 && dd > 0 && Math.Abs(dd - dur) > 15) continue; // kemungkinan lagu lain
            double score = (dur > 0 && dd > 0 ? Math.Abs(dd - dur) : 5) + (sy ? 0 : 1000);
            if (score < bs) { bs = score; best = d; }
        }
        return ToRes(best);
    }

    // Urutan: LRCLIB (berwaktu) -> NetEase (berwaktu) -> lirik polos LRCLIB.
    static LRes Fetch(string title, string artist, string album, double dur)
    {
        LRes r = FetchLrclib(title, artist, album, dur);
        if (r != null && !r.err && (r.instr || r.synced.Length > 0)) return r;
        LRes ne = null;
        try { ne = FetchNetease(title, artist, dur); } catch { }
        if (ne != null && ne.synced.Length > 0) return ne;
        return r;
    }

    // NetEase Cloud Music (API tidak resmi, bisa berubah sewaktu-waktu). Hanya dipakai kalau durasi dan judul/artis cocok.
    static LRes FetchNetease(string title, string artist, double dur)
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
        const string REF = "https://music.163.com/";
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        string cleanT = CleanTitle(title), nt = Norm(cleanT), na = Norm(artist);
        bool f;
        string js = Http("https://music.163.com/api/search/get/web?csrf_token=&type=1&offset=0&limit=8&s=" + E(cleanT + (artist.Length > 0 ? " " + artist : "")), out f, UA, REF);
        if (js == null) return null;
        Dictionary<string, object> root = ser.DeserializeObject(js) as Dictionary<string, object>;
        if (root == null || !root.ContainsKey("result")) return null;
        Dictionary<string, object> res = root["result"] as Dictionary<string, object>;
        if (res == null || !res.ContainsKey("songs")) return null;
        System.Collections.IEnumerable songs = res["songs"] as System.Collections.IEnumerable;
        if (songs == null) return null;
        string bestId = null; double bestDiff = double.MaxValue;
        foreach (object o in songs)
        {
            Dictionary<string, object> sd = o as Dictionary<string, object>;
            if (sd == null) continue;
            double dd = 0; double.TryParse(Str(sd, "duration"), NumberStyles.Float, IC, out dd); dd /= 1000.0;
            double diff = (dur > 0 && dd > 0) ? Math.Abs(dd - dur) : 999;
            if (dur > 0 && diff > 3) continue;
            string nn = Norm(Str(sd, "name"));
            bool tOk = nn.Length > 0 && nt.Length > 0 && (nn.IndexOf(nt) >= 0 || nt.IndexOf(nn) >= 0);
            bool aOk = false;
            System.Collections.IEnumerable ars = sd.ContainsKey("artists") ? sd["artists"] as System.Collections.IEnumerable : null;
            if (ars != null)
                foreach (object ao in ars)
                {
                    Dictionary<string, object> ad = ao as Dictionary<string, object>;
                    string an = ad == null ? "" : Norm(Str(ad, "name"));
                    if (an.Length > 0 && na.Length > 0 && (na.IndexOf(an) >= 0 || an.IndexOf(na) >= 0)) aOk = true;
                }
            bool ok = dur > 0 ? (tOk || (aOk && diff <= 1.5)) : (tOk && aOk);
            if (ok && diff < bestDiff) { bestDiff = diff; bestId = Str(sd, "id"); }
        }
        if (string.IsNullOrEmpty(bestId)) return null;
        js = Http("https://music.163.com/api/song/lyric?lv=1&kv=1&tv=-1&id=" + E(bestId), out f, UA, REF);
        if (js == null) return null;
        Dictionary<string, object> ly = ser.DeserializeObject(js) as Dictionary<string, object>;
        Dictionary<string, object> lrc = (ly != null && ly.ContainsKey("lrc")) ? ly["lrc"] as Dictionary<string, object> : null;
        string text = lrc == null ? "" : Str(lrc, "lyric");
        if (text.Length == 0) return null;
        StringBuilder sb = new StringBuilder(); int cnt = 0;
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            if (!Regex.IsMatch(line, @"^\[\d+:\d+")) continue;
            if (Regex.IsMatch(line, @"^\[[^\]]*\]\s*(\u4F5C\u8BCD|\u4F5C\u66F2|\u7F16\u66F2|\u5236\u4F5C\u4EBA?|\u4F5C\u8A5E|\u7DE8\u66F2|\u8BCD|\u66F2|\u6DF7\u97F3|\u6BCD\u5E26|Lyrics?|Music|Composer|Arranger)\s*[:\uFF1A]", RegexOptions.IgnoreCase)) continue;
            sb.Append(line).Append('\n'); cnt++;
        }
        if (cnt < 5) return null;
        return new LRes { synced = sb.ToString() };
    }

    static LRes FetchLrclib(string title, string artist, string album, double dur)
    {
        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        const string api = "https://lrclib.net/api/";
        int fails = 0, oks = 0; bool f;
        string cleanT = CleanTitle(title);

        if (artist.Length > 0)
        {
            string u = api + "get?track_name=" + E(title) + "&artist_name=" + E(artist);
            if (album.Length > 0) u += "&album_name=" + E(album);
            if (dur > 0) u += "&duration=" + ((int)Math.Round(dur)).ToString(IC);
            string js = Http(u, out f);
            if (f) fails++; else oks++;
            if (js != null)
            {
                try { LRes r = ToRes(ser.DeserializeObject(js) as Dictionary<string, object>); if (Has(r)) return r; } catch { }
            }
        }

        List<string> qs = new List<string>();
        if (artist.Length > 0)
        {
            qs.Add("track_name=" + E(cleanT) + "&artist_name=" + E(artist));
            qs.Add("q=" + E(cleanT + " " + artist));
        }
        qs.Add("q=" + E(cleanT));
        if (cleanT != title) qs.Add("q=" + E(title));
        foreach (string q in qs)
        {
            string js = Http(api + "search?" + q, out f);
            if (f) { fails++; continue; }
            oks++;
            if (js == null) continue;
            try { LRes best = Pick(ser.DeserializeObject(js), dur); if (Has(best)) return best; } catch { }
        }
        if (fails > 0 && oks == 0) return new LRes { err = true };
        return null;
    }
}

// Jendela pop-out: pil hitam berisi satu baris lirik, melayang sedikit di atas taskbar.
// Klik menembus jendela ini (tidak mengganggu aplikasi di bawahnya).
class PopLyric : Window
{
    const double PW = 880, PH = 170, Gap = 28, PadX = 26, PadY = 12;   // Gap = jarak pil dari bawah area kerja (di atas taskbar)

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);

    Border pill;
    TextBlock tb;
    TranslateTransform tr = new TranslateTransform();
    string target = "", shown = null;
    bool busy, sizeSet;
    int gen;
    IntPtr hwnd;

    public PopLyric()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize; Focusable = false;
        Width = PW; Height = PH; Title = "VorsWin Lirik";
        tb = new TextBlock
        {
            Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, FontFamily = new FontFamily("Segoe UI, Yu Gothic UI, Malgun Gothic, Segoe UI Symbol"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = tr, Opacity = 0
        };
        pill = new Border
        {
            Background = Brushes.Black, CornerRadius = new CornerRadius(24), Child = tb, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, Gap)
        };
        Content = pill;
        Place();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        // NOACTIVATE + TOOLWINDOW + TRANSPARENT (klik menembus)
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x80 | 0x20);
    }

    public void Place()
    {
        Rect wa = SystemParameters.WorkArea;
        double w = Math.Min(PW, wa.Width - 10);
        if (Width != w) Width = w;
        tb.MaxWidth = w - 2 * PadX - 24;
        double l = wa.Left + (wa.Width - w) / 2, t = wa.Bottom - PH;
        if (Left != l) Left = l;
        if (Top != t) Top = t;
    }

    public void Raise()
    {
        if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13);
    }

    public void Reset()
    {
        gen++; busy = false; sizeSet = false; shown = null; target = "";
        tb.BeginAnimation(OpacityProperty, null); pill.BeginAnimation(OpacityProperty, null);
        tr.BeginAnimation(TranslateTransform.YProperty, null);
        pill.BeginAnimation(WidthProperty, null); pill.BeginAnimation(HeightProperty, null);
        tb.Opacity = 0; pill.Opacity = 0;
        pill.ClearValue(WidthProperty); pill.ClearValue(HeightProperty);
    }

    // Ganti teks: teks lama memudar sambil naik, teks baru muncul dari bawah sambil memudar masuk. Ukuran pil menyesuaikan.
    public void SetText(string t)
    {
        target = t ?? "";
        if (!busy) Next();
    }

    void Next()
    {
        if (shown == target) { busy = false; return; }
        busy = true;
        int g = ++gen;
        if (shown != null && shown.Length > 0)
        {
            TimeSpan d = TimeSpan.FromMilliseconds(140);
            tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -12, d));
            DoubleAnimation fo = new DoubleAnimation(0, d);
            fo.Completed += delegate { if (g == gen) Swap(g); };
            tb.BeginAnimation(OpacityProperty, fo);
        }
        else Swap(g);
    }

    void Swap(int g)
    {
        string t = target; shown = t;
        if (t.Length == 0)
        {
            pill.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));
            busy = false;
            return;
        }
        tb.Text = t;
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = Math.Ceiling(tb.DesiredSize.Width) + 2 * PadX, h = Math.Ceiling(tb.DesiredSize.Height) + 2 * PadY;
        TimeSpan din = TimeSpan.FromMilliseconds(220);
        CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        if (!sizeSet) { pill.Width = w; pill.Height = h; sizeSet = true; }
        else
        {
            pill.BeginAnimation(WidthProperty, new DoubleAnimation(w, din) { EasingFunction = ease });
            pill.BeginAnimation(HeightProperty, new DoubleAnimation(h, din) { EasingFunction = ease });
        }
        pill.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
        tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, din) { EasingFunction = ease });
        DoubleAnimation fi = new DoubleAnimation(0, 1, din);
        fi.Completed += delegate { if (g != gen) return; busy = false; if (shown != target) Next(); };
        tb.BeginAnimation(OpacityProperty, fi);
    }
}
