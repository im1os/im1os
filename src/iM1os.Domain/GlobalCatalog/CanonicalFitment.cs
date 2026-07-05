using iM1os.Domain.Common;

namespace iM1os.Domain.GlobalCatalog;

public sealed class CanonicalFitment : AuditableEntity
{
    public Guid CanonicalItemId { get; set; }

    public int Year { get; set; }

    public required string Make { get; set; }

    public required string MakeKey { get; set; }

    public required string Model { get; set; }

    public required string ModelKey { get; set; }

    public string? VehicleType { get; set; }

    public string? Submodel { get; set; }

    public string SubmodelKey { get; set; } = string.Empty;

    public string? Engine { get; set; }

    public string EngineKey { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
