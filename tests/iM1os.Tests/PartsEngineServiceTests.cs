using iM1os.Application.Common;
using iM1os.Application.Parts;
using iM1os.Infrastructure.Persistence;
using iM1os.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Tests;

public sealed class PartsEngineServiceTests
{
    [Fact]
    public async Task Parts_engine_creates_searches_and_returns_complete_part_detail()
    {
        var organizationId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 27, 14, 0, 0, TimeSpan.Zero);
        var dbContext = CreateContext(organizationId, now);
        var service = CreateService(dbContext, organizationId, now);

        var part = await service.CreateManufacturerPartAsync(new CreateManufacturerPartRequest(
            ManufacturerPartNumber: "79013001044",
            Upc: "123456789012",
            Brand: "KTM",
            Description: "Fork seal kit",
            Category: "Suspension",
            Subcategory: "Fork Seals",
            ImageUrls: ["https://cdn.example.test/fork-seal.jpg"],
            Weight: 0.25m,
            Length: 4m,
            Width: 2m,
            Height: 1m,
            Msrp: 34.99m,
            Map: 29.99m,
            Status: "Active",
            CrossReferences: [new CreatePartCrossReferenceRequest("Equivalent", "SKF-FS-48", "SKF", "Common replacement")]),
            CancellationToken.None);

        await service.AddSupplierListingAsync(new AddSupplierListingRequest(
            part.Id,
            "WPS",
            "WPS-12345",
            21.88m,
            34.99m,
            12,
            "InStock",
            1,
            "SmallParcel",
            false,
            now),
            CancellationToken.None);

        await service.SetInventoryItemAsync(new SetInventoryItemRequest(
            part.Id,
            LocationId: Guid.NewGuid(),
            BinLocation: "A-17",
            QuantityOnHand: 5,
            QuantityAllocated: 2,
            AverageCost: 20.50m,
            LastCost: 21.00m,
            ReorderPoint: 2),
            CancellationToken.None);

        var searchResults = await service.SearchAsync("WPS-12345", 25, CancellationToken.None);
        var detail = await service.GetPartDetailAsync(part.Id, CancellationToken.None);

        Assert.Single(searchResults);
        Assert.Equal(part.Id, searchResults.Single().Id);
        Assert.NotNull(detail);
        Assert.Equal("79013001044", detail.ManufacturerPartNumber);
        Assert.Single(detail.Images);
        Assert.Single(detail.CrossReferences);
        Assert.Equal("SKF-FS-48", detail.CrossReferences.Single().ReferenceValue);
        Assert.Single(detail.SupplierListings);
        Assert.Equal("WPS", detail.SupplierListings.Single().Supplier);
        Assert.Single(detail.Inventory);
        Assert.Equal(3, detail.Inventory.Single().QuantityAvailable);
        Assert.Contains(await dbContext.DomainEvents.Select(x => x.EventType).ToListAsync(), x => x == "PartCreated");
        Assert.Contains(await dbContext.DomainEvents.Select(x => x.EventType).ToListAsync(), x => x == "SupplierMappingAdded");
        Assert.Contains(await dbContext.DomainEvents.Select(x => x.EventType).ToListAsync(), x => x == "InventoryChanged");
        Assert.Contains(await dbContext.DomainEvents.Select(x => x.EventType).ToListAsync(), x => x == "CostChanged");
        Assert.Equal(4, await dbContext.TimelineEvents.CountAsync());
    }

    [Fact]
    public async Task Search_respects_organization_isolation()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 27, 15, 0, 0, TimeSpan.Zero);
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var systemContext = CreateSystemContext(options, now))
        {
            systemContext.ManufacturerParts.Add(new()
            {
                OrganizationId = organizationA,
                ManufacturerPartNumber = "ORG-A-PART",
                Brand = "Twin Air",
                Description = "Air filter",
                Status = "Active"
            });
            systemContext.ManufacturerParts.Add(new()
            {
                OrganizationId = organizationB,
                ManufacturerPartNumber = "ORG-B-PART",
                Brand = "Twin Air",
                Description = "Air filter",
                Status = "Active"
            });

            await systemContext.SaveChangesAsync();
        }

        await using var tenantContext = CreateContext(options, organizationA, now);
        var service = CreateService(tenantContext, organizationA, now);

        var results = await service.SearchAsync("Twin Air", 25, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("ORG-A-PART", results.Single().ManufacturerPartNumber);
    }

    [Fact]
    public async Task SupersedePartAsync_records_supersession_event_and_detail()
    {
        var organizationId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 27, 16, 0, 0, TimeSpan.Zero);
        var dbContext = CreateContext(organizationId, now);
        var service = CreateService(dbContext, organizationId, now);

        var oldPart = await service.CreateManufacturerPartAsync(new CreateManufacturerPartRequest(
            "OLD-1",
            null,
            "Honda",
            "Old brake pad",
            "Brakes",
            null,
            [],
            null,
            null,
            null,
            null,
            20m,
            null,
            "Active",
            []),
            CancellationToken.None);
        var newPart = await service.CreateManufacturerPartAsync(new CreateManufacturerPartRequest(
            "NEW-1",
            null,
            "Honda",
            "New brake pad",
            "Brakes",
            null,
            [],
            null,
            null,
            null,
            null,
            25m,
            null,
            "Active",
            []),
            CancellationToken.None);

        var detail = await service.SupersedePartAsync(oldPart.Id, new SupersedePartRequest(newPart.Id), CancellationToken.None);

        Assert.Equal("Superseded", detail.Status);
        Assert.NotNull(detail.SupersededBy);
        Assert.Equal("NEW-1", detail.SupersededBy.ManufacturerPartNumber);
        Assert.True(await dbContext.DomainEvents.AnyAsync(x => x.EventType == "PartSuperseded"));
    }

    private static PartsEngineService CreateService(ApplicationDbContext dbContext, Guid organizationId, DateTimeOffset now)
    {
        return new PartsEngineService(dbContext, new TestCurrentUser("user-1", organizationId), new TestClock(now));
    }

    private static ApplicationDbContext CreateContext(Guid organizationId, DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return CreateContext(options, organizationId, now);
    }

    private static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options, Guid organizationId, DateTimeOffset now)
    {
        var currentUser = new TestCurrentUser("user-1", organizationId);
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
    }

    private static ApplicationDbContext CreateSystemContext(DbContextOptions<ApplicationDbContext> options, DateTimeOffset now)
    {
        var currentUser = new NoCurrentUser();
        return new ApplicationDbContext(options, currentUser, new TestClock(now), new TenantProvider(currentUser));
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
