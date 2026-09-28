# 0.3.4 — 2026-09-28

- Kare simgeye sığdırma sırasında eklenen üst/alt saf siyah şeritler, özgün PNG zemin rengiyle eşleştirildi. Logo kırpılmaz ve oranları korunur.
- 16, 24, 32, 48, 64, 128 ve 256 piksel simge önizlemeleri kontrol edildi.
# 0.3.3 — 2026-09-27

- Logo kullanıcının yeni PNG görseliyle değiştirildi; oranları korunur.
- Derleme her seferinde ICO dosyasını aynı gömülü PNG kaynağından yeniden oluşturur. Böylece kaynak logo değişince EXE simgesinin eski kalması önlenir.
- Önceki kaynak yönetimi, konum kaydı, metin yerleşimi ve filtre kurtarma düzeltmeleri korunmuştur.
# 0.3.2 — 2026-09-27

- Kullanıcının sağladığı kırmızı/mavi logo, siyah zemini ve oranı korunarak uygulama, kurulum ve 16–256 piksel simgelere eklendi.
- Üç ayrı logo çizimi tek gömülü kaynağa alındı; eski çizim yollarındaki serbest bırakılmayan kaynaklar kaldırıldı.
- Düğmelerin her güncellemede yeni yazı tipi oluşturması önlendi; değişen yazı tipleri serbest bırakılır.
- Harici eski app.ico dosyasının güncel gömülü simgeyi geçersiz kılması önlendi.

Kontrol: 15 ayar testi, 11 gamma kurtarma testi, 7 logo boyutu, 20.000 tekrarlı düğme güncellemesi ve 56 yerleşim durumu geçti. Gerçek oyun/Alt+Tab, HDR, ARM64 ve x86 cihaz testi yapılmadı.
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




