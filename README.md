# WinIsle

Widget "island" mengambang untuk Windows. WinIsle menempel di tepi kanan layar dan menampilkan jam, media yang sedang diputar, lirik, timer, kalender Indonesia, dan beberapa alat kecil lainnya, semuanya dalam satu pil yang bisa dibuka.

Proyek ini adalah adaptasi Windows dari ide yang dipopulerkan oleh **vorssaint** di macOS. Dibuat untuk pemakaian pribadi, dan sebagian kodenya dikembangkan dengan bantuan AI.

## Fitur

WinIsle memiliki sembilan tab. Pindah tab dengan tombol ▲ / ▼ atau ikon fitur di sisi kiri.

| Tab | Isi |
| --- | --- |
| Musik | Informasi lagu, progres, dan kontrol pemutaran |
| Lirik | Lirik tersinkron, offset, romaji/romanisasi, dan pop-out yang dapat diatur |
| Timer | Preset timer dan alarm |
| Suara | Volume utama dan volume per aplikasi |
| Kalender | Kalender Indonesia |
| Scan Teks | Pilih area layar dengan `Alt+Shift+O`, kenali teks secara lokal, lalu salin hasilnya |
| Clipboard | Riwayat salinan dan salin ulang |
| Sistem | CPU, RAM, jaringan, disk, dan baterai |
| Kontrol | Keep-awake, kunci layar, autostart, glow, dan keluar |

Saat island ciut, jam dan judul lagu ditampilkan vertikal. Gunakan `Ctrl+Alt+I` atau ikon tray untuk memindahkan island ke depan atau belakang jendela lain.

## Persyaratan

- Windows 10 atau 11
- .NET Framework 4.x
- Windows PowerShell 5.1

## Download dan menjalankan

1. [Download WinIsle versi terbaru (.zip)](https://github.com/MKsafr/WinIsle/releases/latest/download/WinIsle-latest.zip).
2. Ekstrak ZIP ke folder pilihan, misalnya `Documents\WinIsle`.
3. Buka folder hasil ekstrak, lalu jalankan `WinIsle.exe`.
4. Biarkan `media.ps1`, `kana.ps1`, dan `scantext.ps1` tetap di folder yang sama dengan executable agar fitur media, romanisasi, dan Scan Teks dapat digunakan.

## Build dari source

1. Pastikan Windows PowerShell 5.1 dan .NET Framework 4.x tersedia.
2. Pastikan folder `App`, `Interop`, `UI`, `Features`, serta file `media.ps1`, `kana.ps1`, `scantext.ps1`, dan `WinIsle.ico` tersedia di folder proyek.
3. Jalankan `build.bat`. Skrip akan mencari `csc.exe` dari .NET Framework dan membangun `WinIsle.exe`.
4. Jika WinIsle sedang terbuka, skrip build akan menutup proses tersebut sebelum mengganti executable.
5. Setelah build berhasil, jalankan `WinIsle.exe`.

## Struktur kode

Source code dipisahkan berdasarkan tanggung jawab:

- `App`: entry point aplikasi.
- `Interop`: deklarasi Windows Core Audio.
- `UI`: window utama, tampilan, navigasi, dan pop-out lirik.
- `Features`: implementasi fitur, termasuk subfolder `Lyrics` untuk tampilan, romanisasi, dan pencarian lirik.

File di dalam folder `UI` dan `Features` melengkapi kelas window utama yang sama, sehingga pemisahan ini tidak mengubah alur aplikasi.

## Catatan

- Hanya untuk Windows; beberapa fitur memakai API Windows (Core Audio, OCR Windows, clipboard, registry).
- Lirik dicari dari LRCLIB (tersinkron), lalu NetEase (tersinkron), kemudian lirik polos LRCLIB.
- Jika autostart diaktifkan, entri disimpan di registry pengguna (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
- Jika ada error saat berjalan, catatannya ditulis ke `error.log` di folder aplikasi.

## Lisensi dan atribusi

Proyek ini dilisensikan di bawah **GNU General Public License v3.0**. Teks lengkapnya ada di [LICENSE](LICENSE).

WinIsle terinspirasi dari karya **vorssaint**. Jika kamu berencana membagikan atau mengembangkan proyek ini lebih lanjut, periksa juga lisensi proyek aslinya.
