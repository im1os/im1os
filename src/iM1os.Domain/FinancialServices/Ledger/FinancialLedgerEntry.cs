using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Ledger;

public sealed class FinancialLedgerEntry : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string EntryType { get; set; } = "Payment";

    public string Direction { get; set; } = "In";

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Status { get; set; } = "Posted";

    public string SourceModule { get; set; } = "FinancialServices";

    public string SourceType { get; set; } = "PaymentTransaction";

    public string SourceId { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Provider { get; set; }

    public string? ProviderTransactionId { get; set; }

    public string? Description { get; set; }

    public string? CorrelationId { get; set; }
}
