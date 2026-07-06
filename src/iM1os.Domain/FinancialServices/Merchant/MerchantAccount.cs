using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Merchant;

public sealed class MerchantAccount : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string Status { get; set; } = "NotStarted";

    public string UnderwritingStatus { get; set; } = "NotSubmitted";

    public string? LegalBusinessName { get; set; }

    public string? Dba { get; set; }

    public string? Ein { get; set; }

    public string? TaxIdentifierLastFour { get; set; }

    public string? BusinessType { get; set; }

    public string? PhysicalAddressLine1 { get; set; }

    public string? PhysicalAddressLine2 { get; set; }

    public string? PhysicalCity { get; set; }

    public string? PhysicalRegion { get; set; }

    public string? PhysicalPostalCode { get; set; }

    public string? PhysicalCountry { get; set; }

    public string? MailingAddressLine1 { get; set; }

    public string? MailingAddressLine2 { get; set; }

    public string? MailingCity { get; set; }

    public string? MailingRegion { get; set; }

    public string? MailingPostalCode { get; set; }

    public string? MailingCountry { get; set; }

    public string? OwnerName { get; set; }

    public string? OwnerEmail { get; set; }

    public string? OwnerPhone { get; set; }

    public string? BankName { get; set; }

    public string? BankRoutingLastFour { get; set; }

    public string? BankAccountLastFour { get; set; }

    public decimal? ExpectedMonthlyVolume { get; set; }

    public decimal? AverageTicket { get; set; }

    public string? Website { get; set; }

    public string? Mcc { get; set; }

    public string? ProcessingProfile { get; set; }

    public string? SettlementSchedule { get; set; }

    public string? PrimaryProviderCode { get; set; }

    public bool PaymentsEnabled { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset? ActivatedAtUtc { get; set; }

    public DateTimeOffset? RejectedAtUtc { get; set; }
}
