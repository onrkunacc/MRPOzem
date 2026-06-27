using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    /// <summary>
    /// Gönderim ekranını oluşturmak için gerekli model.
    /// </summary>
    public class ShipmentTemplateFormVm
    {
        public int HospitalID { get; set; }
        public string HospitalName { get; set; } = "";
        public DateTime Month { get; set; }
        public string MonthLabel { get; set; } = "";

        public List<ShipmentTemplateRowVm> Rows { get; set; } = new();

        /// <summary>Bu ay zaten kayıt var mı?</summary>
        public bool HasExistingData => Rows.Any(r => r.MevcutAdet > 0);

        public int ExistingTestCount => Rows.Count(r => r.MevcutAdet > 0);
        public int ExistingTotalAmount => Rows.Sum(r => r.MevcutAdet);
    }

    public class ShipmentTemplateRowVm
    {
        public int TestID { get; set; }
        public string TestName { get; set; } = "";
        public string? SutCode { get; set; }

        public TestGirisTipi GirisTipi { get; set; }
        public int? KutuBasinaAdet { get; set; }

        /// <summary>Bu ay için zaten kayıtlı miktar (varsa)</summary>
        public int MevcutAdet { get; set; }
    }

    /// <summary>
    /// Form submit edildiğinde gelen model.
    /// </summary>
    public class ShipmentTemplateSaveVm
    {
        public int HospitalID { get; set; }
        public DateTime Month { get; set; }

        /// <summary>"add", "overwrite", "skip"</summary>
        public string ConflictMode { get; set; } = "add";

        public List<ShipmentTemplateSaveRowVm> Rows { get; set; } = new();
    }

    public class ShipmentTemplateSaveRowVm
    {
        public int TestID { get; set; }
        public int? Amount { get; set; } // null veya 0 → atla
    }

    /// <summary>
    /// Save sonucu — kullanıcıya gösterilecek özet.
    /// </summary>
    public class ShipmentSaveResult
    {
        public bool Success { get; set; }
        public int EklenenSatir { get; set; }
        public int GuncellenenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public int ToplamAdet { get; set; }
        public string? ErrorMessage { get; set; }
    }
}