using iM1os.Domain.Common;

namespace iM1os.Domain.Vehicles;

public sealed class CustomerVehicle : AuditableEntity, IOrganizationOwned, ILocationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid CustomerId { get; set; }

    public string Type { get; set; } = "Off-Road";

    public int? Year { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? Trim { get; set; }

    public string? Vin { get; set; }

    public string? Color { get; set; }

    public string? TagPlate { get; set; }

    public decimal? Mileage { get; set; }

    public decimal? MileageIn { get; set; }

    public decimal? MileageOut { get; set; }

    public decimal? Hours { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
