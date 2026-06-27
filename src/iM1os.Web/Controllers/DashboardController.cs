using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

public sealed class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
