# Kota Takip Modülü

Modül, kullanıcıya iki ekran sunar:

1. `Finans / Kota Takip / Veri Girişi`: kota, cari eşleştirmesi ve gider/gelir kayıtları.
2. `Finans / Kota Takip / Finansal Özet`: kota bazında Abbott USD, 3rd party, toplam gider, gelir ve brüt fark.

## Kurulum

Uygulamanın kullandığı SQL Server veritabanına yeni tabloları eklemek için:

```bash
dotnet ef database update --project CRMOzem.DataAccess --startup-project CRMOzem.Web
```

Modüle `Finans`, `Muhasebe` ve `Yönetici` rolleri erişebilir.

## Kullanım sırası

1. Veri girişi ekranındaki **Kota ve müşteri tanımları** bölümünden kotayı bir kez ekleyin.
2. Bizim faturalarımız otomatik gelsin isteniyorsa ilgili cariyi kotaya bağlayın. Aynı cari, çakışan tarih aralıklarında iki farklı kotaya bağlanamaz.
3. Abbott ve 3rd party dâhil alış faturalarını **Yeni finans kaydı** bölümünden girin.
4. **Gelirleri Faturalardan Güncelle** düğmesi, mevcut `Invoices` kayıtlarını cari-kota bağlantısına göre gelir olarak ekler veya günceller.
5. Sonucu **Finansal Özet** ekranından kontrol edin.

## Hesaplama kuralları

- TRY kaydında TL karşılığı doğrudan tutardır.
- USD/EUR kaydında TL karşılığı `Tutar × Döviz Kuru` olarak hesaplanır.
- 3rd party kayıtları ayrı gösterilir ve toplam giderin içinde yer alır.
- Brüt finansal fark `Toplam Gelir - Toplam Gider` değeridir; net kâr değildir.
- Otomatik gelir kayıtları Kota Takip ekranından değiştirilemez. Kaynak fatura güncellendikten sonra senkronizasyon yeniden çalıştırılır.
- Cari-kota bağlantısı olmayan veya birden fazla bağlantıyla çakışan faturaya ait türetilmiş gelir kaydı senkronizasyon sırasında kaldırılır ve kullanıcıya uyarı gösterilir.

## Mikro entegrasyonu

Bu sürüm, MRP Özem içindeki `Invoices` tablosunu güvenli kaynak olarak kullanır. Mikro'dan okuma yapan ayrı bir entegrasyon eklendiğinde Mikro verisi önce bu mevcut fatura akışına alınabilir; Kota Takip modülünde yazma veya iş kuralı değişikliği gerekmez.
