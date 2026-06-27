using iM1os.Domain.Common;

namespace iM1os.Domain.Audit;

public sealed class AuditLog : Entity
{
    public Guid? OrganizationId { get; set; }

    public string? UserId { get; set; }

    public required string Action { get; set; }

    public required string EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? ChangesJson { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
