using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class GlobalVehicle : AuditableEntity
{
    public int Year { get; set; }

    public required string Make { get; set; }

    public required string Model { get; set; }

    public string? VehicleClass { get; set; }

    public string? VehicleType { get; set; }

    public string? Submodel { get; set; }

    public string? Engine { get; set; }

    public string? VinRange { get; set; }

    public string? Market { get; set; }

    public string? Notes { get; set; }
}
