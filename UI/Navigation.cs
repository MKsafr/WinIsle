using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

partial class Island
{
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
        isl.CornerRadius = new CornerRadius(o ? 22 : 12, 0, 0, o ? 22 : 12);
        if (glowBody != null) glowBody.CornerRadius = isl.CornerRadius;
        UpdateGlowContour();
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


}
