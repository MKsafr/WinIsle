@echo off
setlocal
cd /d "%~dp0"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" goto nocsc

taskkill /f /im VorsWinIsland.exe >nul 2>&1

"%FW%\csc.exe" /nologo /optimize+ /codepage:65001 /target:winexe /out:VorsWinIsland.exe /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\WindowsBase.dll" /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll Island.cs
if errorlevel 1 goto failed

echo.
echo BERHASIL. Jalankan VorsWinIsland.exe
pause
exit /b 0

:failed
echo.
echo BUILD GAGAL. Salin pesan error di atas dan kirim ke Claude.
pause
exit /b 1

:nocsc
echo csc.exe tidak ditemukan di %FW%
pause
exit /b 1
