# VorsWinIsland

VorsWinIsland adalah aplikasi desktop Windows yang terinspirasi dari konsep Dynamic Island yang populer di macOS. Proyek ini dibuat sebagai adaptasi Windows dari ide yang dipopulerkan oleh vorssaint di ekosistem mac. Karya ini merupakan karya turunan yang terinspirasi dari konsep original tersebut, dan tetap mengikuti lisensi open source yang sesuai dengan hak penggunaan karya asal.

Proyek ini juga dibuat dengan bantuan AI untuk mempercepat iterasi dan prototyping. Aplikasi ini menampilkan "island" floating di sisi kanan layar, menampilkan informasi seperti jam, media yang sedang diputar, lirik YouTube, timer, kalender, clipboard, dan informasi sistem. Fungsionalitas utama didukung oleh aplikasi WPF C# yang berjalan di Windows, serta ekstensi browser kecil untuk mengirim lirik dari YouTube ke aplikasi.

## Lisensi dan atribusi

Proyek ini dibuat berdasarkan inspirasi dari Dynamic Island yang pertama kali dipopulerkan oleh vorssaint di macOS. Karena proyek ini merupakan adaptasi/derivasi dari konsep yang berasal dari karya tersebut, kami menambahkan atribusi yang jelas dan menggunakan lisensi yang konsisten untuk pengembangan selanjutnya.

Lisensi yang berlaku untuk proyek ini adalah GNU General Public License v3.0 (GPL-3.0). Silakan lihat [LICENSE](LICENSE) untuk teks lengkap lisensi.

## Fitur utama

- Floating island di sisi kanan layar
- Jam dan kalender
- Pendeteksi media / status pemutaran
- Integrasi lirik dari YouTube
- Timer dan alarm
- Salinan clipboard history
- Informasi CPU, RAM, jaringan, dan baterai
- Kontrol volume dan layar lock
- Dukungan autostart Windows

## Teknologi yang dipakai

- C# / WPF
- .NET Framework bawaan Windows
- JavaScript untuk ekstensi browser
- HTTP lokal untuk komunikasi antara YouTube dan aplikasi desktop

## Prasyarat

Sebelum menjalankan proyek ini, pastikan komputer Anda sudah memenuhi hal berikut:

- Windows 10 atau Windows 11
- .NET Framework yang kompatibel
- Browser Chrome/Edge modern
- Akses untuk menjalankan aplikasi desktop Windows

## Cara menjalankan

### 1) Build aplikasi

Buka command prompt atau PowerShell di folder project, lalu jalankan:

```bat
build.bat
```

Script `build.bat` akan:

- menuju ke folder project
- menemukan compiler C# bawaan Windows (`csc.exe`)
- membuat file executable `VorsWinIsland.exe`

Jika build berhasil, Anda akan melihat pesan bahwa aplikasi berhasil dibuat.

### 2) Jalankan aplikasi

Setelah build selesai, jalankan file berikut:

```bat
VorsWinIsland.exe
```

Atau cukup double-click file `VorsWinIsland.exe` pada folder project.

### 3) Jalankan ekstensi YouTube (opsional untuk lirik)

Untuk fitur lirik YouTube:

1. Buka browser Chrome atau Edge
2. Aktifkan Developer Mode
3. Pilih `Load unpacked`
4. Pilih folder project ini
5. Buka halaman YouTube dan putar lagu/video yang memiliki subtitle/lirik

Aplikasi desktop akan menerima data lirik dari ekstensi melalui endpoint lokal `http://127.0.0.1:47834/lyrics`.

## Struktur project

- `Island.cs` : inti aplikasi desktop WPF
- `build.bat` : script build untuk menghasilkan executable Windows
- `background.js` : service worker ekstensi browser
- `lyrics.js` : logika pengambilan dan parsing lirik dari YouTube
- `manifest.json` : manifest ekstensi browser
- `kana.ps1` dan `media.ps1` : utility script pendukung

## Catatan penting

- Proyek ini dikembangkan khusus untuk Windows.
- Beberapa fitur bergantung pada API Windows dan antarmuka audio yang tersedia pada sistem tersebut.
- Fitur lirik YouTube hanya bekerja jika ekstensi browser aktif dan halaman YouTube dimuat dengan subtitle yang relevan.

## Lisensi

Proyek ini menggunakan prinsip dan ide yang terinspirasi dari konsep Dynamic Island yang dibuat oleh vorssaint di macOS. Silakan cek lisensi dan penggunaan original project bila Anda akan mengembangkan lebih lanjut.

## Pengembang

Proyek ini dibuat sebagai eksperimen desktop Windows yang menggabungkan konsep Dynamic Island dengan kebutuhan penggunaan harian, serta dibuat dengan bantuan AI dalam proses pengembangannya.
