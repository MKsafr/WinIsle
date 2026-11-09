using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

class PopTimer : Window
{
    const double WidgetWidth = 268, WidgetHeight = 272, CornerRadiusSize = 22;
    const double DragThreshold = 4;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }

    readonly Action togglePause, stopTimer, stopAlarm, onClose;
    readonly TextBlock timeText, statusText;
    readonly Button pauseButton, stopButton, alarmButton;
    readonly Grid root;
    readonly Border panel;
    IntPtr hwnd;
    bool dragPending, dragging, positioned;
    Point dragStart;
    double dragLeft, dragTop;

    public PopTimer(Action togglePause, Action stopTimer, Action stopAlarm, Action onClose)
    {
        this.togglePause = togglePause;
        this.stopTimer = stopTimer;
        this.stopAlarm = stopAlarm;
        this.onClose = onClose;

        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = WidgetWidth; Height = WidgetHeight;
        Title = "WinIsle Timer";

        root = new Grid { Margin = new Thickness(14, 10, 14, 14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock title = new TextBlock
        {
            Text = "\u23F1  TIMER", Foreground = Brushes.White, Opacity = 0.8,
            FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
        };
        Button close = MakeButton("\u2715", Color.FromRgb(38, 38, 38));
        close.MinWidth = 32; close.MinHeight = 30; close.Margin = new Thickness(0);
        close.Padding = new Thickness(4);
        close.ToolTip = "Tutup pop-out timer";
        close.Click += delegate { Hide(); onClose(); };
        Grid header = new Grid { Margin = new Thickness(2, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(title, 0); Grid.SetColumn(close, 1);
        header.Children.Add(title); header.Children.Add(close);
        Grid.SetRow(header, 0);

        statusText = new TextBlock
        {
            Text = "Atur waktu di widget", Foreground = Brushes.White, Opacity = 0.62,
            FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 2)
        };
        Grid.SetRow(statusText, 1);

        timeText = new TextBlock
        {
            Text = "00:00:00", Foreground = Brushes.White,
            FontSize = 34, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(timeText, 2);

        pauseButton = MakeButton("\u25B6  Mulai", Color.FromRgb(30, 144, 255));
        pauseButton.ToolTip = "Mulai timer";
        pauseButton.Click += delegate { this.togglePause(); };
        pauseButton.MinWidth = 100;
        stopButton = MakeButton("\u25A0  Berhenti", Color.FromRgb(55, 55, 60));
        stopButton.ToolTip = "Hentikan timer";
        stopButton.Click += delegate { this.stopTimer(); };
        stopButton.MinWidth = 100;
        StackPanel controls = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        controls.Children.Add(pauseButton);
        controls.Children.Add(stopButton);
        Grid.SetRow(controls, 3);

        alarmButton = MakeButton("\U0001F515  Matikan alarm", Color.FromRgb(220, 65, 65));
        alarmButton.ToolTip = "Matikan alarm";
        alarmButton.Click += delegate { this.stopAlarm(); };
        alarmButton.HorizontalAlignment = HorizontalAlignment.Center;
        alarmButton.Margin = new Thickness(0, 8, 0, 0);
        alarmButton.Visibility = Visibility.Collapsed;
        Grid.SetRow(alarmButton, 4);

        panel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 19, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 57, 64)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CornerRadiusSize),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 3, Opacity = 0.45 },
            Child = root, Cursor = Cursors.SizeAll
        };
        root.Children.Add(header);
        root.Children.Add(statusText);
        root.Children.Add(timeText);
        root.Children.Add(controls);
        root.Children.Add(alarmButton);
        Content = panel;
        PreviewMouseLeftButtonDown += DragDown;
        PreviewMouseMove += DragMove;
        PreviewMouseLeftButtonUp += DragUp;
    }

    static Button MakeButton(string text, Color background)
    {
        ControlTemplate template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "TargetType='Button'><Border x:Name='surface' Background='{TemplateBinding Background}' " +
            "CornerRadius='10' Padding='{TemplateBinding Padding}'><ContentPresenter " +
            "HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'>" +
            "<Setter TargetName='surface' Property='Opacity' Value='0.82'/></Trigger>" +
            "<Trigger Property='IsPressed' Value='True'><Setter TargetName='surface' " +
            "Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'>" +
            "<Setter TargetName='surface' Property='Opacity' Value='0.4'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate>");
        return new Button
        {
            Content = text, MinWidth = 42, MinHeight = 36, Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(8, 4, 8, 4), FontSize = 14,
            Background = new SolidColorBrush(background),
            Foreground = Brushes.White, BorderThickness = new Thickness(0), Template = template,
            FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol"),
            Focusable = false, Cursor = Cursors.Hand
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x80);
    }

    public void Update(string remaining, bool running, bool paused, bool alarmOn, bool canStart)
    {
        timeText.Text = running || paused || canStart ? remaining : "00:00:00";
        statusText.Text = alarmOn ? "WAKTU HABIS" :
            (paused ? "Timer dijeda" : (running ? "SISA WAKTU" :
            (canStart ? "Tekan mulai untuk menjalankan" : "Atur waktu di widget")));
        statusText.Foreground = alarmOn ?
            new SolidColorBrush(Color.FromRgb(255, 115, 115)) : Brushes.White;
        pauseButton.Content = paused ? "\u25B6  Lanjut" : (running ? "\u23F8  Jeda" : "\u25B6  Mulai");
        pauseButton.ToolTip = paused ? "Lanjutkan timer" :
            (running ? "Jeda timer" : "Mulai timer");
        pauseButton.IsEnabled = !alarmOn && (running || paused || canStart);
        stopButton.IsEnabled = !alarmOn && (running || paused);
        pauseButton.Opacity = pauseButton.IsEnabled ? 1 : 0.45;
        stopButton.Opacity = stopButton.IsEnabled ? 1 : 0.45;
        pauseButton.Visibility = alarmOn ? Visibility.Collapsed : Visibility.Visible;
        stopButton.Visibility = alarmOn ? Visibility.Collapsed : Visibility.Visible;
        alarmButton.Visibility = alarmOn ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Place()
    {
        double centerX = positioned ? Left + Width / 2 :
            SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width / 2;
        Place(centerX);
    }

    void Place(double centerX)
    {
        Rect wa = SystemParameters.WorkArea;
        double left = Math.Max(wa.Left, Math.Min(centerX - Width / 2, wa.Right - Width));
        double top = Top;
        if (double.IsNaN(top) || !positioned) top = wa.Bottom - Height - 12;
        top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - Height));
        Left = left; Top = top; positioned = true;
    }

    public void Raise()
    {
        if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13);
    }

    bool CursorDip(out Point point)
    {
        point = new Point();
        NativePoint native;
        if (!GetCursorPos(out native)) return false;
        PresentationSource source = PresentationSource.FromVisual(this);
        if (source == null || source.CompositionTarget == null)
            point = new Point(native.X, native.Y);
        else
            point = source.CompositionTarget.TransformFromDevice.Transform(new Point(native.X, native.Y));
        return true;
    }

    void DragDown(object sender, MouseButtonEventArgs e)
    {
        Point cursor;
        if (e.ChangedButton != MouseButton.Left || !CursorDip(out cursor)) return;
        dragPending = true;
        dragStart = cursor;
        dragLeft = Left; dragTop = Top;
    }

    void DragMove(object sender, MouseEventArgs e)
    {
        if (!dragPending || (e.LeftButton != MouseButtonState.Pressed && !dragging)) return;
        Point cursor;
        if (!CursorDip(out cursor)) return;
        if (!dragging && (Math.Abs(cursor.X - dragStart.X) >= DragThreshold ||
            Math.Abs(cursor.Y - dragStart.Y) >= DragThreshold))
        {
            dragging = true;
            Mouse.Capture(root, CaptureMode.Element);
        }
        if (!dragging) return;
        Rect wa = SystemParameters.WorkArea;
        Left = Math.Max(wa.Left, Math.Min(dragLeft + cursor.X - dragStart.X, wa.Right - Width));
        Top = Math.Max(wa.Top, Math.Min(dragTop + cursor.Y - dragStart.Y, wa.Bottom - Height));
        e.Handled = true;
    }

    void DragUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !dragPending) return;
        dragPending = false;
        if (!dragging) return;
        dragging = false;
        if (Mouse.Captured == root) Mouse.Capture(null);
        e.Handled = true;
    }
}
