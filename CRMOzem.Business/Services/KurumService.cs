using CRMOzem.Business.Interfaces;
using CRMOzem.Core.DTOs.Istatistik;
using CRMOzem.Core.Models;
using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services
{
    public class KurumService : IKurumService
    {
        private readonly ApplicationDbContext _db;

        public KurumService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<int> AddAsync(AddKurumRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Ad))
                throw new ArgumentException("Kurum adı boş olamaz.");

            var ad = req.Ad.Trim();

            // İstersen aynı isimde kurum engeli:
            var exists = await _db.Kurumlar.AnyAsync(x => x.Ad == ad);
            if (exists)
                throw new InvalidOperationException("Aynı isimde kurum zaten mevcut.");

            var entity = new Kurum
            {
                Ad = ad,
                IhaleTarihi = req.IhaleTarihi,
                IhaleNumarasi = string.IsNullOrWhiteSpace(req.IhaleNumarasi) ? null : req.IhaleNumarasi.Trim(),
                AktifMi = true
            };

            _db.Kurumlar.Add(entity);
            await _db.SaveChangesAsync();

            return entity.Id;
        }
    }
}
