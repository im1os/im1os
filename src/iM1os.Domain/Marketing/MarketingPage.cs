using iM1os.Domain.Common;

namespace iM1os.Domain.Marketing;

public sealed class MarketingPage : AuditableEntity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required string NavigationLabel { get; set; }

    public string? MetaDescription { get; set; }

    public string? OpenGraphTitle { get; set; }

    public string? OpenGraphDescription { get; set; }

    public string? OpenGraphImageUrl { get; set; }

    public string? CanonicalUrl { get; set; }

    public bool UseRawHtmlBody { get; set; }

    public string? RawHtmlBody { get; set; }

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public ICollection<MarketingContentBlock> Blocks { get; } = new List<MarketingContentBlock>();
}
