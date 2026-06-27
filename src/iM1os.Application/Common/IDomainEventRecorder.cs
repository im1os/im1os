namespace iM1os.Application.Common;

public interface IDomainEventRecorder
{
    Task<Guid> RecordAsync(DomainEventRecordRequest request, CancellationToken cancellationToken = default);
}
