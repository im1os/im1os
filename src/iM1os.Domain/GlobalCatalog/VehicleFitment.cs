using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class VehicleFitment : AuditableEntity
{
    public Guid GlobalProductId { get; set; }

    public Guid GlobalVehicleId { get; set; }

    public int Quantity { get; set; }

    public string? Position { get; set; }

    public string? Notes { get; set; }
}
