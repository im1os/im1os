using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class SupplierFitmentRecord : AuditableEntity
{
    public Guid SupplierId { get; set; }

    public Guid? SupplierProductId { get; set; }

    public Guid? GlobalProductId { get; set; }

    public Guid? GlobalVehicleId { get; set; }

    public Guid? VehicleFitmentId { get; set; }

    public required string SupplierKey { get; set; }

    public string? SourceSupplierProductId { get; set; }

    public string? SupplierPartNumber { get; set; }

    public required string SupplierSku { get; set; }

    public string? SourceFitmentItemId { get; set; }

    public string? SourceFitmentPartNumber { get; set; }

    public string? MfgPartNumber { get; set; }

    public string? VehicleClass { get; set; }

    public string? VehicleType { get; set; }

    public int Year { get; set; }

    public required string Make { get; set; }

    public required string Model { get; set; }

    public string? Submodel { get; set; }

    public string? Engine { get; set; }

    public string? Notes { get; set; }

    public required string ResolutionStatus { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }
}
