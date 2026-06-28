using System.Diagnostics;
using iM1os.Application.Marketing;
using iM1os.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

public class HomeController(IMarketingCmsService marketingCmsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = await marketingCmsService.GetPublishedPageAsync("home", cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        ViewData["MarketingSite"] = true;
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Page(string slug, CancellationToken cancellationToken)
    {
        var page = await marketingCmsService.GetPublishedPageAsync(slug, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        ViewData["MarketingSite"] = true;
        return View("Index", page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDemo(MarketingLeadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            TempData["MarketingLeadStatus"] = "Please provide your name and email.";
            return RedirectToAction(nameof(Index), null, null, "contact");
        }

        await marketingCmsService.CaptureLeadAsync(request with { Source = "request-demo" }, cancellationToken);
        TempData["MarketingLeadStatus"] = "Thanks. The iM1 team will follow up shortly.";
        return RedirectToAction(nameof(Index), null, null, "contact");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
