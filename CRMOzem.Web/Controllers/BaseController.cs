using Microsoft.AspNetCore.Mvc;

namespace CRMOzem.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        protected async Task<IActionResult> SafeExecuteAsync(
            Func<Task<IActionResult>> action,
            ILogger logger,
            string? userMessage = null)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled controller exception. Path: {Path}", HttpContext?.Request?.Path.Value);

                if (HttpContext.Request.Headers.TryGetValue("X-Requested-With", out var v) &&
                    v.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { ok = false, message = userMessage ?? "İşlem sırasında hata oluştu." });
                }

                TempData["Error"] = userMessage ?? "İşlem sırasında hata oluştu.";
                return RedirectToAction("Index", "Inventory");
            }
        }
    }
}
