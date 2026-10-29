using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

partial class Island
{
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
        btnLyricRefresh = SB("\u21BB", delegate { RetryLyrics(); });
        btnLyricRefresh.ToolTip = "Coba hubungkan kembali ke LRCLIB";
        btnLyricRefresh.IsEnabled = false;
        btnLyricRefresh.Focusable = false;
        Grid lyricHeader = new Grid { Height = 28 };
        btnLyricRefresh.HorizontalAlignment = HorizontalAlignment.Right;
        lyricHeader.Children.Add(btnLyricRefresh);
        Grid lyricArea = new Grid { Height = 126, ClipToBounds = true };
        lyricArea.Children.Add(lStat); lyricArea.Children.Add(lSync); lyricArea.Children.Add(lPlain);
        lOff = T("0.0s", 0.7, 11); lOff.VerticalAlignment = VerticalAlignment.Center; lOff.TextAlignment = TextAlignment.Center; lOff.MinWidth = 36;
        btnRom = SB("Aa", delegate { ToggleRom(); }); btnRom.ToolTip = "Lirik latin (romaji / romanisasi)"; btnRom.Visibility = Visibility.Visible;
        btnPop = SB("\u2750", delegate { TogglePop(); }); btnPop.ToolTip = "Pop out: lirik melayang. Geser untuk memindahkan, klik dua kali untuk mengembalikan";
        StackPanel offRow = Row(btnRom, SB("\u23EA", delegate { SetOffset(offset - 0.5); }), lOff, SB("\u23E9", delegate { SetOffset(offset + 0.5); }), btnPop);
        offRow.HorizontalAlignment = HorizontalAlignment.Center; offRow.Margin = new Thickness(0, 4, 0, 0);
        lHint = T("", 0.4, 9.5); lHint.TextWrapping = TextWrapping.Wrap; lHint.TextAlignment = TextAlignment.Center; lHint.TextTrimming = TextTrimming.None;
        pages[1] = Col(lyricHeader, lyricArea, offRow, lHint);

        // 4: Kalender
        calGrid = new UniformGrid { Columns = 7, Rows = 7, Height = 122 };
        calTitleBtn = SB("", delegate { calY = DateTime.Today.Year; calM = DateTime.Today.Month; calSel = null; BuildCal(); });
        calTitleBtn.Background = Brushes.Transparent; calTitleBtn.FontWeight = FontWeights.SemiBold; calTitleBtn.ToolTip = "Kembali ke bulan ini";
        Button prev = SB("\u25C0", delegate { MoveMonth(-1); });
        Button next = SB("\u25B6", delegate { MoveMonth(1); });
        Grid calNav = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        calNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(prev, 0); Grid.SetColumn(calTitleBtn, 1); Grid.SetColumn(next, 2);
        calNav.Children.Add(prev); calNav.Children.Add(calTitleBtn); calNav.Children.Add(next);
        calInfo = T("", 0.9, 10); calInfo.TextWrapping = TextWrapping.Wrap; calInfo.TextTrimming = TextTrimming.None;
        calInfo.Height = 40; calInfo.Margin = new Thickness(2, 3, 2, 0);
        pages[4] = Col(calNav, calGrid, calInfo);

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
        btnPause = SB("\u25B6", delegate { StartPause(); }); btnPause.ToolTip = "Mulai timer";
        btnPause.FontSize = 22; btnPause.Padding = new Thickness(18, 8, 18, 8); btnPause.MinWidth = 62; btnPause.MinHeight = 48;
        Button btnCancelTimer = SB("\u25A0", delegate { CancelTimer(); }); btnCancelTimer.ToolTip = "Hentikan timer";
        btnCancelTimer.FontSize = 22; btnCancelTimer.Padding = new Thickness(18, 8, 18, 8); btnCancelTimer.MinWidth = 62; btnCancelTimer.MinHeight = 48;
        StackPanel tctl = Row(btnPause, btnCancelTimer);
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


}
