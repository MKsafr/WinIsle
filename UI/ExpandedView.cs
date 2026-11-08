using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

partial class Island
{
    // ---------- Tampilan membesar: tombol atas, header, halaman, tombol bawah, ikon fitur ----------
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
        Grid header = new Grid { Margin = new Thickness(34, 2, 22, 2) };
        dateFull = T("", 0.55, 10); dateFull.VerticalAlignment = VerticalAlignment.Center; dateFull.Margin = new Thickness(6, 3, 0, 0);
        StackPanel clk = Row(clockFull, dateFull); clk.HorizontalAlignment = HorizontalAlignment.Left; clk.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(clk); header.Children.Add(tabTitle);

        Grid host = new Grid { Margin = new Thickness(34, 2, 22, 2) };
        foreach (UIElement p in pages) { p.Visibility = Visibility.Collapsed; p.RenderTransform = new TranslateTransform(); host.Children.Add(p); }
        pages[0].Visibility = Visibility.Visible;

        // Ikon fitur (klik ikon untuk loncat ke fitur itu)
        pageIconsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0) };
        for (int i = 0; i < pages.Length; i++)
        {
            int idx = i;
            TextBlock icon = new TextBlock
            {
                Text = tabIcons[i], FontFamily = FF, FontSize = 9, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            Border circle = new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Rgb(38, 38, 38),
                BorderBrush = Rgb(110, 110, 110), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = icon
            };
            tabIconCircles[i] = circle;
            Grid hit = new Grid { Width = 22, Height = 22, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tabNames[i] };
            hit.Children.Add(circle);
            hit.MouseLeftButtonDown += delegate { SwitchTo(idx); };
            pageIconsPanel.Children.Add(hit);
        }

        expRoot = new Grid { Width = OW, Height = OH, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, Opacity = 0 };
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        expRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(up, 0); Grid.SetRow(header, 1); Grid.SetRow(host, 2); Grid.SetRow(down, 3);
        Grid.SetRow(pageIconsPanel, 1); Grid.SetRowSpan(pageIconsPanel, 2);
        expRoot.Children.Add(up); expRoot.Children.Add(header); expRoot.Children.Add(host); expRoot.Children.Add(down); expRoot.Children.Add(pageIconsPanel);
    }

    void Tick(int ms, Action a)
    {
        DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += delegate { try { a(); } catch (Exception ex) { Prog.Log(ex.ToString()); } };
        t.Start();
    }


}
