using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class PlatformTenant : AuditableEntity
{
    public Guid OrganizationId { get; set; }

    public required string OrganizationName { get; set; }

    public required string Slug { get; set; }

    public required string Status { get; set; }

    public required string SubscriptionPlan { get; set; }

    public required string CurrentVersion { get; set; }

    public required string HealthStatus { get; set; }

    public int ActiveUsers { get; set; }

    public int Locations { get; set; }

    public DateTimeOffset? TrialExpiresAtUtc { get; set; }

    public required string BillingStatus { get; set; }

    public required string ProvisioningStatus { get; set; }
}
