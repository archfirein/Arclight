# ArcLight 0.3.1

Windows için gece modu, renk sıcaklığı ve parlaklık uygulaması.

## İndirme

[Güncel sürümler](https://github.com/archfirein/Arclight/releases)

- `ArcLight-Installer.win-x64.exe`: standart 64 bit Windows kurulumu.
- `ArcLight-Installer.win-x86.exe`: 32 bit Windows kurulumu.
- `ArcLight-Installer.win-arm64.exe`: ARM64 Windows kurulumu.
- `ArcLight.win-*.zip`: aynı uygulamanın taşınabilir paketi.

x64/x86 için .NET Framework 4 veya sonrası gerekir. Yerel ARM64 çalıştırma için
ARM64 destekli .NET Framework 4.8.1 ve uyumlu Windows sürümü gerekir.

Güncellemeden önce ArcLight içindeki **Çık** düğmesini kullanın. Yeni kurulum mevcut
konum ve kullanıcı ayarlarını korur. Ctrl+Alt+N, gece modunu açıp kapatır.

## Düzeltmeler

Konum kaydı atomik dosya değiştirme ve yedek kurtarma kullanır. Gece/gündüz
açıklaması ile klavye kısayolu ayrı satırlarda, metne göre boyutlandırılır.
Ayarlar `%APPDATA%\ArcLight\ayarlar.cfg`, önceki geçerli kayıt ise `.bak` dosyasıdır.

## Kaynak ve derleme

`Program.cs` önceki kurulu sürümden geri elde edilen kaynak üzerine uygulanmış
düzeltmeleri içerir. `Settings.cs` güvenli kayıt ve kurtarmayı içerir.

Windows PowerShell'de `./build-release.ps1` çalıştırın. Betik gerekirse Microsoft'un
NuGet deposundan Roslyn 4.8.0 derleyicisini indirir ve üç mimariyi `release/` içinde
oluşturur. `-Compiler` ile mevcut Roslyn `csc.exe` yolu verilebilir.

Kaynak ZIP ve TAR.GZ aynı kaynakları içerir. `SHA256SUMS.txt` sekiz dağıtım
dosyasının SHA-256 değerlerini içerir.

## Doğrulama

15 ayar testi; 7 dil, iki mod ve farklı yazı boyutlarıyla 56 yerleşim kontrolü geçti.
x64 üzerinde uygulama açılışı, Ctrl+Alt+N ve yeniden açılışta Erzurum konumunun
korunması kontrol edildi. ARM64 ve 32 bit Windows üzerinde cihaz testi yapılmadı.

## Uygulama geçişlerinde filtre kurtarma

Filtre açıkken renk tablosundaki değişimler ölçülüp geri yüklenir. 11 kurtarma testi eklendi. Gerçek oyun/Alt+Tab ve HDR testi henüz yapılmadı; tam ekran geçişinde kısa renk değişimleri olabilir.

