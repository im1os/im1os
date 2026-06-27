using iM1os.Application.Common;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class DomainEventRecorderTests
{
    [Fact]
    public async Task RecordAsync_persists_immutable_business_event_with_context()
    {
        var organizationId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
        var currentUser = new TestCurrentUser("user-1", organizationId);
        var clock = new TestClock(now);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options, currentUser, clock, new TenantProvider(currentUser));
        var recorder = new DomainEventRecorder(dbContext, currentUser, clock);

        var eventId = await recorder.RecordAsync(new DomainEventRecordRequest(
            OrganizationId: null,
            LocationId: locationId,
            EntityType: "WorkOrder",
            EntityId: "WO-1001",
            EventType: "WorkOrderCreated",
            PayloadJson: "{\"workOrderNumber\":\"WO-1001\"}",
            CorrelationId: "corr-1",
            SourceModule: "Service"));

        var record = await dbContext.DomainEvents.SingleAsync(x => x.Id == eventId);

        Assert.Equal(organizationId, record.OrganizationId);
        Assert.Equal(locationId, record.LocationId);
        Assert.Equal("WorkOrder", record.EntityType);
        Assert.Equal("WO-1001", record.EntityId);
        Assert.Equal("WorkOrderCreated", record.EventType);
        Assert.Equal("user-1", record.ActorUserId);
        Assert.Equal(now, record.OccurredAtUtc);
        Assert.Equal("{\"workOrderNumber\":\"WO-1001\"}", record.PayloadJson);
        Assert.Equal("corr-1", record.CorrelationId);
        Assert.Equal("Service", record.SourceModule);
    }

    private sealed class TestCurrentUser(string userId, Guid organizationId) : ICurrentUser
    {
        public string? UserId => userId;

        public string? Email => "user@example.com";

        public Guid? OrganizationId => organizationId;

        public bool IsAuthenticated => true;
    }

    private sealed class TestClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => now;
    }
}
