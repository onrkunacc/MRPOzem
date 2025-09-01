using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Areas.Istatistik.Controllers
{
    [Area("Istatistik")]
    [Authorize(Roles = "İstatistik,Yönetici")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
