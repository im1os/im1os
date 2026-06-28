namespace iM1os.Application.Marketing;

public sealed record MarketingPageDto(
    Guid Id,
    string Slug,
    string Title,
    string NavigationLabel,
    string? MetaDescription,
    string? OpenGraphTitle,
    string? OpenGraphDescription,
    string? OpenGraphImageUrl,
    string? CanonicalUrl,
    bool IsPublished,
    int SortOrder,
    IReadOnlyCollection<MarketingContentBlockDto> Blocks);

public sealed record MarketingContentBlockDto(
    Guid Id,
    string BlockType,
    string? Eyebrow,
    string Heading,
    string? Body,
    string? PrimaryActionLabel,
    string? PrimaryActionUrl,
    string? SecondaryActionLabel,
    string? SecondaryActionUrl,
    string? ItemsJson,
    bool IsPublished,
    int SortOrder);

public sealed record MarketingPageSummary(Guid Id, string Slug, string Title, string NavigationLabel, bool IsPublished, int SortOrder);

public sealed record SaveMarketingPageRequest(
    Guid? Id,
    string Slug,
    string Title,
    string NavigationLabel,
    string? MetaDescription,
    string? OpenGraphTitle,
    string? OpenGraphDescription,
    string? OpenGraphImageUrl,
    string? CanonicalUrl,
    bool IsPublished,
    int SortOrder);

public sealed record SaveMarketingContentBlockRequest(
    Guid? Id,
    Guid MarketingPageId,
    string BlockType,
    string? Eyebrow,
    string Heading,
    string? Body,
    string? PrimaryActionLabel,
    string? PrimaryActionUrl,
    string? SecondaryActionLabel,
    string? SecondaryActionUrl,
    string? ItemsJson,
    bool IsPublished,
    int SortOrder);

public sealed record MarketingLeadRequest(string Name, string Email, string? Company, string? Phone, string? Message, string Source);
