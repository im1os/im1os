using iM1os.Domain.Common;

namespace iM1os.Domain.Tenancy;

public sealed class BusinessOnboarding : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string BusinessEmail { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "America/Chicago";

    public string BusinessHoursJson { get; set; } = "{}";

    public decimal LaborRate { get; set; }

    public bool SuppliersSkipped { get; set; }

    public bool MerchantServicesSkipped { get; set; }

    public int CompletedSteps { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
