using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

partial class Island
{
    // =====================================================================
    //  KALENDER
    // =====================================================================
    void MoveMonth(int delta)
    {
        calM += delta;
        if (calM > 12) { calM = 1; calY++; }
        if (calM < 1) { calM = 12; calY--; }
        calSel = null;
        BuildCal();
    }

    void BuildCal()
    {
        calGrid.Children.Clear();
        string[] dn = { "Sen", "Sel", "Rab", "Kam", "Jum", "Sab", "Min" };
        for (int i = 0; i < 7; i++)
        {
            TextBlock h = T(dn[i], i == 6 ? 0.9 : 0.5, 9.5);
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
            DateTime dd = new DateTime(calY, calM, day);
            Hol hol = HolidayOf(dd);
            TextBlock t = T(day.ToString(), 1, 11);
            t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            bool isToday = (dd == today);
            Border b = new Border
            {
                Child = t, CornerRadius = new CornerRadius(9), Margin = new Thickness(1),
                Background = isToday ? (Brush)Accent : Brushes.Transparent
            };
            if (hol != null) b.Cursor = Cursors.Hand;
            if (hol != null) t.FontWeight = FontWeights.SemiBold;
            if (!isToday)
            {
                if (hol != null) t.Foreground = hol.Cuti ? Orange : Reddish;   // merah = libur nasional, oranye = cuti bersama
                else if ((off + day - 1) % 7 == 6) t.Foreground = Reddish;      // Minggu
            }
            // Tanggal libur: tekan untuk menampilkan keterangan (tekan lagi untuk menyembunyikan). Tanggal biasa: tidak ada teks.
            b.MouseLeftButtonDown += delegate { calSel = (hol != null && !(calSel.HasValue && calSel.Value == dd)) ? (DateTime?)dd : null; BuildCal(); };
            calGrid.Children.Add(b);
        }
        calTitleBtn.Content = first.ToString("MMMM yyyy", idCulture);
        UpdateCalInfo();
    }

    // Keterangan di bawah kalender: hanya muncul kalau tanggal libur sedang ditekan.
    void UpdateCalInfo()
    {
        calInfo.Inlines.Clear();
        if (!calSel.HasValue) return;
        DateTime d = calSel.Value;
        Hol h = HolidayOf(d);
        if (h == null) return;
        System.Windows.Documents.Run r1 = new System.Windows.Documents.Run(d.ToString("dddd, d MMMM yyyy", idCulture));
        r1.FontWeight = FontWeights.SemiBold;
        System.Windows.Documents.Run r2 = new System.Windows.Documents.Run((h.Cuti ? "Cuti bersama: " : "") + h.Name);
        r2.Foreground = h.Cuti ? Orange : Reddish;
        calInfo.Inlines.Add(r1);
        calInfo.Inlines.Add(new System.Windows.Documents.LineBreak());
        calInfo.Inlines.Add(r2);
    }

    // ---------- Hari libur nasional Indonesia ----------
    // Sumber: SKB 3 Menteri (Menag, Menaker, MenPANRB) tentang Hari Libur Nasional dan Cuti Bersama.
    //   2026: SKB No. 1497/2025, 2/2025, 5/2025 (19 Sep 2025)   2027: SKB No. 1205/2026, 3/2026, 2/2026 (15 Sep 2026)
    // Libur tetap (tanggalnya sama tiap tahun) dihitung otomatis untuk semua tahun.
    // Libur yang berpindah-pindah (keagamaan, Imlek, Nyepi, dst.) harus diisi per tahun di BuildHolidays().
    // Tanggal Idulfitri/Iduladha 1448 H (2027) mengikuti SKB dan bisa berubah
    // kalau nanti ditetapkan lain oleh Menteri Agama.
    class Hol { public string Name; public bool Cuti; }

    static readonly Dictionary<DateTime, Hol> HolMoving = BuildHolidays();

    static string FixedHoliday(DateTime d)
    {
        switch (d.Month * 100 + d.Day)
        {
            case 101: return "Tahun Baru Masehi";
            case 501: return "Hari Buruh Internasional";
            case 601: return "Hari Lahir Pancasila";
            case 817: return "Proklamasi Kemerdekaan RI";
            case 1225: return "Hari Raya Natal";
        }
        return null;
    }

    static Hol HolidayOf(DateTime d)
    {
        Hol m;
        HolMoving.TryGetValue(d.Date, out m);
        string f = FixedHoliday(d);
        if (f == null) return m;
        return new Hol { Name = m == null ? f : f + " \u2022 " + m.Name, Cuti = false };
    }

    // Hd(kamus, tahun, bulan, tanggal, nama, cutiBersama)
    static void Hd(Dictionary<DateTime, Hol> d, int y, int m, int day, string name, bool cuti)
    {
        d[new DateTime(y, m, day)] = new Hol { Name = name, Cuti = cuti };
    }

    static Dictionary<DateTime, Hol> BuildHolidays()
    {
        Dictionary<DateTime, Hol> d = new Dictionary<DateTime, Hol>();

        // ===== 2026 =====
        Hd(d, 2026, 1, 16, "Isra Mikraj Nabi Muhammad SAW", false);
        Hd(d, 2026, 2, 16, "Tahun Baru Imlek 2577 Kongzili", true);
        Hd(d, 2026, 2, 17, "Tahun Baru Imlek 2577 Kongzili", false);
        Hd(d, 2026, 3, 18, "Hari Suci Nyepi (Tahun Baru Saka 1948)", true);
        Hd(d, 2026, 3, 19, "Hari Suci Nyepi (Tahun Baru Saka 1948)", false);
        Hd(d, 2026, 3, 20, "Hari Raya Idulfitri 1447 H", true);
        Hd(d, 2026, 3, 21, "Hari Raya Idulfitri 1447 H", false);
        Hd(d, 2026, 3, 22, "Hari Raya Idulfitri 1447 H", false);
        Hd(d, 2026, 3, 23, "Hari Raya Idulfitri 1447 H", true);
        Hd(d, 2026, 3, 24, "Hari Raya Idulfitri 1447 H", true);
        Hd(d, 2026, 4, 3, "Wafat Yesus Kristus", false);
        Hd(d, 2026, 4, 5, "Hari Kebangkitan Yesus Kristus (Paskah)", false);
        Hd(d, 2026, 5, 14, "Kenaikan Yesus Kristus", false);
        Hd(d, 2026, 5, 15, "Kenaikan Yesus Kristus", true);
        Hd(d, 2026, 5, 27, "Hari Raya Iduladha 1447 H", false);
        Hd(d, 2026, 5, 28, "Hari Raya Iduladha 1447 H", true);
        Hd(d, 2026, 5, 31, "Hari Raya Waisak 2570 BE", false);
        Hd(d, 2026, 6, 16, "Tahun Baru Islam 1 Muharam 1448 H", false);
        Hd(d, 2026, 8, 25, "Maulid Nabi Muhammad SAW", false);
        Hd(d, 2026, 12, 24, "Hari Raya Natal", true);

        // ===== 2027 =====
        Hd(d, 2027, 1, 5, "Isra Mikraj Nabi Muhammad SAW (1448 H)", false);
        Hd(d, 2027, 2, 5, "Tahun Baru Imlek 2578 Kongzili", true);
        Hd(d, 2027, 2, 6, "Tahun Baru Imlek 2578 Kongzili", false);
        Hd(d, 2027, 3, 8, "Hari Suci Nyepi (Tahun Baru Saka 1949)", false);
        Hd(d, 2027, 3, 9, "Hari Raya Idulfitri 1448 H", true);
        Hd(d, 2027, 3, 10, "Hari Raya Idulfitri 1448 H", false);
        Hd(d, 2027, 3, 11, "Hari Raya Idulfitri 1448 H", false);
        Hd(d, 2027, 3, 12, "Hari Raya Idulfitri 1448 H", true);
        Hd(d, 2027, 3, 15, "Hari Raya Idulfitri 1448 H", true);
        Hd(d, 2027, 3, 25, "Wafat Yesus Kristus", true);
        Hd(d, 2027, 3, 26, "Wafat Yesus Kristus", false);
        Hd(d, 2027, 3, 28, "Hari Kebangkitan Yesus Kristus (Paskah)", false);
        Hd(d, 2027, 5, 6, "Kenaikan Yesus Kristus", false);
        Hd(d, 2027, 5, 17, "Hari Raya Iduladha 1448 H", false);
        Hd(d, 2027, 5, 18, "Hari Raya Iduladha 1448 H", true);
        Hd(d, 2027, 5, 19, "Hari Raya Waisak 2571 BE", true);
        Hd(d, 2027, 5, 20, "Hari Raya Waisak 2571 BE", false);
        Hd(d, 2027, 6, 6, "Tahun Baru Islam 1 Muharam 1449 H", false);
        Hd(d, 2027, 8, 15, "Maulid Nabi Muhammad SAW", false);
        Hd(d, 2027, 12, 24, "Hari Raya Natal", true);
        Hd(d, 2027, 12, 26, "Isra Mikraj Nabi Muhammad SAW (1449 H)", false);

        return d;
    }


}
