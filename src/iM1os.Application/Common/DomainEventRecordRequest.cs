namespace iM1os.Application.Common;

public sealed record DomainEventRecordRequest(
    Guid? OrganizationId,
    Guid? LocationId,
    string EntityType,
    string EntityId,
    string EventType,
    string PayloadJson,
    string? CorrelationId,
    string SourceModule);
