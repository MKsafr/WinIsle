using System;
using System.Windows;
using System.Windows.Controls;

partial class Island
{
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


}
