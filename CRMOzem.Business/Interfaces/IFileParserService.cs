using CRMOzem.Core.ViewModels;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IFileParserService
    {
        Task<List<StatisticsPreviewItem>> ParseExcelAsync(IFormFile file);
        Task<List<StatisticsPreviewItem>> ParsePdfAsync(IFormFile file);
        Task<List<ShipmentPreviewItem>> ParseShipmentExcelAsync(IFormFile file);
        Task<List<ShipmentPreviewItem>> ParseShipmentPdfAsync(IFormFile file);
    }
}
