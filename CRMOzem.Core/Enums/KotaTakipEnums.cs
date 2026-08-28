using System.ComponentModel.DataAnnotations;

namespace CRMOzem.Core.Enums
{
    public enum KotaKayitTipi
    {
        Gider = 1,
        Gelir = 2
    }

    public enum KotaTedarikciGrubu
    {
        Abbott = 1,

        [Display(Name = "3rd Party")]
        UcuncuParti = 2,

        [Display(Name = "Diğer")]
        Diger = 3,

        [Display(Name = "Özem")]
        Ozem = 4
    }

    public enum KotaGiderKategorisi
    {
        Reaktif = 1,

        [Display(Name = "Kalibratör")]
        Kalibrator = 2,

        Kontrol = 3,

        [Display(Name = "Sarf / Consumable")]
        Sarf = 4,

        Kira = 5,

        Servis = 6,

        AlinIQ = 7,

        [Display(Name = "Dış Laboratuvar")]
        DisLaboratuvar = 8,

        [Display(Name = "Diğer")]
        Diger = 9,

        Gelir = 10
    }

    public enum KotaParaBirimi
    {
        TRY = 1,
        USD = 2,
        EUR = 3
    }

    public enum KotaKayitKaynagi
    {
        Manuel = 1,

        [Display(Name = "Fatura Modülü")]
        FaturaModulu = 2
    }
}
