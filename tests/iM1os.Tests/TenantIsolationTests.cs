using iM1os.Application.Common;
using iM1os.Domain.Customers;
using iM1os.Domain.Service;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Tenant_query_filters_hide_other_organizations_operational_records()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var seedContext = CreateSystemContext(options))
        {
            var customerA = new Customer
            {
                OrganizationId = organizationA,
                DisplayName = "Tenant A Customer"
            };

            var customerB = new Customer
            {
                OrganizationId = organizationB,
                DisplayName = "Tenant B Customer"
            };

            seedContext.Customers.AddRange(customerA, customerB);
            seedContext.WorkOrders.AddRange(
                new WorkOrder
                {
                    OrganizationId = organizationA,
                    WorkOrderNumber = "A-1001",
                    CustomerId = customerA.Id,
                    Stage = "intake",
                    Priority = "normal",
                    OpenedAtUtc = DateTimeOffset.UtcNow
                },
                new WorkOrder
                {
                    OrganizationId = organizationB,
                    WorkOrderNumber = "B-1001",
                    CustomerId = customerB.Id,
                    Stage = "intake",
                    Priority = "normal",
                    OpenedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        await using var tenantAContext = CreateContext(options, organizationA);

        var visibleWorkOrders = await tenantAContext.WorkOrders
            .Select(x => x.WorkOrderNumber)
            .ToListAsync();

        Assert.Single(visibleWorkOrders);
        Assert.Equal("A-1001", visibleWorkOrders[0]);
    }

    [Fact]
    public async Task SaveChangesAsync_rejects_organization_owned_records_without_tenant_context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(
            options,
            new NoCurrentUser(),
            new SystemClock(),
            new TenantProvider(new NoCurrentUser()));

        dbContext.Customers.Add(new Customer { DisplayName = "Missing Tenant Customer" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());

        Assert.Contains("requires an OrganizationId", exception.Message);
    }

    private static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options, Guid organizationId)
    {
        var currentUser = new TestCurrentUser("user-1", organizationId);
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private static ApplicationDbContext CreateSystemContext(DbContextOptions<ApplicationDbContext> options)
    {
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new SystemClock(), new TenantProvider(currentUser));
    }

    private sealed class TestCurrentUser(string userId, Guid organizationId) : ICurrentUser
    {
        public string? UserId => userId;

        public string? Email => "user@example.com";

        public Guid? OrganizationId => organizationId;

        public bool IsAuthenticated => true;
    }
}
