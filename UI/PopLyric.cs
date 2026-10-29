using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

// Jendela pop-out: pil hitam berisi satu baris lirik, awalnya melayang sedikit di atas taskbar.
// Pil bisa digeser dengan mouse (klik dua kali = kembali ke posisi awal) dan selalu ditahan di dalam area kerja layar.
// Bagian jendela yang transparan menembus klik (tidak mengganggu aplikasi di bawahnya); hanya pilnya yang menangkap mouse.
class PopLyric : Window
{
    const double PW = 400, PH = 64, Gap = 12, PadX = 9, PadY = 4;   // Gap = jarak pil dari bawah area kerja (di atas taskbar)

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
    // Posisi pil: pusat horizontal (cx) dan tepi bawah, dalam koordinat layar (DIP)
    bool moved, dragging;                    // moved = pengguna sudah memindahkan pil
    double userCx, userBottom, curCx, curBottom, pillW, pillH;
    double dragX0, dragY0, dragCx0, dragBottom0;

    public PopLyric()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize; Focusable = false;
        Width = PW; Height = PH; Title = "WinIsle Lirik";
        tb = new TextBlock
        {
            Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, FontFamily = new FontFamily("Segoe UI, Yu Gothic UI, Malgun Gothic, Segoe UI Symbol"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = tr, Opacity = 0
        };
        pill = new Border
        {
            Background = Brushes.Black, CornerRadius = new CornerRadius(11), Child = tb, Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, Gap),
            ClipToBounds = true
        };
        pill.Cursor = Cursors.SizeAll;
        pill.MouseLeftButtonDown += PillDown;
        pill.MouseMove += PillMove;
        pill.MouseLeftButtonUp += PillUp;
        pill.LostMouseCapture += delegate { if (dragging) { dragging = false; if (moved) { userCx = curCx; userBottom = curBottom; } } };
        Content = pill;
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

    // Letakkan jendela supaya pil berada di (cx, bottom) yang diminta, lalu tahan pil di dalam area kerja layar.
    public void Place()
    {
        Rect wa = SystemParameters.WorkArea;
        double w = Math.Min(PW, wa.Width - 10);
        if (Width != w) Width = w;
        tb.MaxWidth = w - 2 * PadX - 12;
        double pw = pillW > 0 ? pillW : pill.ActualWidth, ph = pillH > 0 ? pillH : pill.ActualHeight;
        double cx = moved ? userCx : wa.Left + wa.Width / 2;
        double bottom = moved ? userBottom : wa.Bottom - Gap;
        cx = Cl(cx, wa.Left + pw / 2, wa.Right - pw / 2);
        bottom = Cl(bottom, wa.Top + ph, wa.Bottom);
        curCx = cx; curBottom = bottom;
        if (moved && !dragging) { userCx = cx; userBottom = bottom; }
        double l = cx - w / 2, t = bottom + Gap - PH;   // pil menempel di bawah jendela dengan jarak Gap
        if (Left != l) Left = l;
        if (Top != t) Top = t;
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
        if (e.ClickCount >= 2)   // klik dua kali: kembali ke posisi awal
        {
            dragging = false; pill.ReleaseMouseCapture(); moved = false; Place();
            e.Handled = true;
            return;
        }
        Point c;
        if (!CursorDip(out c)) return;
        dragging = true; dragX0 = c.X; dragY0 = c.Y; dragCx0 = curCx; dragBottom0 = curBottom;
        pill.CaptureMouse();
        e.Handled = true;
    }

    void PillMove(object sender, MouseEventArgs e)
    {
        if (!dragging) return;
        Point c;
        if (!CursorDip(out c)) return;
        moved = true;
        userCx = dragCx0 + (c.X - dragX0);          // pil mengikuti kursor; Place() menahannya di dalam layar
        userBottom = dragBottom0 + (c.Y - dragY0);
        Place();
    }

    void PillUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        pill.ReleaseMouseCapture();
        if (moved) { userCx = curCx; userBottom = curBottom; }
        e.Handled = true;
    }

    public void Raise()
    {
        if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13);
    }

    public void Reset()
    {
        gen++; busy = false; sizeSet = false; shown = null; target = "";
        moved = false;
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
