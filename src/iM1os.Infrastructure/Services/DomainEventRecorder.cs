using iM1os.Application.Common;
using iM1os.Domain.Audit;

namespace iM1os.Infrastructure.Services;

public sealed class DomainEventRecorder(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IDomainEventRecorder
{
    public async Task<Guid> RecordAsync(DomainEventRecordRequest request, CancellationToken cancellationToken = default)
    {
        var organizationId = request.OrganizationId ?? currentUser.OrganizationId;
        if (!organizationId.HasValue)
        {
            throw new InvalidOperationException("Domain events require an organization context.");
        }

        if (string.IsNullOrWhiteSpace(request.EntityType))
        {
            throw new ArgumentException("Entity type is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.EntityId))
        {
            throw new ArgumentException("Entity id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new ArgumentException("Event type is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            throw new ArgumentException("Payload JSON is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SourceModule))
        {
            throw new ArgumentException("Source module is required.", nameof(request));
        }

        var record = new DomainEventRecord
        {
            OrganizationId = organizationId.Value,
            LocationId = request.LocationId,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId.Trim(),
            EventType = request.EventType.Trim(),
            ActorUserId = currentUser.UserId,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            PayloadJson = request.PayloadJson,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId.Trim(),
            SourceModule = request.SourceModule.Trim()
        };

        dbContext.DomainEvents.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return record.Id;
    }
}
