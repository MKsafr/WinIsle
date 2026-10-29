using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

// Jendela pop-out: pil hitam berisi lirik, awalnya melayang sedikit di atas taskbar.
// Pil bisa digeser dengan mouse dan selalu ditahan di dalam area kerja layar.
// Bagian jendela yang transparan menembus klik (tidak mengganggu aplikasi di bawahnya); hanya pilnya yang menangkap mouse.
class PopLyric : Window
{
    const double PW = 400, PH = 64, Gap = 12, PadX = 9, PadY = 4;   // Gap = jarak pil dari bawah area kerja (di atas taskbar)
    const double DefaultFontSize = 16, MinFontSize = 10, MaxFontSize = 48;
    const double DefaultMaxWidth = 400, MinMaxWidth = 100, MaxMaxWidth = 900, CornerRadiusSize = 11;
    const string SettingsPath = @"Software\WinIsle\PopLyric";

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PT p);
    struct PT { public int X, Y; }

    Border pill;
    TextBlock tb;
    TranslateTransform tr = new TranslateTransform();
    string target = "", shown = null;
    bool busy, sizeSet;
    int gen;
    IntPtr hwnd;
    bool moved, dragging, legacyPosition, defaultTopSet;
    double userAnchorX, userTop, curAnchorX, curTop, legacyCx, legacyBottom, pillW, pillH;
    double dragX0, dragY0, dragAnchorX0, dragTop0;
    double fontSize = DefaultFontSize, maxWidth = DefaultMaxWidth;
    TextAlignment textAlignment = TextAlignment.Center;

    public PopLyric()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize; Focusable = false;
        Width = PW; Height = PH; Title = "WinIsle Lirik";
        tb = new TextBlock
        {
            Foreground = Brushes.White, FontSize = DefaultFontSize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, FontFamily = new FontFamily("Segoe UI, Yu Gothic UI, Malgun Gothic, Segoe UI Symbol"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = tr, Opacity = 0
        };
        pill = new Border
        {
            Background = Brushes.Black, CornerRadius = new CornerRadius(CornerRadiusSize),
            Padding = new Thickness(PadX, PadY, PadX, PadY), Child = tb, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top
        };
        pill.SizeChanged += delegate { UpdatePillClip(); };
        UpdatePillClip();
        pill.Cursor = Cursors.SizeAll;
        pill.MouseLeftButtonDown += PillDown;
        pill.MouseMove += PillMove;
        pill.MouseLeftButtonUp += PillUp;
        pill.LostMouseCapture += delegate
        {
            if (dragging)
            {
                dragging = false;
                if (moved) { userAnchorX = curAnchorX; userTop = curTop; SaveSettings(); }
            }
        };
        Content = pill;
        LoadSettings();
        Place();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        // NOACTIVATE + TOOLWINDOW. Piksel yang sepenuhnya transparan menembus klik dengan sendirinya, jadi pil bisa diklik/digeser.
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x80);
    }

    static double Cl(double v, double lo, double hi)
    {
        if (hi < lo) return (lo + hi) / 2;
        return Math.Max(lo, Math.Min(hi, v));
    }

    void UpdatePillClip()
    {
        double width = pill.ActualWidth;
        double height = pill.ActualHeight;
        if (width > 0 && height > 0)
            pill.Clip = new RectangleGeometry(new Rect(0, 0, width, height), CornerRadiusSize, CornerRadiusSize);
    }

    public void Place()
    {
        Rect wa = SystemParameters.WorkArea;
        double maxPillWidth = Math.Min(maxWidth, wa.Width - 10);
        double w = pillW > 0 ? Math.Min(pillW, maxPillWidth) : Math.Min(PW - 12, maxPillWidth);
        double h = pillH > 0 ? pillH : PH;
        if (!moved && !defaultTopSet && pillH > 0)
        {
            userTop = wa.Bottom - Gap - h;
            defaultTopSet = true;
        }
        if (legacyPosition && pillW > 0 && pillH > 0)
        {
            userAnchorX = legacyCx + (textAlignment == TextAlignment.Left ? -w / 2 :
                (textAlignment == TextAlignment.Right ? w / 2 : 0));
            userTop = legacyBottom - h;
            legacyPosition = false;
        }
        double centerX = wa.Left + wa.Width / 2;
        double anchorX = moved ? userAnchorX : centerX +
            (textAlignment == TextAlignment.Left ? -w / 2 : (textAlignment == TextAlignment.Right ? w / 2 : 0));
        double top = moved ? userTop : (defaultTopSet ? userTop : wa.Bottom - Gap - h);
        double left = textAlignment == TextAlignment.Left ? anchorX :
            (textAlignment == TextAlignment.Right ? anchorX - w : anchorX - w / 2);
        left = Cl(left, wa.Left, wa.Right - w);
        top = Cl(top, wa.Top, wa.Bottom - h);
        anchorX = textAlignment == TextAlignment.Left ? left :
            (textAlignment == TextAlignment.Right ? left + w : left + w / 2);
        if (moved && !dragging)
        {
            userAnchorX = anchorX;
            userTop = top;
        }
        curAnchorX = anchorX; curTop = top;
        if (Width != w) Width = w;
        if (Height != h) Height = h;
        tb.MaxWidth = Math.Max(0, maxPillWidth - 2 * PadX);
        tb.MaxHeight = double.PositiveInfinity;
        HorizontalAlignment alignment = textAlignment == TextAlignment.Left ? HorizontalAlignment.Left :
            (textAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center);
        if (pill.HorizontalAlignment != alignment)
            pill.HorizontalAlignment = alignment;
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    public double FontSizeSetting { get { return fontSize; } }
    public double MaxWidthSetting { get { return maxWidth; } }
    public TextAlignment AlignmentSetting { get { return textAlignment; } }

    public void ApplySettings(double newFont, double newWidth, TextAlignment newAlignment)
    {
        if (moved && textAlignment != newAlignment)
        {
            double left = textAlignment == TextAlignment.Left ? userAnchorX :
                (textAlignment == TextAlignment.Right ? userAnchorX - pillW : userAnchorX - pillW / 2);
            userAnchorX = newAlignment == TextAlignment.Left ? left :
                (newAlignment == TextAlignment.Right ? left + pillW : left + pillW / 2);
        }
        fontSize = newFont; maxWidth = newWidth; textAlignment = newAlignment;
        tb.FontSize = fontSize;
        tb.TextAlignment = textAlignment;
        Place();
        if (shown != null) { shown = null; busy = false; Next(); }
        SaveSettings();
    }

    public void ResetSettings()
    {
        fontSize = DefaultFontSize; maxWidth = DefaultMaxWidth; textAlignment = TextAlignment.Center;
        moved = false; defaultTopSet = false; userAnchorX = userTop = 0;
        tb.FontSize = fontSize;
        tb.TextAlignment = textAlignment;
        pill.ClearValue(WidthProperty); pill.ClearValue(HeightProperty);
        pillW = pillH = 0; sizeSet = false;
        if (shown != null) { shown = null; busy = false; Next(); }
        Place();
        SaveSettings();
    }

    void LoadSettings()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsPath))
            {
                if (key == null) return;
                fontSize = ReadSetting(key, "FontSize", DefaultFontSize);
                bool legacyCustomSize = Convert.ToInt32(key.GetValue("CustomSize", 0), CultureInfo.InvariantCulture) != 0;
                maxWidth = key.GetValue("MaxWidth") != null ? ReadSetting(key, "MaxWidth", DefaultMaxWidth) :
                    (legacyCustomSize ? ReadSetting(key, "Width", DefaultMaxWidth) : DefaultMaxWidth);
                string alignment = Convert.ToString(key.GetValue("Alignment", "Center"), CultureInfo.InvariantCulture);
                textAlignment = alignment == "Left" ? TextAlignment.Left :
                    (alignment == "Right" ? TextAlignment.Right : TextAlignment.Center);
                moved = Convert.ToInt32(key.GetValue("Moved", 0), CultureInfo.InvariantCulture) != 0;
                if (key.GetValue("AnchorX") != null && key.GetValue("Top") != null)
                {
                    userAnchorX = ReadSetting(key, "AnchorX", 0);
                    userTop = ReadSetting(key, "Top", 0);
                }
                else if (moved)
                {
                    legacyCx = ReadSetting(key, "CenterX", 0);
                    legacyBottom = ReadSetting(key, "Bottom", 0);
                    legacyPosition = true;
                }
            }
            fontSize = Cl(fontSize, MinFontSize, MaxFontSize);
            maxWidth = Cl(maxWidth, MinMaxWidth, MaxMaxWidth);
            tb.FontSize = fontSize;
            tb.TextAlignment = textAlignment;
        }
        catch (Exception ex) { Prog.Log(ex.ToString()); }
    }

    static double ReadSetting(Microsoft.Win32.RegistryKey key, string name, double fallback)
    {
        object value = key.GetValue(name);
        double result;
        return value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    void SaveSettings()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsPath))
            {
                if (key == null) throw new InvalidOperationException("Tidak dapat menyimpan pengaturan pop-out lirik.");
                key.SetValue("FontSize", fontSize.ToString("R", CultureInfo.InvariantCulture));
                key.SetValue("MaxWidth", maxWidth.ToString("R", CultureInfo.InvariantCulture));
                key.SetValue("Alignment", textAlignment.ToString());
                key.DeleteValue("Width", false);
                key.DeleteValue("Height", false);
                key.DeleteValue("CustomSize", false);
                key.SetValue("Moved", moved ? 1 : 0);
                key.SetValue("AnchorX", userAnchorX.ToString("R", CultureInfo.InvariantCulture));
                key.SetValue("Top", userTop.ToString("R", CultureInfo.InvariantCulture));
                key.DeleteValue("CenterX", false);
                key.DeleteValue("Bottom", false);
            }
        }
        catch (Exception ex) { Prog.Log(ex.ToString()); }
    }

    // Posisi kursor di layar dalam DIP (GetCursorPos memakai piksel fisik).
    bool CursorDip(out Point p)
    {
        p = new Point();
        PT pt;
        if (!GetCursorPos(out pt)) return false;
        PresentationSource src = PresentationSource.FromVisual(this);
        if (src == null || src.CompositionTarget == null) { p = new Point(pt.X, pt.Y); return true; }
        p = src.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y));
        return true;
    }

    void PillDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        Point c;
        if (!CursorDip(out c)) return;
        dragging = true; dragX0 = c.X; dragY0 = c.Y; dragAnchorX0 = curAnchorX; dragTop0 = curTop;
        pill.CaptureMouse();
        e.Handled = true;
    }

    void PillMove(object sender, MouseEventArgs e)
    {
        if (!dragging) return;
        Point c;
        if (!CursorDip(out c)) return;
        moved = true;
        userAnchorX = dragAnchorX0 + (c.X - dragX0);
        userTop = dragTop0 + (c.Y - dragY0);
        Place();
    }

    void PillUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        pill.ReleaseMouseCapture();
        if (moved) { userAnchorX = curAnchorX; userTop = curTop; SaveSettings(); }
        e.Handled = true;
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
        Place();
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
        Place();
        tb.Width = double.NaN;
        tb.Measure(new Size(tb.MaxWidth, double.PositiveInfinity));
        double textWidth = Math.Min(tb.MaxWidth, Math.Ceiling(tb.DesiredSize.Width));
        tb.Width = textWidth;
        tb.Measure(new Size(textWidth, double.PositiveInfinity));
        double w = textWidth + 2 * PadX, h = Math.Ceiling(tb.DesiredSize.Height) + 2 * PadY;
        pillW = w; pillH = h; Place();   // pil baru bisa lebih lebar: pastikan tetap di dalam layar
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
