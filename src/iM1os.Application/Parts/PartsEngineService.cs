using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Domain.Audit;
using iM1os.Domain.Parts;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Application.Parts;

public sealed class PartsEngineService(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IPartsEngineService
{
    private const string SourceModule = "Parts";

    public async Task<PartDetail> CreateManufacturerPartAsync(CreateManufacturerPartRequest request, CancellationToken cancellationToken)
    {
        var organizationId = RequireOrganizationId();
        var now = dateTimeProvider.UtcNow;

        var part = new ManufacturerPart
        {
            OrganizationId = organizationId,
            ManufacturerPartNumber = Required(request.ManufacturerPartNumber, "Manufacturer part number"),
            Upc = Clean(request.Upc),
            Brand = Required(request.Brand, "Brand"),
            Description = Required(request.Description, "Description"),
            Category = Clean(request.Category),
            Subcategory = Clean(request.Subcategory),
            Weight = request.Weight,
            Length = request.Length,
            Width = request.Width,
            Height = request.Height,
            Msrp = request.Msrp,
            Map = request.Map,
            Status = Required(request.Status, "Status")
        };

        dbContext.ManufacturerParts.Add(part);

        var sortOrder = 0;
        foreach (var imageUrl in request.ImageUrls.Select(Clean).Where(x => x is not null).Select(x => x!))
        {
            dbContext.ManufacturerPartImages.Add(new ManufacturerPartImage
            {
                OrganizationId = organizationId,
                ManufacturerPartId = part.Id,
                Url = imageUrl,
                SortOrder = sortOrder++
            });
        }

        foreach (var crossReference in request.CrossReferences)
        {
            dbContext.ManufacturerPartCrossReferences.Add(new ManufacturerPartCrossReference
            {
                OrganizationId = organizationId,
                ManufacturerPartId = part.Id,
                ReferenceType = Required(crossReference.ReferenceType, "Reference type"),
                ReferenceValue = Required(crossReference.ReferenceValue, "Reference value"),
                Brand = Clean(crossReference.Brand),
                Notes = Clean(crossReference.Notes)
            });
        }

        AddEvents(organizationId, null, "ManufacturerPart", part.Id.ToString(), "PartCreated", "Part created", now, new
        {
            part.ManufacturerPartNumber,
            part.Brand,
            part.Upc,
            part.Description
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetPartDetailAsync(part.Id, cancellationToken))!;
    }

    public async Task<PartDetail?> GetPartDetailAsync(Guid manufacturerPartId, CancellationToken cancellationToken)
    {
        var part = await dbContext.ManufacturerParts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == manufacturerPartId, cancellationToken);

        if (part is null)
        {
            return null;
        }

        var images = await dbContext.ManufacturerPartImages
            .AsNoTracking()
            .Where(x => x.ManufacturerPartId == part.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Url)
            .ToListAsync(cancellationToken);

        var crossReferences = await dbContext.ManufacturerPartCrossReferences
            .AsNoTracking()
            .Where(x => x.ManufacturerPartId == part.Id)
            .OrderBy(x => x.ReferenceType)
            .ThenBy(x => x.ReferenceValue)
            .Select(x => new PartCrossReferenceDetail(x.ReferenceType, x.ReferenceValue, x.Brand, x.Notes))
            .ToListAsync(cancellationToken);

        var supplierListings = await dbContext.SupplierListings
            .AsNoTracking()
            .Where(x => x.ManufacturerPartId == part.Id && x.IsActive)
            .OrderBy(x => x.Supplier)
            .ThenBy(x => x.SupplierSku)
            .Select(x => new SupplierListingDetail(
                x.Id,
                x.Supplier,
                x.SupplierSku,
                x.SupplierCost,
                x.SupplierMsrp,
                x.WarehouseInventory,
                x.WarehouseAvailability,
                x.LeadTimeDays,
                x.FreightClass,
                x.IsPromotionEligible,
                x.LastSyncAtUtc))
            .ToListAsync(cancellationToken);

        var inventory = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(x => x.ManufacturerPartId == part.Id)
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.BinLocation)
            .Select(x => new InventoryItemDetail(
                x.Id,
                x.LocationId,
                x.BinLocation,
                x.QuantityOnHand,
                x.QuantityAllocated,
                x.QuantityAvailable,
                x.AverageCost,
                x.LastCost,
                x.ReorderPoint))
            .ToListAsync(cancellationToken);

        PartSummary? supersededBy = null;
        if (part.SupersededByManufacturerPartId.HasValue)
        {
            supersededBy = await dbContext.ManufacturerParts
                .AsNoTracking()
                .Where(x => x.Id == part.SupersededByManufacturerPartId.Value)
                .Select(x => new PartSummary(x.Id, x.ManufacturerPartNumber, x.Brand, x.Description))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new PartDetail(
            part.Id,
            part.ManufacturerPartNumber,
            part.Upc,
            part.Brand,
            part.Description,
            part.Category,
            part.Subcategory,
            part.Weight,
            part.Length,
            part.Width,
            part.Height,
            part.Msrp,
            part.Map,
            part.Status,
            supersededBy,
            images,
            crossReferences,
            supplierListings,
            inventory);
    }

    public async Task<IReadOnlyCollection<PartSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var cleanQuery = Required(query, "Search query");
        var normalized = cleanQuery.ToUpperInvariant();
        var take = Math.Clamp(limit, 1, 100);

        return await dbContext.ManufacturerParts
            .AsNoTracking()
            .Where(part =>
                part.ManufacturerPartNumber.ToUpper().Contains(normalized) ||
                (part.Upc != null && part.Upc.ToUpper().Contains(normalized)) ||
                part.Brand.ToUpper().Contains(normalized) ||
                part.Description.ToUpper().Contains(normalized) ||
                dbContext.SupplierListings.Any(listing =>
                    listing.ManufacturerPartId == part.Id &&
                    listing.SupplierSku.ToUpper().Contains(normalized)))
            .OrderBy(x => x.Brand)
            .ThenBy(x => x.ManufacturerPartNumber)
            .Take(take)
            .Select(x => new PartSearchResult(
                x.Id,
                x.ManufacturerPartNumber,
                x.Upc,
                x.Brand,
                x.Description,
                x.Category,
                x.Subcategory,
                x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierListingDetail> AddSupplierListingAsync(AddSupplierListingRequest request, CancellationToken cancellationToken)
    {
        var organizationId = RequireOrganizationId();
        var now = dateTimeProvider.UtcNow;

        await EnsurePartExistsAsync(request.ManufacturerPartId, cancellationToken);

        var listing = new SupplierListing
        {
            OrganizationId = organizationId,
            ManufacturerPartId = request.ManufacturerPartId,
            Supplier = Required(request.Supplier, "Supplier"),
            SupplierSku = Required(request.SupplierSku, "Supplier SKU"),
            SupplierCost = request.SupplierCost,
            SupplierMsrp = request.SupplierMsrp,
            WarehouseInventory = request.WarehouseInventory,
            WarehouseAvailability = Required(request.WarehouseAvailability, "Warehouse availability"),
            LeadTimeDays = request.LeadTimeDays,
            FreightClass = Clean(request.FreightClass),
            IsPromotionEligible = request.IsPromotionEligible,
            LastSyncAtUtc = request.LastSyncAtUtc ?? now
        };

        dbContext.SupplierListings.Add(listing);
        AddEvents(organizationId, null, "ManufacturerPart", request.ManufacturerPartId.ToString(), "SupplierMappingAdded", "Supplier mapping added", now, new
        {
            listing.Supplier,
            listing.SupplierSku,
            listing.SupplierCost,
            listing.WarehouseAvailability
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SupplierListingDetail(
            listing.Id,
            listing.Supplier,
            listing.SupplierSku,
            listing.SupplierCost,
            listing.SupplierMsrp,
            listing.WarehouseInventory,
            listing.WarehouseAvailability,
            listing.LeadTimeDays,
            listing.FreightClass,
            listing.IsPromotionEligible,
            listing.LastSyncAtUtc);
    }

    public async Task<InventoryItemDetail> SetInventoryItemAsync(SetInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var organizationId = RequireOrganizationId();
        var now = dateTimeProvider.UtcNow;

        await EnsurePartExistsAsync(request.ManufacturerPartId, cancellationToken);

        var quantityAvailable = request.QuantityOnHand - request.QuantityAllocated;
        if (quantityAvailable < 0)
        {
            throw new InvalidOperationException("Allocated quantity cannot exceed quantity on hand.");
        }

        var inventoryItem = await dbContext.InventoryItems
            .SingleOrDefaultAsync(x => x.ManufacturerPartId == request.ManufacturerPartId && x.LocationId == request.LocationId, cancellationToken);

        var previousQuantityOnHand = inventoryItem?.QuantityOnHand;
        var previousLastCost = inventoryItem?.LastCost;

        if (inventoryItem is null)
        {
            inventoryItem = new InventoryItem
            {
                OrganizationId = organizationId,
                ManufacturerPartId = request.ManufacturerPartId,
                LocationId = request.LocationId
            };
            dbContext.InventoryItems.Add(inventoryItem);
        }

        inventoryItem.BinLocation = Clean(request.BinLocation);
        inventoryItem.QuantityOnHand = request.QuantityOnHand;
        inventoryItem.QuantityAllocated = request.QuantityAllocated;
        inventoryItem.QuantityAvailable = quantityAvailable;
        inventoryItem.AverageCost = request.AverageCost;
        inventoryItem.LastCost = request.LastCost;
        inventoryItem.ReorderPoint = request.ReorderPoint;

        AddEvents(organizationId, request.LocationId, "ManufacturerPart", request.ManufacturerPartId.ToString(), "InventoryChanged", "Inventory changed", now, new
        {
            request.LocationId,
            previousQuantityOnHand,
            inventoryItem.QuantityOnHand,
            inventoryItem.QuantityAllocated,
            inventoryItem.QuantityAvailable,
            inventoryItem.BinLocation
        });

        if (previousLastCost != request.LastCost)
        {
            AddEvents(organizationId, request.LocationId, "ManufacturerPart", request.ManufacturerPartId.ToString(), "CostChanged", "Cost changed", now, new
            {
                request.LocationId,
                PreviousLastCost = previousLastCost,
                inventoryItem.LastCost,
                inventoryItem.AverageCost
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InventoryItemDetail(
            inventoryItem.Id,
            inventoryItem.LocationId,
            inventoryItem.BinLocation,
            inventoryItem.QuantityOnHand,
            inventoryItem.QuantityAllocated,
            inventoryItem.QuantityAvailable,
            inventoryItem.AverageCost,
            inventoryItem.LastCost,
            inventoryItem.ReorderPoint);
    }

    public async Task<PartDetail> SupersedePartAsync(Guid manufacturerPartId, SupersedePartRequest request, CancellationToken cancellationToken)
    {
        var organizationId = RequireOrganizationId();
        var now = dateTimeProvider.UtcNow;

        var part = await dbContext.ManufacturerParts.SingleOrDefaultAsync(x => x.Id == manufacturerPartId, cancellationToken)
            ?? throw new InvalidOperationException("Manufacturer part was not found.");

        await EnsurePartExistsAsync(request.SupersededByManufacturerPartId, cancellationToken);

        part.SupersededByManufacturerPartId = request.SupersededByManufacturerPartId;
        part.Status = "Superseded";

        AddEvents(organizationId, null, "ManufacturerPart", part.Id.ToString(), "PartSuperseded", "Part superseded", now, new
        {
            SupersededPartId = part.Id,
            request.SupersededByManufacturerPartId
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetPartDetailAsync(part.Id, cancellationToken))!;
    }

    private async Task EnsurePartExistsAsync(Guid manufacturerPartId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ManufacturerParts.AnyAsync(x => x.Id == manufacturerPartId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Manufacturer part was not found.");
        }
    }

    private void AddEvents(Guid organizationId, Guid? locationId, string entityType, string entityId, string eventType, string summary, DateTimeOffset occurredAtUtc, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var correlationId = Guid.NewGuid().ToString("N");

        dbContext.TimelineEvents.Add(new TimelineEvent
        {
            OrganizationId = organizationId,
            LocationId = locationId,
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            ActorUserId = currentUser.UserId,
            OccurredAtUtc = occurredAtUtc,
            Summary = summary,
            PayloadJson = payloadJson
        });

        dbContext.DomainEvents.Add(new DomainEventRecord
        {
            OrganizationId = organizationId,
            LocationId = locationId,
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            ActorUserId = currentUser.UserId,
            OccurredAtUtc = occurredAtUtc,
            PayloadJson = payloadJson,
            CorrelationId = correlationId,
            SourceModule = SourceModule
        });
    }

    private Guid RequireOrganizationId()
    {
        return currentUser.OrganizationId ?? throw new InvalidOperationException("Parts Engine operations require an organization context.");
    }

    private static string Required(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{fieldName} is required.")
            : value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
