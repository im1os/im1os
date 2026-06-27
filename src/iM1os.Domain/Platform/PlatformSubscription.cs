using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class PlatformSubscription : AuditableEntity
{
    public Guid OrganizationId { get; set; }

    public required string Plan { get; set; }

    public required string BillingStatus { get; set; }

    public bool IsTrial { get; set; }

    public DateTimeOffset? TrialExpiresAtUtc { get; set; }

    public string? BillingProviderCustomerId { get; set; }
}
