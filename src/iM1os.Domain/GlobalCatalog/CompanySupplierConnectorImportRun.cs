using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CompanySupplierConnectorImportRun : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public Guid CompanySupplierConnectorConfigurationId { get; set; }

    public required string ImportType { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? RequestedByUserId { get; set; }

    public string? Source { get; set; }

    public string? ParametersJson { get; set; }

    public string? Message { get; set; }

    public int ProgressProcessed { get; set; }

    public int? ProgressTotal { get; set; }
}
