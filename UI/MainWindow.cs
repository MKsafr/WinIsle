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

partial class Island : Window
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
    class LRes { public string synced = "", plain = ""; public bool instr, err; public double score; }
    class LQuery { public string title, artist; }

    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static readonly Regex Ts = new Regex(@"\[(\d+):(\d+(?:\.\d+)?)\]");
    static readonly Regex Wt = new Regex(@"<\d+:\d+(?:\.\d+)?>");
    static readonly Dictionary<string, string> RomajiCache = new Dictionary<string, string>();
    static readonly object RomajiCacheLock = new object();
    static readonly FontFamily FF = new FontFamily("Segoe UI, Segoe UI Symbol, Segoe UI Emoji");
    static readonly SolidColorBrush Accent = new SolidColorBrush(Color.FromRgb(30, 144, 255));
    static readonly SolidColorBrush Reddish = new SolidColorBrush(Color.FromRgb(255, 105, 97));

    // ---------- UI ----------
    Border isl, seekFill, titleHost;
    Canvas titleCanvas;
    Grid miniRoot, expRoot, seekBar, shell;
    Canvas bodyCanvas;
    System.Windows.Shapes.Path bodyShape;
    System.Windows.Media.Effects.DropShadowEffect glowEffect;
    UniformGrid calGrid;
    UIElement[] pages = new UIElement[8];
    Border[] dots = new Border[8];
    ScrollViewer clipScroll;
    StackPanel pageDotsPanel;
    Grid lyricArea, lyricSettingsPanel;
    TextBox popFontChoice, popWidthChoice;
    Button[] popAlignmentButtons;
    TextAlignment popAlignmentSelection = TextAlignment.Center;
    bool popSettingsEditing;
    TextBlock popSettingsStatus;
    Button btnPlay, btnAwake, btnAuto, btnGlow, calTitleBtn, btnLyricRefresh, btnLyricSettings;
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
    string curTi = "", curAlbum = "";
    bool lsynced, playingNow, open, back, awake, glowEnabled, isDragging, quitting, clipDirty = true, lyricConnectionError;
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
    TextBlock calInfo;      // keterangan tanggal terpilih / ringkasan bulan (tab Kalender)
    DateTime? calSel;       // tanggal yang sedang dipilih di kalender

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

    void UpdateBodyShape()
    {
        if (isl == null || isl.ActualWidth <= 0 || isl.ActualHeight <= 0) return;
        double w = isl.ActualWidth, h = isl.ActualHeight;
        Point p = isl.TransformToAncestor(shell).Transform(new Point(0, 0));
        if (bodyShape == null) return;

        const double extW = 28, extH = 14;
        double r = isl.CornerRadius.TopLeft;
        const double k = 0.55228475;
        StreamGeometry geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(w, 0), true, true);
            ctx.BezierTo(
                new Point(w - extW + extW * 0.85, extH * 0.95),
                new Point(w - extW + extW * 0.4, extH),
                new Point(w - extW, extH), true, false);
            ctx.LineTo(new Point(r, extH), true, false);
            ctx.BezierTo(
                new Point(r - r * k, extH),
                new Point(0, extH + r - r * k),
                new Point(0, extH + r), true, false);
            ctx.LineTo(new Point(0, extH + h - r), true, false);
            ctx.BezierTo(
                new Point(0, extH + h - r + r * k),
                new Point(r - r * k, extH + h),
                new Point(r, extH + h), true, false);
            ctx.LineTo(new Point(w - extW, extH + h), true, false);
            ctx.BezierTo(
                new Point(w - extW + extW * 0.4, extH + h),
                new Point(w - extW + extW * 0.85, extH + h + extH * 0.05),
                new Point(w, extH + h + extH), true, false);
            ctx.LineTo(new Point(w, extH + h), true, false);
            ctx.LineTo(new Point(w, extH), true, false);
        }
        geometry.Freeze();
        bodyShape.Data = geometry;
        Canvas.SetLeft(bodyShape, p.X);
        Canvas.SetTop(bodyShape, p.Y - extH);
    }

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
        Width = WW; Height = WH; Title = "WinIsle";
        Reposition();
        try { idCulture = new CultureInfo("id-ID"); } catch { idCulture = CultureInfo.CurrentCulture; }
        calY = DateTime.Today.Year; calM = DateTime.Today.Month;

        AutoStartInit();
        glowEnabled = LoadGlowSetting();
        BuildMini();
        BuildPages();
        BuildExpanded();

        Grid root = new Grid();
        root.Children.Add(miniRoot);
        root.Children.Add(expRoot);
        glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.White, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.35
        };
        bodyCanvas = new Canvas { IsHitTestVisible = false };
        bodyShape = new System.Windows.Shapes.Path
        {
            Fill = Brushes.Black, Effect = glowEnabled ? glowEffect : null, IsHitTestVisible = false
        };
        bodyCanvas.Children.Add(bodyShape);
        isl = new Border
        {
            Background = Brushes.Transparent, CornerRadius = new CornerRadius(12, 0, 0, 12), Margin = new Thickness(12, 12, 0, 12),
            Width = CW, Height = CH,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true,
            Child = root
        };
        shell = new Grid();
        shell.Children.Add(bodyCanvas);
        shell.Children.Add(isl);
        Content = shell;
        isl.SizeChanged += delegate(object s, SizeChangedEventArgs e) { UpdateBodyShape(); };
        SizeChanged += delegate { UpdateBodyShape(); };
        UpdateBodyShape();
        isl.PreviewMouseWheel += Wheel;
        isl.PreviewMouseDown += delegate { if (editing && !tTimer.IsMouseOver) EndEdit(true); };
        Deactivated += delegate { if (editing) EndEdit(false); };

        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Visible = true, Text = "WinIsle" };
        Forms.ContextMenuStrip cm = new Forms.ContextMenuStrip();
        cm.Items.Add("Ke belakang / ke depan (Ctrl+Alt+I)", null, delegate { ToggleBack(); });
        cm.Items.Add("Keluar", null, delegate { ExitApp(); });
        tray.ContextMenuStrip = cm;
        tray.MouseClick += delegate(object s, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) ToggleBack(); };

        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { Reposition(); };

        Tick(120, Hover);
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
        CheckForUpdates();
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


}
