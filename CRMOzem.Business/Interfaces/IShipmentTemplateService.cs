using CRMOzem.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IShipmentTemplateService
    {
        /// <summary>
        /// Hastanenin aktif şablonuna göre gönderim formunu hazırlar.
        /// Mevcut ay verisi varsa onu da getirir.
        /// </summary>
        Task<ShipmentTemplateFormVm> BuildFormAsync(int hospitalId, DateTime month, CancellationToken ct = default);

        /// <summary>
        /// Toplu gönderim kaydeder. Mode'a göre üzerine ekler / yeniden yazar.
        /// </summary>
        Task<ShipmentSaveResult> SaveAsync(ShipmentTemplateSaveVm model, CancellationToken ct = default);
    }
}