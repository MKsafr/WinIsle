using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

partial class Island
{
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


}
