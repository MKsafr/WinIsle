// WinIsle - adaptasi Windows dari ide "island" (pil mengambang) yang dipopulerkan oleh vorssaint.
// Proyek ini terinspirasi dari karya original tersebut dan menyertakan atribusi lisensi yang sesuai.
// C# 5 + WPF tanpa XAML. Dikompilasi dengan csc.exe bawaan Windows (lihat build.bat).
// Island menempel di tepi KANAN layar, di tengah secara vertikal.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Win32 = Microsoft.Win32;
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.1.0")]

static class Prog
{
    public static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), DateTime.Now.ToString("s") + " " + s + Environment.NewLine); }
        catch { }
    }

    [STAThread]
    static void Main()
    {
        bool created;
        System.Threading.Mutex m = new System.Threading.Mutex(true, "WinIsle", out created);
        if (!created) return;
        Application app = new Application();
        app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
        {
            Log(e.Exception.ToString());
            e.Handled = true;
        };
        app.Run(new Island());
        GC.KeepAlive(m);
    }
}
