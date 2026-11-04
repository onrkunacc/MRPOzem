using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Enums
{
    /// <summary>
    /// Karşılaştırmadaki iki taraftan hangisi.
    /// Tedarikci = Abbott (USD), Biz = ÖZEM (TL).
    /// </summary>
    public enum KarsilastirmaTarafi
    {
        Tedarikci = 1,
        Biz = 2
    }

    /// <summary>
    /// İki taraftaki satırların hangi kurala göre eşleştirileceği.
    /// </summary>
    public enum EslestirmeKurali
    {
        /// <summary>Referans numarası + aynı takvim ayı. (Varsayılan)</summary>
        ReferansVeAy = 1,

        /// <summary>Sadece referans numarası — tarihe bakılmaz.</summary>
        SadeceReferans = 2,

        /// <summary>Referans numarası + birebir aynı gün.</summary>
        ReferansVeTarih = 3,

        /// <summary>Referans numarası + bizim fatura tarihimizden önceki en yakın tedarikçi fiyatı.</summary>
        ReferansVeEnYakinOnceki = 4
    }

    /// <summary>
    /// USD → TL dönüşümünde kullanılacak kurun kaynağı.
    /// </summary>
    public enum KurKaynagi
    {
        /// <summary>Her satır için fatura tarihindeki TCMB döviz satış kuru. (Varsayılan)</summary>
        TcmbFaturaTarihi = 1,

        /// <summary>Karşılaştırma için elle girilen tek sabit kur.</summary>
        SabitKur = 2,

        /// <summary>Excel dosyasındaki kur kolonu.</summary>
        ExcelKolonu = 3
    }

    /// <summary>
    /// Karşılaştırmanın yaşam döngüsü.
    /// </summary>
    public enum KarsilastirmaDurumu
    {
        /// <summary>Oluşturuldu, henüz dosya yüklenmedi.</summary>
        Taslak = 1,

        /// <summary>Dosyalardan en az biri yüklendi, hesaplama bekliyor.</summary>
        DosyaBekliyor = 2,

        /// <summary>Her iki dosya yüklendi ve hesaplandı.</summary>
        Hesaplandi = 3,

        /// <summary>Kapatıldı — arşiv.</summary>
        Arsiv = 4
    }

    /// <summary>
    /// Bir sonuç satırının eşleşme durumu.
    /// </summary>
    public enum SonucDurumu
    {
        /// <summary>Her iki tarafta da bulundu.</summary>
        Eslesti = 1,

        /// <summary>Sadece tedarikçi (Abbott) tarafında var.</summary>
        SadeceTedarikci = 2,

        /// <summary>Sadece bizim tarafta var.</summary>
        SadeceBiz = 3
    }

    /// <summary>
    /// Fark yönü. Pozitif fark = bizim birim fiyatımız tedarikçininkinden yüksek.
    /// </summary>
    public enum FarkYonu
    {
        /// <summary>Fark tolerans aralığında — anlamlı sapma yok.</summary>
        Esit = 1,

        /// <summary>Bizim fiyatımız yüksek.</summary>
        BizYuksek = 2,

        /// <summary>Tedarikçi fiyatı yüksek.</summary>
        TedarikciYuksek = 3
    }
}
