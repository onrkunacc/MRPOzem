using CRMOzem.Business.Interfaces;
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
    public class TenderService : ITenderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenderCalculatorService _calculator;

        public TenderService(ApplicationDbContext context, ITenderCalculatorService calculator)
        {
            _context = context;
            _calculator = calculator;
        }

        public async Task<Tender> CreateAsync(Tender tender)
        {
            try
            {
                _calculator.Calculate(tender);

                await _context.Tenders.AddAsync(tender);
                await _context.SaveChangesAsync();

                return tender;
            }
            catch (Exception ex)
            {
                throw new Exception("İhale oluşturulurken bir hata oluştu.", ex);
            }
        }

        public async Task<Tender> UpdateAsync(Tender tender)
        {
            try
            {
                _calculator.Calculate(tender);

                _context.Tenders.Update(tender);
                await _context.SaveChangesAsync();

                return tender;
            }
            catch (Exception ex)
            {
                throw new Exception("İhale güncellenirken bir hata oluştu.", ex);
            }
        }

        public async Task<Tender?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Tenders
                    .Include(t => t.Tests)
                    .Include(t => t.ExtraCosts)
                    .Include(t => t.Personnels)
                    .FirstOrDefaultAsync(t => t.Id == id);
            }
            catch (Exception ex)
            {
                throw new Exception("İhale verileri getirilirken hata oluştu.", ex);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var tender = await _context.Tenders.FindAsync(id);
                if (tender == null) return false;

                _context.Tenders.Remove(tender);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("İhale silinirken hata oluştu.", ex);
            }
        }
        public async Task<List<Tender>> GetAllAsync()
        {
            try
            {
                return await _context.Tenders
                    .Include(t => t.Tests)
                    .Include(t => t.ExtraCosts)
                    .Include(t => t.Personnels)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("İhale listesi alınırken hata oluştu.", ex);
            }
        }

        public async Task<decimal> CalculateTotalAsync(int tenderId)
        {
            try
            {
                var tender = await GetByIdAsync(tenderId);
                if (tender == null) return 0;

                _calculator.Calculate(tender);

                return tender.OverallCost;
            }
            catch (Exception ex)
            {
                throw new Exception("İhale toplam hesaplanırken hata oluştu.", ex);
            }
        }
    }
}
