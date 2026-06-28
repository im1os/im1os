using iM1os.Domain.Common;

namespace iM1os.Domain.Marketing;

public sealed class MarketingContentBlock : AuditableEntity
{
    public Guid MarketingPageId { get; set; }

    public required string BlockType { get; set; }

    public string? Eyebrow { get; set; }

    public required string Heading { get; set; }

    public string? Body { get; set; }

    public string? PrimaryActionLabel { get; set; }

    public string? PrimaryActionUrl { get; set; }

    public string? SecondaryActionLabel { get; set; }

    public string? SecondaryActionUrl { get; set; }

    public string? ItemsJson { get; set; }

    public bool IsPublished { get; set; } = true;

    public int SortOrder { get; set; }
}
