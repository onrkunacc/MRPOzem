namespace CRMOzem.Core.Enums
{
    /// <summary>
    /// Hesaplanan rakama ne kadar güvenilebilir?
    /// Ekranda her satırın yanında gösterilir — "bu rakam neden böyle"
    /// sorusunun ilk cevabı.
    /// </summary>
    public enum VeriGuveni
    {
        Yok = 0,     // hesap yapılamadı
        Dusuk = 1,   // sayım yok ya da çok eski / kullanım verisi yetersiz
        Orta = 2,    // sayım var ama eskimeye başladı ya da son ay raporlanmadı
        Yuksek = 3   // güncel sayım + en az 3 ay raporlanmış kullanım
    }

    /// <summary>Stok rakamı nereden geliyor?</summary>
    public enum StokKaynagi
    {
        Yok = 0,
        FizikiSayim = 1,  // son sayım + sonrasındaki gönderim − kullanım
        KayitFarki = 2    // sayım yok: bugüne kadarki gönderim − kullanım
    }

    /// <summary>Aylık tüketim neye göre hesaplandı?</summary>
    public enum TuketimKaynagi
    {
        Yok = 0,
        Kullanim = 1,   // hastanenin raporladığı test kullanımı
        Gonderim = 2    // kullanım verisi yok: son 6 ayın gönderim ortalaması
    }

    /// <summary>Sipariş kalemi kataloğumuzdaki ürünle nasıl eşleşti?</summary>
    public enum UrunEslesme
    {
        Yok = 0,
        Referans = 1,  // referans (katalog) numarası tuttu
        Sayim = 2,     // fiziki sayımdaki ürün üzerinden bulundu
        Ad = 3         // referans tutmadı, ürün adı birebir aynı
    }
}
