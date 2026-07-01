using iM1os.Domain.Common;

namespace iM1os.Domain.Tenancy;

public sealed class BusinessConfiguration : AuditableEntity, IOrganizationOwned
{
    public Guid OrganizationId { get; set; }

    public decimal DefaultLaborRate { get; set; }

    public decimal DiagnosticRate { get; set; }

    public decimal EmergencyRate { get; set; }

    public decimal WeekendRate { get; set; }

    public decimal EnvironmentalFee { get; set; }

    public decimal ShopSuppliesPercent { get; set; }

    public bool LaborLineItemsTaxable { get; set; } = true;

    public decimal DefaultTaxRate { get; set; }

    public string RegionalTaxOverridesJson { get; set; } = "[]";

    public string NumberSequencesJson { get; set; } = "{}";

    public string NotificationPreferencesJson { get; set; } = "{}";

    public string DepartmentsJson { get; set; } = "[]";

    public string ConnectorPlaceholdersJson { get; set; } = "[]";
}
