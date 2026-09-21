# 🌙 ArcLight - Smart Night Light & Blue Light Filter

<p align="center">
  <strong>Windows için Hafif, Modern ve Akıllı Mavi Işık Filtresi</strong><br>
  <em>Lightweight, Modern & Smart Blue Light Filter for Windows (x64, x86, ARM64)</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/Architecture-x64%20%7C%20x86%20%7C%20ARM64-orange?style=flat-square" alt="Arch">
  <img src="https://img.shields.io/badge/Language-C%23-green?style=flat-square" alt="Language">
  <img src="https://img.shields.io/badge/License-MIT-lightgrey?style=flat-square" alt="License">
</p>

---

## ✨ Özellikler / Features

- 💻 **Donanımsal Gamma Kontrolü (Hardware Gamma Ramp):** Ekranın üzerine sahte filtre katmanı koymaz; doğrudan Windows donanım renk motorunu kontrol eder. Oyunlarda ve videolarda sıfır FPS/performans kaybı yaşatır.
- 📍 **Akıllı Lokasyon & Güneş Takibi:** Şehrinizi seçtiğinizde koordinatlara göre günlük gün doğumu ve gün batımı saatlerini hesaplar. Akşam otomatik açılır, sabah otomatik kapanır.
- ⏰ **Özel Saat Aralığı:** İsteğe bağlı olarak dilediğiniz başlangıç ve bitiş saatini belirleyebilirsiniz.
- 🌐 **7 Dil Desteği:** 🇹🇷 Türkçe, 🇬🇧 English, 🇪🇸 Español, 🇫🇷 Français, 🇨🇳 简体中文, 🇯🇵 日本語, 🇰🇷 한국어.
- ⚡ **Tek Tıkla Kontrol:** Arayüzdeki büyük butondan veya görev çubuğundaki / saatin yanındaki sistem tepsisi simgesinden tek tıkla açıp kapatma.
- 🔥 **Geniş Renk Sıcaklığı (1800K - 6500K):** Rahat okuma, mum ışığı veya akşam modları.
- ☀️ **Ekran Parlaklığı Ayarı (%25 - %100):** Gece gözü yormayan yumuşak karartma.


---

## 📦 İndirme / Download

En son sürümü doğrudan [Releases (Sürümler)](../../releases) sayfasından indirebilirsiniz:

- **ArcLight-Installer.win-x64.exe** - Standart 64-bit Windows bilgisayarlar için kurulumcu
- **ArcLight-Installer.win-arm64.exe** - Snapdragon / ARM tabanlı cihazlar için
- **ArcLight-Installer.win-x86.exe** - 32-bit eski sistemler için
- **ArcLight.win-x64.zip** - Kurulumsuz taşınabilir (Portable) sürüm

---

## 🛠️ Kurulum ve Derleme (Build from Source)

Windows'un yerleşik C# derleyicisi (`csc.exe`) ile harici hiçbir araç gerekmeden derlenebilir:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:app.ico /out:ArcLight.exe /r:System.Windows.Forms.dll,System.Drawing.dll Program.cs
