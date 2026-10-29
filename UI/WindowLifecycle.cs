using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using System.Windows.Threading;

partial class Island
{
    // =====================================================================
    //  WINDOW: gaya, hotkey, hook
    // =====================================================================
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000080); // NOACTIVATE + TOOLWINDOW
        HwndSource.FromHwnd(hwnd).AddHook(Hook);
        RegisterHotKey(hwnd, 1, 0x4003, 0x49); // Ctrl+Alt+I
        AddClipboardFormatListener(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try { UnregisterHotKey(hwnd, 1); RemoveClipboardFormatListener(hwnd); } catch { }
        KillMedia();
        try { if (pop != null) pop.Close(); } catch { }
        try { tray.Visible = false; tray.Dispose(); } catch { }
    }

    IntPtr Hook(IntPtr h, int m, IntPtr w, IntPtr l, ref bool handled)
    {
        if (m == 0x0312) { ToggleBack(); handled = true; }          // WM_HOTKEY
        else if (m == 0x031D) { clipDirty = true; clipTries = 0; }  // WM_CLIPBOARDUPDATE
        return IntPtr.Zero;
    }

    void ExitApp()
    {
        quitting = true;
        KillMedia();
        try { tray.Visible = false; } catch { }
        Application.Current.Shutdown();
    }

    void ToggleBack()
    {
        back = !back;
        if (back) { SetOpen(false); Topmost = false; SetWindowPos(hwnd, (IntPtr)1, 0, 0, 0, 0, 0x13); }
        else { Topmost = true; SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, 0x13); }
    }


}
