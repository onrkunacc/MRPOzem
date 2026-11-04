using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface ILeaveBelgeService
    {
        /// <summary>
        /// Onaylanmış izin için doldurulmuş izin belgesini PDF olarak üretir.
        /// Dosya diske yazılmaz — doğrudan indirilmek üzere byte dizisi döner.
        /// </summary>
        Task<(byte[] Icerik, string DosyaAdi)> OlusturAsync(int leaveRequestId, CancellationToken ct = default);
    }
}
