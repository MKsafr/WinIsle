# WinIsle

Widget "island" mengambang untuk Windows. WinIsle menempel di tepi kanan layar dan menampilkan jam, media yang sedang diputar, lirik, timer, kalender Indonesia, dan beberapa alat kecil lainnya, semuanya dalam satu pil yang bisa dibuka.

Proyek ini adalah adaptasi Windows dari ide yang dipopulerkan oleh **vorssaint** di macOS. Dibuat untuk pemakaian pribadi, dan sebagian kodenya dikembangkan dengan bantuan AI.

## Fitur

Island punya 8 tab. Pindah antar tab dengan tombol ▲ / ▼ atau titik penunjuk di sisi kiri.

| Tab | Isi |
| --- | --- |
| Musik | Judul, artis, bar progres yang bisa digeser, tombol putar/jeda/sebelumnya/berikutnya |
| Lirik | Lirik tersinkron, penyesuaian offset, mode romaji/romanisasi (Jepang, Korea), dan lirik melayang (pop out) yang bisa digeser |
| Timer | Timer dengan preset, tombol putar/jeda dan berhenti, tambah 1 menit, dan alarm (media yang sedang diputar otomatis dijeda saat alarm berbunyi) |
| Suara | Volume utama dan volume per aplikasi, klik ikon untuk mute |
| Kalender | Kalender Indonesia dengan hari libur nasional dan cuti bersama (lihat di bawah) |
| Clipboard | Riwayat salinan, klik untuk menyalin ulang |
| Sistem | CPU, RAM, kecepatan internet, disk, dan baterai |
| Kontrol | Keep awake, kunci layar, kontrol volume, autostart, dan keluar |

Saat island ciut, jam dan judul lagu tampil vertikal. Ctrl+Alt+I (atau klik ikon di tray) memindahkan island ke belakang/depan jendela lain.

### Kalender dan hari libur nasional

- Hari Minggu dan **libur nasional** berwarna merah, **cuti bersama** berwarna oranye.
- Tekan tanggal libur untuk melihat namanya di bawah kalender (tekan lagi untuk menyembunyikan). Tanggal biasa tidak menampilkan apa-apa.
- Libur tetap (1 Jan, 1 Mei, 1 Jun, 17 Agu, 25 Des) berlaku untuk semua tahun.
- Libur yang tanggalnya berubah tiap tahun (Isra Mikraj, Imlek, Nyepi, Idulfitri, Paskah, Iduladha, Waisak, dan lainnya) sudah tersedia untuk **2026 dan 2027**, berdasarkan SKB 3 Menteri. Untuk tahun lain, hanya libur tetap yang ditandai.
- Tanggal Idulfitri dan Iduladha 2027 mengikuti SKB dan bisa berubah jika Menteri Agama menetapkan lain.

Menambah tahun baru: di `Island.cs`, tambahkan baris `Hd(d, tahun, bulan, tanggal, "Nama libur", cutiBersama);` di dalam `BuildHolidays()` (isi `true` untuk cuti bersama).

### Pop out lirik

Tombol pop out di tab Lirik menampilkan lirik sebagai pil melayang di dekat bawah layar.

- Geser pilnya dengan mouse ke mana saja. Pil tidak bisa digeser keluar dari area kerja layar.
- Nonaktifkan lalu aktifkan lagi pop out untuk mengembalikan pil ke posisi awal; ukuran pil dibuat ringkas agar tidak mengganggu.
- Klik dua kali pada pil untuk mengembalikannya ke posisi awal.
- Bagian yang kosong di sekitar pil tidak menghalangi klik ke aplikasi di bawahnya.

### Alarm timer

Saat timer habis dan alarm berbunyi, semua media yang sedang diputar (Spotify, YouTube di browser, pemutar video, dan sejenisnya) dijeda otomatis supaya alarm langsung terdengar. Media tidak dilanjutkan sendiri setelah alarm dimatikan. Ini hanya berlaku untuk aplikasi yang melapor ke kontrol media Windows; suara yang tidak punya kontrol media, misalnya game, tidak ikut dijeda.

## Prasyarat

- Windows 10 atau 11
- .NET Framework 4.x (sudah bawaan Windows; dipakai lewat `csc.exe`)
- Windows PowerShell 5.1 (bawaan Windows)

## Cara memakai

### 1. Build

Jalankan `build.bat` di folder proyek. Skrip ini mencari `csc.exe` bawaan Windows dan menghasilkan `WinIsle.exe`.

### 2. Jalankan

Buka `WinIsle.exe`. Simpan `media.ps1` dan `kana.ps1` **di folder yang sama** dengan exe-nya, karena keduanya dipakai untuk membaca media dan mengubah kanji ke romaji.

## Struktur proyek

| File | Fungsi |
| --- | --- |
| `Island.cs` | Aplikasi utama (C# + WPF tanpa XAML) |
| `build.bat` | Build ke `WinIsle.exe` |
| `media.ps1` | Membaca dan mengontrol media yang sedang diputar |
| `kana.ps1` | Membaca kanji menjadi hiragana untuk romaji |

## Catatan

- Hanya untuk Windows; beberapa fitur memakai API Windows (Core Audio, clipboard, registry).
- Lirik dicari dari LRCLIB (tersinkron), lalu NetEase (tersinkron), kemudian lirik polos LRCLIB.
- Jika autostart diaktifkan, entri disimpan di registry pengguna (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
- Jika ada error saat berjalan, catatannya ditulis ke `error.log` di folder aplikasi.

## Lisensi dan atribusi

Proyek ini dilisensikan di bawah **GNU General Public License v3.0**. Teks lengkapnya ada di [LICENSE](LICENSE).

WinIsle terinspirasi dari karya **vorssaint**. Jika kamu berencana membagikan atau mengembangkan proyek ini lebih lanjut, periksa juga lisensi proyek aslinya.
