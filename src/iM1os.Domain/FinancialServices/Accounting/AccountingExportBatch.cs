using iM1os.Domain.Common;

namespace iM1os.Domain.FinancialServices.Accounting;

public sealed class AccountingExportBatch : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTimeOffset PeriodStartUtc { get; set; }

    public DateTimeOffset PeriodEndUtc { get; set; }

    public string? ExportReference { get; set; }
}
