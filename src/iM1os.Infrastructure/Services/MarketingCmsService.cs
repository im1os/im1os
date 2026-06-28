using iM1os.Application.Common;
using iM1os.Application.Marketing;
using iM1os.Domain.Marketing;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class MarketingCmsService(IApplicationDbContext dbContext) : IMarketingCmsService
{
    public async Task<MarketingPageDto?> GetPublishedPageAsync(string slug, CancellationToken cancellationToken)
    {
        var page = await dbContext.MarketingPages
            .AsNoTracking()
            .Include(x => x.Blocks)
            .SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished, cancellationToken);

        return page is null ? null : ToDto(page, publishedOnly: true);
    }

    public async Task<IReadOnlyCollection<MarketingPageSummary>> GetPagesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.MarketingPages
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .Select(x => new MarketingPageSummary(x.Id, x.Slug, x.Title, x.NavigationLabel, x.IsPublished, x.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MarketingPageDto?> GetPageAsync(Guid id, CancellationToken cancellationToken)
    {
        var page = await dbContext.MarketingPages
            .AsNoTracking()
            .Include(x => x.Blocks)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return page is null ? null : ToDto(page, publishedOnly: false);
    }

    public async Task<MarketingPageDto> SavePageAsync(SaveMarketingPageRequest request, CancellationToken cancellationToken)
    {
        MarketingPage page;
        if (request.Id is Guid id)
        {
            page = await dbContext.MarketingPages.Include(x => x.Blocks).SingleAsync(x => x.Id == id, cancellationToken);
        }
        else
        {
            page = new MarketingPage { Slug = request.Slug.Trim(), Title = request.Title.Trim(), NavigationLabel = request.NavigationLabel.Trim() };
            dbContext.MarketingPages.Add(page);
        }

        page.Slug = request.Slug.Trim().ToLowerInvariant();
        page.Title = request.Title.Trim();
        page.NavigationLabel = request.NavigationLabel.Trim();
        page.MetaDescription = Clean(request.MetaDescription);
        page.OpenGraphTitle = Clean(request.OpenGraphTitle);
        page.OpenGraphDescription = Clean(request.OpenGraphDescription);
        page.OpenGraphImageUrl = Clean(request.OpenGraphImageUrl);
        page.CanonicalUrl = Clean(request.CanonicalUrl);
        page.UseRawHtmlBody = request.UseRawHtmlBody;
        page.RawHtmlBody = Clean(request.RawHtmlBody);
        page.IsPublished = request.IsPublished;
        page.SortOrder = request.SortOrder;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(page, publishedOnly: false);
    }

    public async Task<MarketingContentBlockDto> SaveBlockAsync(SaveMarketingContentBlockRequest request, CancellationToken cancellationToken)
    {
        MarketingContentBlock block;
        if (request.Id is Guid id)
        {
            block = await dbContext.MarketingContentBlocks.SingleAsync(x => x.Id == id, cancellationToken);
        }
        else
        {
            block = new MarketingContentBlock
            {
                MarketingPageId = request.MarketingPageId,
                BlockType = request.BlockType.Trim(),
                Heading = request.Heading.Trim()
            };
            dbContext.MarketingContentBlocks.Add(block);
        }

        block.MarketingPageId = request.MarketingPageId;
        block.BlockType = request.BlockType.Trim();
        block.Eyebrow = Clean(request.Eyebrow);
        block.Heading = request.Heading.Trim();
        block.Body = Clean(request.Body);
        block.PrimaryActionLabel = Clean(request.PrimaryActionLabel);
        block.PrimaryActionUrl = Clean(request.PrimaryActionUrl);
        block.SecondaryActionLabel = Clean(request.SecondaryActionLabel);
        block.SecondaryActionUrl = Clean(request.SecondaryActionUrl);
        block.ItemsJson = string.IsNullOrWhiteSpace(request.ItemsJson) ? null : request.ItemsJson.Trim();
        block.IsPublished = request.IsPublished;
        block.SortOrder = request.SortOrder;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(block);
    }

    public async Task DeleteBlockAsync(Guid id, CancellationToken cancellationToken)
    {
        var block = await dbContext.MarketingContentBlocks.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (block is null)
        {
            return;
        }

        dbContext.MarketingContentBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CaptureLeadAsync(MarketingLeadRequest request, CancellationToken cancellationToken)
    {
        dbContext.MarketingLeads.Add(new MarketingLead
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Company = Clean(request.Company),
            Phone = Clean(request.Phone),
            Message = Clean(request.Message),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "marketing-site" : request.Source.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MarketingPageDto ToDto(MarketingPage page, bool publishedOnly)
    {
        var blocks = page.Blocks
            .Where(x => !publishedOnly || x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .Select(ToDto)
            .ToArray();

        return new MarketingPageDto(
            page.Id,
            page.Slug,
            page.Title,
            page.NavigationLabel,
            page.MetaDescription,
            page.OpenGraphTitle,
            page.OpenGraphDescription,
            page.OpenGraphImageUrl,
            page.CanonicalUrl,
            page.UseRawHtmlBody,
            page.RawHtmlBody,
            page.IsPublished,
            page.SortOrder,
            blocks);
    }

    private static MarketingContentBlockDto ToDto(MarketingContentBlock block)
    {
        return new MarketingContentBlockDto(
            block.Id,
            block.BlockType,
            block.Eyebrow,
            block.Heading,
            block.Body,
            block.PrimaryActionLabel,
            block.PrimaryActionUrl,
            block.SecondaryActionLabel,
            block.SecondaryActionUrl,
            block.ItemsJson,
            block.IsPublished,
            block.SortOrder);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
