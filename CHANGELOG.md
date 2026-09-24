# 0.3.1 — 2026-09-24

- Filtre açıkken ekranın gamma tablosu 500 ms aralıklarla kontrol edilir. Oyun veya uygulama geçişinde ölçülen değişiklik geri yüklenir.
- Renk geçişi sürerken ve filtre kapalıyken kurtarma yazımı yapılmaz.
- Sürücü başarısızlığı ya da erişilemeyen ekran durumunda yeniden denemeler kademeli olarak seyrekleşir; sonuç tekrar okunarak doğrulanır.
- Önceki konum kaydı ve metin yerleşimi düzeltmeleri korunmuştur.

Doğrulama: 11 gamma kurtarma testi ve 15 ayar testi. Gerçek oyun/Alt+Tab, HDR, ARM64 ve 32 bit Windows cihaz testleri yapılmadı. Tam ekran geçişindeki çok kısa renk değişimleri tamamen önlenemeyebilir.
# 0.3 — 2026-09-23

- Konum seçiminin kaybolmasına yol açan eski ayar dosyasını silme adımı kaldırıldı.
  Kayıt diske aktarıldıktan sonra atomik olarak değiştirilir ve yedek tutulur.
- Eksik/bozuk ayar dosyası için tamamlanmış geçici dosya ve yedekten kurtarma eklendi.
- Geçersiz ayar ve saat değerlerinin güvenli varsayılanları bozması önlendi.
- Gece modu açıklaması ve Ctrl+Alt+N bilgisi ayrı, metne göre büyüyen alanlara alındı.
- Kurulumdaki Tamamla düğmesinin kurulumu ikinci kez çalıştırması düzeltildi.
- Kurulum artık çalışan uygulamayı zorla sonlandırmaz; güvenli çıkış yapılmasını ister.
- x64, x86 ve ARM64 kurulum/taşınabilir paketleri aynı güncel kaynakla derlendi.

Doğrulama: 15 ayar testi, 56 yerleşim kontrolü ve x64 uygulama yeniden açılış testi.
Tam Windows yeniden başlatma, ARM64 ve 32 bit Windows cihaz testleri yapılmadı.

