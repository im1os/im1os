using iM1os.Domain.Common;

namespace iM1os.Domain.Marketing;

public sealed class MarketingLead : AuditableEntity
{
    public required string Name { get; set; }

    public required string Email { get; set; }

    public string? Company { get; set; }

    public string? Phone { get; set; }

    public string? Message { get; set; }

    public required string Source { get; set; }

    public string Status { get; set; } = "New";
}
