using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Models
{
    public class TcmbKur
    {
        public int TcmbKurID { get; set; }

        /// <summary>Kur tarihi (gün seviyesi)</summary>
        public DateTime Tarih { get; set; }

        public decimal UsdAlis { get; set; }
        public decimal UsdSatis { get; set; }

        public decimal? EurAlis { get; set; }
        public decimal? EurSatis { get; set; }

        /// <summary>Efektif alış (banka)</summary>
        public decimal? UsdEfektifAlis { get; set; }
        public decimal? UsdEfektifSatis { get; set; }

        /// <summary>Ne zaman çekildi</summary>
        public DateTime CekilmeZamani { get; set; } = DateTime.Now;

        /// <summary>TCMB verisi mi, manuel giriş mi</summary>
        public bool ManuelGirildi { get; set; } = false;

        [StringLength(500)]
        public string? Not { get; set; }
    }
}
