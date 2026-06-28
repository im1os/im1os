using iM1os.Application.Marketing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Web.Controllers;

[Authorize(Roles = "Platform Administrator")]
public sealed class MarketingAdminController(IMarketingCmsService marketingCmsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await marketingCmsService.GetPagesAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Page(Guid? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return View(new SaveMarketingPageRequest(null, string.Empty, string.Empty, string.Empty, null, null, null, null, null, false, 0));
        }

        var page = await marketingCmsService.GetPageAsync(id.Value, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        ViewBag.PageBlocks = page.Blocks;
        return View(new SaveMarketingPageRequest(page.Id, page.Slug, page.Title, page.NavigationLabel, page.MetaDescription, page.OpenGraphTitle, page.OpenGraphDescription, page.OpenGraphImageUrl, page.CanonicalUrl, page.IsPublished, page.SortOrder));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Page(SaveMarketingPageRequest request, CancellationToken cancellationToken)
    {
        var page = await marketingCmsService.SavePageAsync(request, cancellationToken);
        return RedirectToAction(nameof(Page), new { id = page.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Block(Guid pageId, Guid? id, string? blockType, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            var type = string.IsNullOrWhiteSpace(blockType) ? "feature" : blockType.Trim().ToLowerInvariant();
            var heading = type == "html" ? "Raw HTML" : string.Empty;
            return View(new SaveMarketingContentBlockRequest(null, pageId, type, null, heading, null, null, null, null, null, null, true, 0));
        }

        var page = await marketingCmsService.GetPageAsync(pageId, cancellationToken);
        var block = page?.Blocks.SingleOrDefault(x => x.Id == id.Value);
        if (block is null)
        {
            return NotFound();
        }

        return View(new SaveMarketingContentBlockRequest(block.Id, pageId, block.BlockType, block.Eyebrow, block.Heading, block.Body, block.PrimaryActionLabel, block.PrimaryActionUrl, block.SecondaryActionLabel, block.SecondaryActionUrl, block.ItemsJson, block.IsPublished, block.SortOrder));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(SaveMarketingContentBlockRequest request, CancellationToken cancellationToken)
    {
        await marketingCmsService.SaveBlockAsync(request, cancellationToken);
        return RedirectToAction(nameof(Page), new { id = request.MarketingPageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlock(Guid id, Guid pageId, CancellationToken cancellationToken)
    {
        await marketingCmsService.DeleteBlockAsync(id, cancellationToken);
        return RedirectToAction(nameof(Page), new { id = pageId });
    }
}
