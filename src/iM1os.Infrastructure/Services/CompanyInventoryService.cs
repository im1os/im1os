using System.Globalization;
using System.Text;
using System.Text.Json;
using iM1os.Application.Common;
using iM1os.Application.Inventory;
using iM1os.Domain.GlobalCatalog;
using iM1os.Domain.Parts;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class CompanyInventoryService(IApplicationDbContext dbContext) : ICompanyInventoryService
{
    private const int PageSize = 100;
    private static readonly IReadOnlyDictionary<string, string> MakerAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ALLBALLSRACING"] = "ALLBALLS",
        ["MAXIMARACINGOIL"] = "MAXIMA"
    };

    public async Task<CompanyInventoryWorkspace> GetWorkspaceAsync(Guid organizationId, CompanyInventorySearchRequest request, CancellationToken cancellationToken)
    {
        var locations = await dbContext.Locations.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CompanyInventoryLocationOption(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);

        var query = dbContext.CompanyInventoryItems.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var search = request.Query.Trim().ToUpperInvariant();
            var normalizedSearch = NormalizePartNumber(search) ?? search;
            query = query.Where(x =>
                (x.Sku != null && x.Sku.ToUpper().Contains(search)) ||
                (x.ManufacturerPartNumber != null && x.ManufacturerPartNumber.ToUpper().Contains(search)) ||
                (x.NormalizedManufacturerPartNumber != null && x.NormalizedManufacturerPartNumber.ToUpper().Contains(normalizedSearch)) ||
                (x.Upc != null && x.Upc.ToUpper().Contains(search)) ||
                (x.Brand != null && x.Brand.ToUpper().Contains(search)) ||
                x.Title.ToUpper().Contains(search) ||
                (x.Category != null && x.Category.ToUpper().Contains(search)));
        }

        if (request.StockedOnly)
        {
            query = query.Where(x => x.IsStockedInStore);
        }

        var itemEntities = await query
            .OrderBy(x => x.Brand)
            .ThenBy(x => x.Title)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var itemIds = itemEntities.Select(x => x.Id).ToArray();
        var stockEntities = await dbContext.CompanyInventoryLocationStocks.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && itemIds.Contains(x.CompanyInventoryItemId))
            .OrderBy(x => x.LocationNameSnapshot)
            .ToListAsync(cancellationToken);

        if (request.LocationId is not null)
        {
            stockEntities = stockEntities.Where(x => x.LocationId == request.LocationId).ToList();
            var itemIdsWithLocation = stockEntities.Select(x => x.CompanyInventoryItemId).ToHashSet();
            itemEntities = itemEntities.Where(x => itemIdsWithLocation.Contains(x.Id)).ToList();
            itemIds = itemEntities.Select(x => x.Id).ToArray();
        }

        var movementEntities = await dbContext.CompanyInventoryMovements.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && itemIds.Contains(x.CompanyInventoryItemId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        var stockByItem = stockEntities.ToLookup(x => x.CompanyInventoryItemId);
        var movementByItem = movementEntities.ToLookup(x => x.CompanyInventoryItemId);
        var locationNames = locations.ToDictionary(x => x.Id, x => x.Name);
        var replacementPrices = await GetReplacementPricesAsync(organizationId, itemEntities, cancellationToken);

        var rows = itemEntities
            .Select(item => BuildRow(item, stockByItem[item.Id], movementByItem[item.Id], locationNames, replacementPrices))
            .Where(x => !request.LowStockOnly || x.IsLowStock)
            .ToList();

        var allStock = await dbContext.CompanyInventoryLocationStocks.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var allItems = await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .ToListAsync(cancellationToken);
        var allReplacementPrices = await GetReplacementPricesAsync(organizationId, allItems, cancellationToken);
        var itemCost = allItems.ToDictionary(x => x.Id, x => InventoryCost(x, allReplacementPrices) ?? 0m);

        return new CompanyInventoryWorkspace(
            rows,
            locations,
            request.Query,
            request.LocationId,
            request.LowStockOnly,
            request.StockedOnly,
            allItems.Count,
            allStock.Count(IsLowStock),
            allStock.Sum(x => x.QuantityOnHand),
            allStock.Sum(x => x.QuantityAvailable),
            allStock.Sum(x => x.QuantityOnHand * itemCost.GetValueOrDefault(x.CompanyInventoryItemId)));
    }

    public async Task<CompanyInventoryAddPage> GetAddPageAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return new CompanyInventoryAddPage(await GetLocationOptionsAsync(organizationId, cancellationToken));
    }

    public async Task<CompanyInventorySupplierLookupResult> LookupSupplierItemAsync(Guid organizationId, CompanyInventorySupplierLookupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var supplierProductId = await ResolveSupplierProductIdAsync(organizationId, new CompanyInventorySupplierItemRequest(
                request.SupplierProductId,
                request.LookupValue,
                request.SupplierCode,
                null,
                null,
                0,
                null,
                null,
                null,
                null,
                true), cancellationToken);

            var source = await (
                from supplierProduct in dbContext.SupplierProducts.IgnoreQueryFilters()
                join supplier in dbContext.Suppliers.IgnoreQueryFilters() on supplierProduct.SupplierId equals supplier.Id
                join globalProduct in dbContext.GlobalProducts.IgnoreQueryFilters() on supplierProduct.GlobalProductId equals globalProduct.Id
                where supplierProduct.Id == supplierProductId
                select new { supplierProduct, supplier, globalProduct })
                .SingleOrDefaultAsync(cancellationToken);

            if (source is null)
            {
                return SupplierLookupNotFound("Supplier product was not found.");
            }

            var price = await dbContext.SupplierPrices.IgnoreQueryFilters()
                .Where(x => x.SupplierProductId == supplierProductId)
                .OrderByDescending(x => x.LastUpdated)
                .FirstOrDefaultAsync(cancellationToken);
            var companyDealerCost = await dbContext.CompanySupplierPrices.IgnoreQueryFilters()
                .Where(x => x.OrganizationId == organizationId && x.SupplierProductId == supplierProductId)
                .Select(x => (decimal?)x.ActualDealerCost)
                .FirstOrDefaultAsync(cancellationToken);
            var dealerCost = companyDealerCost ?? PositivePrice(price?.DealerCost);
            var manufacturerPartNumber = source.globalProduct.ManufacturerPartNumber ?? source.supplierProduct.ManufacturerPartNumber;
            var inventoryItem = await FindExistingSupplierInventoryItemAsync(
                organizationId,
                supplierProductId,
                NormalizePartNumber(manufacturerPartNumber),
                source.globalProduct.Brand,
                source.globalProduct.Upc,
                cancellationToken);

            var locationStocks = Array.Empty<CompanyInventoryLocationStockRow>();
            if (inventoryItem is not null)
            {
                var locations = (await GetLocationOptionsAsync(organizationId, cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
                var stocks = await dbContext.CompanyInventoryLocationStocks.IgnoreQueryFilters()
                    .Where(x => x.OrganizationId == organizationId && x.CompanyInventoryItemId == inventoryItem.Id)
                    .OrderBy(x => x.LocationNameSnapshot)
                    .ToListAsync(cancellationToken);
                locationStocks = stocks
                    .Select(x => new CompanyInventoryLocationStockRow(
                        x.Id,
                        x.LocationId,
                        x.LocationId is not null && locations.TryGetValue(x.LocationId.Value, out var locationName) ? locationName : x.LocationNameSnapshot ?? "Company",
                        x.BinLocation,
                        x.QuantityOnHand,
                        x.QuantityAllocated,
                        x.QuantityAvailable,
                        x.QuantityOnOrder,
                        x.QuantityBackordered,
                        x.MinQuantity,
                        EffectiveMaxQuantity(x.MinQuantity, x.ReorderQuantity),
                        x.MinQuantity,
                        x.ReorderQuantity,
                        x.StockInStore,
                        x.AllowNegativeStock,
                        IsLowStock(x)))
                    .ToArray();
            }

            return new CompanyInventorySupplierLookupResult(
                true,
                null,
                supplierProductId,
                source.supplier.Code,
                source.supplier.Name,
                source.supplierProduct.SupplierSku,
                manufacturerPartNumber,
                source.globalProduct.Upc,
                source.globalProduct.Brand,
                BuildSupplierTitle(source.globalProduct.Brand, source.globalProduct.Description, manufacturerPartNumber),
                source.globalProduct.Category,
                FirstImage(source.globalProduct.ImagesJson) ?? FirstImage(source.supplierProduct.SupplierImagesJson),
                price?.Msrp,
                dealerCost,
                inventoryItem is not null,
                locationStocks.Sum(x => x.QuantityOnHand),
                locationStocks.Sum(x => x.QuantityAvailable),
                locationStocks);
        }
        catch (InvalidOperationException ex)
        {
            return SupplierLookupNotFound(ex.Message);
        }
    }

    public async Task<Guid> CreateCustomItemAsync(Guid organizationId, Guid actorUserId, CompanyInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var title = Required(request.Title, "Title");
        var initialLocationId = RequireLocationId(request.InitialLocationId);
        var stockPolicy = NormalizeStockPolicy(request.IsStockedInStore, request.MinQuantity, request.ReorderQuantity);
        var item = new CompanyInventoryItem
        {
            OrganizationId = organizationId,
            SourceType = "Custom",
            Sku = Clean(request.Sku),
            ManufacturerPartNumber = Clean(request.ManufacturerPartNumber),
            NormalizedManufacturerPartNumber = NormalizePartNumber(request.ManufacturerPartNumber),
            Upc = Clean(request.Upc),
            Brand = Clean(request.Brand),
            Title = title,
            Description = Clean(request.Description),
            Category = Clean(request.Category),
            Subcategory = Clean(request.Subcategory),
            ImageUrl = Clean(request.ImageUrl),
            RetailPrice = request.RetailPrice,
            SalePrice = request.SalePrice,
            DefaultCost = request.DefaultCost,
            AverageCost = request.DefaultCost,
            LastCost = request.DefaultCost,
            IsStockedInStore = request.IsStockedInStore,
            TrackInventory = request.TrackInventory,
            IsSerialized = request.IsSerialized,
            Notes = Clean(request.Notes)
        };

        dbContext.CompanyInventoryItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SaveInitialStockAsync(organizationId, item.Id, initialLocationId, request.InitialBinLocation, request.InitialQuantityOnHand, stockPolicy.MinQuantity, stockPolicy.MaxQuantity, stockPolicy.MinQuantity, stockPolicy.ReorderQuantity, request.IsStockedInStore, request.DefaultCost, "Initial import", cancellationToken);

        return item.Id;
    }

    public async Task<Guid> AddSupplierItemAsync(Guid organizationId, Guid actorUserId, CompanyInventorySupplierItemRequest request, CancellationToken cancellationToken)
    {
        var supplierProductId = await ResolveSupplierProductIdAsync(organizationId, request, cancellationToken);
        var initialLocationId = RequireLocationId(request.InitialLocationId);
        var stockPolicy = NormalizeStockPolicy(request.StockInStore, request.MinQuantity, request.ReorderQuantity);
        var source = await (
            from supplierProduct in dbContext.SupplierProducts.IgnoreQueryFilters()
            join supplier in dbContext.Suppliers.IgnoreQueryFilters() on supplierProduct.SupplierId equals supplier.Id
            join globalProduct in dbContext.GlobalProducts.IgnoreQueryFilters() on supplierProduct.GlobalProductId equals globalProduct.Id
            where supplierProduct.Id == supplierProductId
            select new { supplierProduct, supplier, globalProduct })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Supplier product was not found.");

        var price = await dbContext.SupplierPrices.IgnoreQueryFilters()
            .Where(x => x.SupplierProductId == supplierProductId)
            .OrderByDescending(x => x.LastUpdated)
            .FirstOrDefaultAsync(cancellationToken);
        var companyDealerCost = await dbContext.CompanySupplierPrices.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.SupplierProductId == supplierProductId)
            .Select(x => (decimal?)x.ActualDealerCost)
            .FirstOrDefaultAsync(cancellationToken);
        var dealerCost = companyDealerCost ?? PositivePrice(price?.DealerCost);

        var manufacturerPartNumber = source.globalProduct.ManufacturerPartNumber ?? source.supplierProduct.ManufacturerPartNumber;
        var normalizedManufacturerPartNumber = NormalizePartNumber(manufacturerPartNumber);
        var existing = await FindExistingSupplierInventoryItemAsync(
            organizationId,
            supplierProductId,
            normalizedManufacturerPartNumber,
            source.globalProduct.Brand,
            source.globalProduct.Upc,
            cancellationToken);
        if (existing is not null)
        {
            ApplyInventoryItemOverrides(existing, request.AssignUpc, request.SalePrice);
            existing.GlobalProductId ??= source.globalProduct.Id;
            existing.SupplierProductId ??= source.supplierProduct.Id;
            existing.SourceSupplierCode ??= source.supplier.Code;
            existing.SourceSupplierName ??= source.supplier.Name;
            existing.SourceSupplierSku ??= source.supplierProduct.SupplierSku;
            existing.SourceSupplierProductId ??= source.supplierProduct.SourceSupplierProductId;
            existing.ManufacturerPartNumber ??= manufacturerPartNumber;
            existing.NormalizedManufacturerPartNumber ??= normalizedManufacturerPartNumber;
            existing.Sku = manufacturerPartNumber ?? existing.Sku;
            existing.Brand ??= source.globalProduct.Brand;
            existing.Upc ??= source.globalProduct.Upc;
            existing.RetailPrice = price?.Msrp ?? existing.RetailPrice;
            await SavePhysicalCountAsync(
                organizationId,
                existing.Id,
                initialLocationId,
                request.InitialBinLocation,
                request.InitialQuantityOnHand,
                stockPolicy.MinQuantity,
                stockPolicy.MaxQuantity,
                stockPolicy.MinQuantity,
                stockPolicy.ReorderQuantity,
                request.StockInStore,
                dealerCost,
                "Supplier item updated",
                cancellationToken);
            return existing.Id;
        }

        var title = BuildSupplierTitle(source.globalProduct.Brand, source.globalProduct.Description, source.globalProduct.ManufacturerPartNumber ?? source.supplierProduct.ManufacturerPartNumber);
        var item = new CompanyInventoryItem
        {
            OrganizationId = organizationId,
            GlobalProductId = source.globalProduct.Id,
            SupplierProductId = source.supplierProduct.Id,
            SourceType = "PlatformSupplier",
            SourceSupplierCode = source.supplier.Code,
            SourceSupplierName = source.supplier.Name,
            SourceSupplierSku = source.supplierProduct.SupplierSku,
            SourceSupplierProductId = source.supplierProduct.SourceSupplierProductId,
            Sku = manufacturerPartNumber ?? source.supplierProduct.SupplierSku,
            ManufacturerPartNumber = manufacturerPartNumber,
            NormalizedManufacturerPartNumber = normalizedManufacturerPartNumber,
            Upc = source.globalProduct.Upc,
            Brand = source.globalProduct.Brand,
            Title = title,
            Description = source.globalProduct.LongDescription ?? source.supplierProduct.SupplierDescription,
            Category = source.globalProduct.Category,
            ImageUrl = FirstImage(source.globalProduct.ImagesJson) ?? FirstImage(source.supplierProduct.SupplierImagesJson),
            RetailPrice = price?.Msrp,
            SalePrice = request.SalePrice,
            DefaultCost = dealerCost,
            AverageCost = dealerCost,
            LastCost = dealerCost,
            IsStockedInStore = request.StockInStore,
            TrackInventory = true,
            Status = source.globalProduct.Status
        };

        dbContext.CompanyInventoryItems.Add(item);
        ApplyInventoryItemOverrides(item, request.AssignUpc, request.SalePrice);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SaveInitialStockAsync(organizationId, item.Id, initialLocationId, request.InitialBinLocation, request.InitialQuantityOnHand, stockPolicy.MinQuantity, stockPolicy.MaxQuantity, stockPolicy.MinQuantity, stockPolicy.ReorderQuantity, request.StockInStore, dealerCost, "Supplier item added", cancellationToken);
        return item.Id;
    }

    private async Task<Guid> ResolveSupplierProductIdAsync(Guid organizationId, CompanyInventorySupplierItemRequest request, CancellationToken cancellationToken)
    {
        if (request.SupplierProductId is { } supplierProductId && supplierProductId != Guid.Empty)
        {
            return supplierProductId;
        }

        var lookup = Clean(request.LookupValue) ?? throw new InvalidOperationException("Enter a supplier SKU, UPC, MFG part number, or supplier product ID.");
        if (Guid.TryParse(lookup, out var parsedSupplierProductId))
        {
            return parsedSupplierProductId;
        }

        var supplierCode = Clean(request.SupplierCode)?.ToUpperInvariant();
        var lookupUpper = lookup.ToUpperInvariant();
        var normalizedLookup = NormalizePartNumber(lookup) ?? lookupUpper;
        var baseQuery =
            from supplierProduct in dbContext.SupplierProducts.IgnoreQueryFilters()
            join supplier in dbContext.Suppliers.IgnoreQueryFilters() on supplierProduct.SupplierId equals supplier.Id
            join globalProduct in dbContext.GlobalProducts.IgnoreQueryFilters() on supplierProduct.GlobalProductId equals globalProduct.Id
            where supplierCode == null || supplier.Code == supplierCode
            select new
            {
                supplierProduct.Id,
                SupplierCode = supplier.Code,
                supplierProduct.SupplierSku,
                supplierProduct.SourceSupplierProductId,
                supplierProduct.SupplierPartNumber,
                supplierProduct.ManufacturerPartNumber,
                supplierProduct.NormalizedManufacturerPartNumber,
                supplierProduct.WarehouseAvailability,
                globalProduct.Upc,
                GlobalManufacturerPartNumber = globalProduct.ManufacturerPartNumber,
                GlobalNormalizedManufacturerPartNumber = globalProduct.NormalizedManufacturerPartNumber
            };

        var skuMatches = await baseQuery
            .Where(x =>
                x.SupplierSku.ToUpper() == lookupUpper ||
                (x.SourceSupplierProductId != null && x.SourceSupplierProductId.ToUpper() == lookupUpper) ||
                (x.SupplierPartNumber != null && x.SupplierPartNumber.ToUpper() == lookupUpper))
            .Select(x => new SupplierProductLookupCandidate(
                x.Id,
                x.SupplierCode,
                x.SupplierSku,
                x.SourceSupplierProductId,
                x.SupplierPartNumber,
                x.ManufacturerPartNumber,
                x.NormalizedManufacturerPartNumber,
                x.WarehouseAvailability,
                x.Upc,
                x.GlobalManufacturerPartNumber,
                x.GlobalNormalizedManufacturerPartNumber))
            .Take(25)
            .ToArrayAsync(cancellationToken);
        if (skuMatches.Length > 0)
        {
            return await SelectSupplierProductCandidateAsync(organizationId, skuMatches, cancellationToken);
        }

        var upcMatches = await baseQuery
            .Where(x => x.Upc != null && x.Upc.ToUpper() == lookupUpper)
            .Select(x => new SupplierProductLookupCandidate(
                x.Id,
                x.SupplierCode,
                x.SupplierSku,
                x.SourceSupplierProductId,
                x.SupplierPartNumber,
                x.ManufacturerPartNumber,
                x.NormalizedManufacturerPartNumber,
                x.WarehouseAvailability,
                x.Upc,
                x.GlobalManufacturerPartNumber,
                x.GlobalNormalizedManufacturerPartNumber))
            .Take(25)
            .ToArrayAsync(cancellationToken);
        if (upcMatches.Length > 0)
        {
            return await SelectSupplierProductCandidateAsync(organizationId, upcMatches, cancellationToken);
        }

        var mfgMatches = await baseQuery
            .Where(x =>
                (x.ManufacturerPartNumber != null && x.ManufacturerPartNumber.ToUpper() == lookupUpper) ||
                (x.GlobalManufacturerPartNumber != null && x.GlobalManufacturerPartNumber.ToUpper() == lookupUpper) ||
                (x.NormalizedManufacturerPartNumber != null && x.NormalizedManufacturerPartNumber.ToUpper() == normalizedLookup) ||
                (x.GlobalNormalizedManufacturerPartNumber != null && x.GlobalNormalizedManufacturerPartNumber.ToUpper() == normalizedLookup))
            .Select(x => new SupplierProductLookupCandidate(
                x.Id,
                x.SupplierCode,
                x.SupplierSku,
                x.SourceSupplierProductId,
                x.SupplierPartNumber,
                x.ManufacturerPartNumber,
                x.NormalizedManufacturerPartNumber,
                x.WarehouseAvailability,
                x.Upc,
                x.GlobalManufacturerPartNumber,
                x.GlobalNormalizedManufacturerPartNumber))
            .Take(25)
            .ToArrayAsync(cancellationToken);
        if (mfgMatches.Length > 0)
        {
            return await SelectSupplierProductCandidateAsync(organizationId, mfgMatches, cancellationToken);
        }

        throw new InvalidOperationException($"No platform supplier item matched '{lookup}'.");
    }

    public async Task SaveLocationStockAsync(Guid organizationId, Guid actorUserId, CompanyInventoryLocationStockRequest request, CancellationToken cancellationToken)
    {
        _ = await FindItemAsync(organizationId, request.CompanyInventoryItemId, cancellationToken);
        var locationId = RequireLocationId(request.LocationId);
        var stockPolicy = NormalizeStockPolicy(request.StockInStore, request.MinQuantity, request.ReorderQuantity);
        var stock = await FindOrCreateStockAsync(organizationId, request.CompanyInventoryItemId, locationId, cancellationToken);
        stock.BinLocation = Clean(request.BinLocation);
        stock.MinQuantity = stockPolicy.MinQuantity;
        stock.MaxQuantity = stockPolicy.MaxQuantity;
        stock.ReorderPoint = stockPolicy.MinQuantity;
        stock.ReorderQuantity = stockPolicy.ReorderQuantity;
        stock.QuantityOnOrder = Math.Max(0, request.QuantityOnOrder);
        stock.QuantityBackordered = Math.Max(0, request.QuantityBackordered);
        stock.StockInStore = request.StockInStore;
        stock.AllowNegativeStock = request.AllowNegativeStock;
        stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityAllocated;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustStockAsync(Guid organizationId, Guid actorUserId, CompanyInventoryStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(organizationId, request.CompanyInventoryItemId, cancellationToken);
        var stock = await FindOrCreateStockAsync(organizationId, request.CompanyInventoryItemId, RequireLocationId(request.LocationId), cancellationToken);
        var newQuantity = stock.QuantityOnHand + request.QuantityDelta;
        if (!stock.AllowNegativeStock && newQuantity < 0)
        {
            throw new InvalidOperationException("This adjustment would make stock negative.");
        }

        stock.QuantityOnHand = newQuantity;
        stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityAllocated;
        stock.LastCountedAtUtc = DateTimeOffset.UtcNow;
        if (request.UnitCost is not null)
        {
            item.LastCost = request.UnitCost;
            item.AverageCost ??= request.UnitCost;
        }

        dbContext.CompanyInventoryMovements.Add(new CompanyInventoryMovement
        {
            OrganizationId = organizationId,
            CompanyInventoryItemId = item.Id,
            LocationId = stock.LocationId,
            MovementType = "Adjustment",
            QuantityDelta = request.QuantityDelta,
            QuantityAfter = stock.QuantityOnHand,
            UnitCost = request.UnitCost,
            Reason = Clean(request.Reason),
            Notes = Clean(request.Notes)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CompanyInventoryImportResult> ImportCsvAsync(Guid organizationId, Guid actorUserId, Stream csvStream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            return new CompanyInventoryImportResult(0, 0, 0, 0, ["CSV file is empty."]);
        }

        var headers = SplitCsvLine(headerLine).Select(NormalizeHeader).ToArray();
        var processed = 0;
        var created = 0;
        var updated = 0;
        var failed = 0;
        var errors = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            processed++;
            try
            {
                var values = SplitCsvLine(line);
                var row = headers.Select((header, index) => new { header, value = index < values.Count ? values[index] : null })
                    .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase);
                var title = Required(Value(row, "title", "description", "name"), "Title");
                var sku = Clean(Value(row, "sku", "stockkeepingunit"));
                var mfgPart = Clean(Value(row, "mfgpart", "manufacturerpartnumber", "partnumber"));
                var existing = await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.OrganizationId == organizationId &&
                        ((sku != null && x.Sku == sku) || (mfgPart != null && x.NormalizedManufacturerPartNumber == NormalizePartNumber(mfgPart))), cancellationToken);

                if (existing is null)
                {
                    existing = new CompanyInventoryItem
                    {
                        OrganizationId = organizationId,
                        SourceType = "Spreadsheet",
                        Sku = sku,
                        ManufacturerPartNumber = mfgPart,
                        NormalizedManufacturerPartNumber = NormalizePartNumber(mfgPart),
                        Upc = Clean(Value(row, "upc")),
                        Brand = Clean(Value(row, "brand")),
                        Title = title,
                        Category = Clean(Value(row, "category")),
                        RetailPrice = DecimalValue(row, "retail", "retailprice", "msrp"),
                        DefaultCost = DecimalValue(row, "cost", "defaultcost", "actualcost"),
                        AverageCost = DecimalValue(row, "cost", "defaultcost", "actualcost"),
                        LastCost = DecimalValue(row, "cost", "defaultcost", "actualcost"),
                        IsStockedInStore = BoolValue(row, "stockinstore", "stocked"),
                        TrackInventory = true
                    };
                    dbContext.CompanyInventoryItems.Add(existing);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    created++;
                }
                else
                {
                    existing.Title = title;
                    existing.Brand = Clean(Value(row, "brand")) ?? existing.Brand;
                    existing.Category = Clean(Value(row, "category")) ?? existing.Category;
                    existing.RetailPrice = DecimalValue(row, "retail", "retailprice", "msrp") ?? existing.RetailPrice;
                    existing.DefaultCost = DecimalValue(row, "cost", "defaultcost", "actualcost") ?? existing.DefaultCost;
                    updated++;
                }

                var locationCode = Clean(Value(row, "location", "locationcode"));
                var locationId = await ResolveLocationIdAsync(organizationId, locationCode, cancellationToken);
                if (locationId is null)
                {
                    throw new InvalidOperationException("Location is required.");
                }
                var quantity = DecimalValue(row, "quantity", "qty", "onhand") ?? 0m;
                var stockInStore = BoolValue(row, "stockinstore", "stocked");
                var stockPolicy = NormalizeStockPolicy(stockInStore, DecimalValue(row, "min", "minimum", "minquantity", "reorderpoint"), DecimalValue(row, "reorderquantity", "reorderqty"));
                await SaveInitialStockAsync(
                    organizationId,
                    existing.Id,
                    locationId,
                    Value(row, "bin", "binlocation"),
                    quantity,
                    stockPolicy.MinQuantity,
                    stockPolicy.MaxQuantity,
                    stockPolicy.MinQuantity,
                    stockPolicy.ReorderQuantity,
                    stockInStore,
                    existing.DefaultCost,
                    "CSV import",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Row {processed + 1}: {ex.Message}");
            }
        }

        return new CompanyInventoryImportResult(processed, created, updated, failed, errors.Take(20).ToArray());
    }

    private async Task SaveInitialStockAsync(Guid organizationId, Guid itemId, Guid? locationId, string? binLocation, decimal quantity, decimal? min, decimal? max, decimal? reorderPoint, decimal? reorderQuantity, bool stockInStore, decimal? unitCost, string reason, CancellationToken cancellationToken)
    {
        var stock = await FindOrCreateStockAsync(organizationId, itemId, locationId, cancellationToken);
        stock.BinLocation = Clean(binLocation) ?? stock.BinLocation;
        stock.MinQuantity = min ?? stock.MinQuantity;
        stock.MaxQuantity = max ?? stock.MaxQuantity;
        stock.ReorderPoint = reorderPoint ?? stock.ReorderPoint;
        stock.ReorderQuantity = reorderQuantity ?? stock.ReorderQuantity;
        stock.StockInStore = stockInStore;
        if (quantity != 0)
        {
            stock.QuantityOnHand = quantity;
            stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityAllocated;
            dbContext.CompanyInventoryMovements.Add(new CompanyInventoryMovement
            {
                OrganizationId = organizationId,
                CompanyInventoryItemId = itemId,
                LocationId = stock.LocationId,
                MovementType = "Initial",
                QuantityDelta = quantity,
                QuantityAfter = stock.QuantityOnHand,
                UnitCost = unitCost,
                Reason = reason
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SavePhysicalCountAsync(Guid organizationId, Guid itemId, Guid? locationId, string? binLocation, decimal quantity, decimal? min, decimal? max, decimal? reorderPoint, decimal? reorderQuantity, bool stockInStore, decimal? unitCost, string reason, CancellationToken cancellationToken)
    {
        var stock = await FindOrCreateStockAsync(organizationId, itemId, locationId, cancellationToken);
        stock.BinLocation = Clean(binLocation) ?? stock.BinLocation;
        stock.MinQuantity = min ?? stock.MinQuantity;
        stock.MaxQuantity = max ?? stock.MaxQuantity;
        stock.ReorderPoint = reorderPoint ?? stock.ReorderPoint;
        stock.ReorderQuantity = reorderQuantity ?? stock.ReorderQuantity;
        stock.StockInStore = stockInStore;
        var delta = quantity - stock.QuantityOnHand;
        stock.QuantityOnHand = quantity;
        stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityAllocated;
        stock.LastCountedAtUtc = DateTimeOffset.UtcNow;
        if (delta != 0)
        {
            dbContext.CompanyInventoryMovements.Add(new CompanyInventoryMovement
            {
                OrganizationId = organizationId,
                CompanyInventoryItemId = itemId,
                LocationId = stock.LocationId,
                MovementType = "PhysicalCount",
                QuantityDelta = delta,
                QuantityAfter = stock.QuantityOnHand,
                UnitCost = unitCost,
                Reason = reason
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyInventoryItem> FindItemAsync(Guid organizationId, Guid itemId, CancellationToken cancellationToken)
    {
        return await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == itemId, cancellationToken)
            ?? throw new InvalidOperationException("Inventory item was not found.");
    }

    private async Task<CompanyInventoryLocationStock> FindOrCreateStockAsync(Guid organizationId, Guid itemId, Guid? locationId, CancellationToken cancellationToken)
    {
        var stock = await dbContext.CompanyInventoryLocationStocks.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.CompanyInventoryItemId == itemId && x.LocationId == locationId, cancellationToken);
        if (stock is not null)
        {
            return stock;
        }

        var locationName = locationId is null
            ? "Company"
            : await dbContext.Locations.IgnoreQueryFilters()
                .Where(x => x.OrganizationId == organizationId && x.Id == locationId)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? "Location";
        stock = new CompanyInventoryLocationStock
        {
            OrganizationId = organizationId,
            CompanyInventoryItemId = itemId,
            LocationId = locationId,
            LocationNameSnapshot = locationName
        };
        dbContext.CompanyInventoryLocationStocks.Add(stock);
        return stock;
    }

    private async Task<Guid?> ResolveLocationIdAsync(Guid organizationId, string? locationCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            return null;
        }

        var clean = locationCode.Trim();
        return await dbContext.Locations.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && (x.Code == clean || x.Name == clean))
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<CompanyInventoryLocationOption>> GetLocationOptionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Locations.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CompanyInventoryLocationOption(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);
    }

    private async Task<CompanyInventoryItem?> FindExistingSupplierInventoryItemAsync(Guid organizationId, Guid supplierProductId, string? normalizedManufacturerPartNumber, string? brand, string? upc, CancellationToken cancellationToken)
    {
        var exact = await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.SupplierProductId == supplierProductId && x.IsActive, cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        var normalizedBrand = NormalizeGroupValue(brand);
        if (normalizedManufacturerPartNumber is not null)
        {
            var candidates = await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
                .Where(x => x.OrganizationId == organizationId && x.IsActive && x.NormalizedManufacturerPartNumber == normalizedManufacturerPartNumber)
                .ToListAsync(cancellationToken);
            var brandMatch = candidates.FirstOrDefault(x => normalizedBrand is not null && string.Equals(NormalizeGroupValue(x.Brand), normalizedBrand, StringComparison.OrdinalIgnoreCase));
            if (brandMatch is not null)
            {
                return brandMatch;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }
        }

        var cleanUpc = Clean(upc);
        return cleanUpc is null
            ? null
            : await dbContext.CompanyInventoryItems.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.IsActive && x.Upc == cleanUpc, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, ReplacementPrice>> GetReplacementPricesAsync(Guid organizationId, IReadOnlyCollection<CompanyInventoryItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new Dictionary<Guid, ReplacementPrice>();
        }

        var preferences = await GetSupplierPurchasePreferencesAsync(organizationId, cancellationToken);
        var itemIdentities = items
            .Select(item => new InventoryItemIdentity(
                item.Id,
                item.SupplierProductId,
                Clean(item.NormalizedManufacturerPartNumber) ?? NormalizePartNumber(item.ManufacturerPartNumber),
                NormalizeGroupValue(item.Brand)))
            .ToArray();
        var normalizedPartNumbers = itemIdentities
            .Select(x => x.NormalizedManufacturerPartNumber)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var exactSupplierProductIds = itemIdentities
            .Select(x => x.SupplierProductId)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (normalizedPartNumbers.Length == 0 && exactSupplierProductIds.Length == 0)
        {
            return new Dictionary<Guid, ReplacementPrice>();
        }

        var candidateRows = await (
            from supplierProduct in dbContext.SupplierProducts.IgnoreQueryFilters().AsNoTracking()
            join supplier in dbContext.Suppliers.IgnoreQueryFilters().AsNoTracking() on supplierProduct.SupplierId equals supplier.Id
            join globalProduct in dbContext.GlobalProducts.IgnoreQueryFilters().AsNoTracking() on supplierProduct.GlobalProductId equals globalProduct.Id
            where exactSupplierProductIds.Contains(supplierProduct.Id) ||
                (supplierProduct.NormalizedManufacturerPartNumber != null && normalizedPartNumbers.Contains(supplierProduct.NormalizedManufacturerPartNumber)) ||
                (globalProduct.NormalizedManufacturerPartNumber != null && normalizedPartNumbers.Contains(globalProduct.NormalizedManufacturerPartNumber))
            select new ReplacementCandidateRow(
                supplierProduct.Id,
                supplier.Code,
                supplierProduct.SupplierSku,
                globalProduct.Brand,
                globalProduct.Manufacturer,
                supplierProduct.ManufacturerPartNumber,
                supplierProduct.NormalizedManufacturerPartNumber,
                globalProduct.ManufacturerPartNumber,
                globalProduct.NormalizedManufacturerPartNumber,
                supplierProduct.WarehouseAvailability))
            .ToListAsync(cancellationToken);
        var candidateSupplierProductIds = candidateRows.Select(x => x.SupplierProductId).Distinct().ToArray();
        var companyPrices = candidateSupplierProductIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.CompanySupplierPrices.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && candidateSupplierProductIds.Contains(x.SupplierProductId))
                .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken);
        var supplierPriceRows = candidateSupplierProductIds.Length == 0
            ? new List<SupplierPrice>()
            : await dbContext.SupplierPrices.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => candidateSupplierProductIds.Contains(x.SupplierProductId))
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.LastUpdated)
                .ToListAsync(cancellationToken);
        var supplierPrices = supplierPriceRows
            .GroupBy(x => x.SupplierProductId)
            .ToDictionary(x => x.Key, x => x.First());

        return itemIdentities.ToDictionary(
            x => x.ItemId,
            x => SelectReplacementPrice(x, candidateRows, companyPrices, supplierPrices, preferences));
    }

    private async Task<SupplierPurchasePreferences> GetSupplierPurchasePreferencesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var json = await dbContext.BusinessConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.SupplierPreferencesJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return SupplierPurchasePreferences.Empty;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSupplierPurchasePreferences>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return SupplierPurchasePreferences.FromStored(stored);
        }
        catch (JsonException)
        {
            return SupplierPurchasePreferences.Empty;
        }
    }

    private static CompanyInventorySupplierLookupResult SupplierLookupNotFound(string message)
    {
        return new CompanyInventorySupplierLookupResult(false, message, null, null, null, null, null, null, null, null, null, null, null, null, false, 0, 0, []);
    }

    private static CompanyInventoryItemRow BuildRow(CompanyInventoryItem item, IEnumerable<CompanyInventoryLocationStock> stocks, IEnumerable<CompanyInventoryMovement> movements, IReadOnlyDictionary<Guid, string> locationNames, IReadOnlyDictionary<Guid, ReplacementPrice>? replacementPrices = null)
    {
        var stockRows = stocks
            .Select(x => new CompanyInventoryLocationStockRow(
                x.Id,
                x.LocationId,
                x.LocationId is not null && locationNames.TryGetValue(x.LocationId.Value, out var name) ? name : x.LocationNameSnapshot ?? "Company",
                x.BinLocation,
                x.QuantityOnHand,
                x.QuantityAllocated,
                x.QuantityAvailable,
                x.QuantityOnOrder,
                x.QuantityBackordered,
                x.MinQuantity,
                EffectiveMaxQuantity(x.MinQuantity, x.ReorderQuantity),
                x.MinQuantity,
                x.ReorderQuantity,
                x.StockInStore,
                x.AllowNegativeStock,
                IsLowStock(x)))
            .OrderBy(x => x.LocationName)
            .ToList();
        var movementRows = movements
            .Take(5)
            .Select(x => new CompanyInventoryMovementRow(
                x.CreatedAtUtc,
                x.MovementType,
                x.LocationId is not null && locationNames.TryGetValue(x.LocationId.Value, out var name) ? name : "Company",
                x.QuantityDelta,
                x.QuantityAfter,
                x.Reason,
                x.Notes))
            .ToList();

        return new CompanyInventoryItemRow(
            item.Id,
            item.SourceType,
            item.SourceSupplierCode,
            ReplacementSupplierCode(item, replacementPrices),
            ReplacementSupplierSku(item, replacementPrices),
            item.Sku,
            item.ManufacturerPartNumber,
            item.Upc,
            item.Brand,
            item.Title,
            item.Category,
            item.Status,
            item.ImageUrl,
            ReplacementRetail(item, replacementPrices),
            item.SalePrice,
            InventoryCost(item, replacementPrices),
            stockRows.Sum(x => x.QuantityOnHand),
            stockRows.Sum(x => x.QuantityAvailable),
            stockRows.Sum(x => x.QuantityAllocated),
            stockRows.Sum(x => x.QuantityOnOrder),
            item.IsStockedInStore,
            item.TrackInventory,
            stockRows.Any(x => x.IsLowStock),
            stockRows,
            movementRows);
    }

    private static bool IsLowStock(CompanyInventoryLocationStock stock)
    {
        var threshold = stock.MinQuantity;
        return threshold is not null && stock.QuantityAvailable <= threshold.Value;
    }

    private static ReplacementPrice SelectReplacementPrice(
        InventoryItemIdentity item,
        IReadOnlyCollection<ReplacementCandidateRow> candidates,
        IReadOnlyDictionary<Guid, decimal> companyPrices,
        IReadOnlyDictionary<Guid, SupplierPrice> supplierPrices,
        SupplierPurchasePreferences preferences)
    {
        var matchingCandidates = candidates
            .Where(candidate => CandidateMatchesItem(item, candidate))
            .Select(candidate =>
            {
                supplierPrices.TryGetValue(candidate.SupplierProductId, out var supplierPrice);
                companyPrices.TryGetValue(candidate.SupplierProductId, out var actualCost);
                return new ReplacementCandidate(
                    candidate.SupplierProductId,
                    candidate.SupplierCode,
                    candidate.SupplierSku,
                    PositivePrice(actualCost),
                    supplierPrice?.Msrp,
                    PositivePrice(supplierPrice?.DealerCost),
                    HasCachedInventory(candidate.WarehouseAvailability),
                    preferences.IsPreferredSupplier(candidate.SupplierCode));
            })
            .Where(x => x.ActualCost is not null || x.DealerCost is not null || x.RetailPrice is not null)
            .OrderBy(ServerPreferenceRank)
            .ThenBy(x => x.ActualCost ?? x.DealerCost ?? decimal.MaxValue)
            .ThenBy(x => x.SupplierCode)
            .ThenBy(x => x.SupplierSku)
            .ToList();
        var selected = matchingCandidates.FirstOrDefault();
        return selected is null
            ? ReplacementPrice.Empty
            : new ReplacementPrice(selected.ActualCost ?? selected.DealerCost, selected.RetailPrice, selected.SupplierProductId, selected.SupplierCode, selected.SupplierSku);
    }

    private static bool CandidateMatchesItem(InventoryItemIdentity item, ReplacementCandidateRow candidate)
    {
        if (item.SupplierProductId == candidate.SupplierProductId)
        {
            return true;
        }

        if (item.NormalizedManufacturerPartNumber is null)
        {
            return false;
        }

        var candidatePartNumber = Clean(candidate.GlobalNormalizedManufacturerPartNumber) ??
            Clean(candidate.SupplierNormalizedManufacturerPartNumber) ??
            NormalizePartNumber(candidate.GlobalManufacturerPartNumber) ??
            NormalizePartNumber(candidate.SupplierManufacturerPartNumber);
        if (!string.Equals(item.NormalizedManufacturerPartNumber, candidatePartNumber, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (item.NormalizedBrand is null)
        {
            return true;
        }

        var candidateBrand = NormalizeGroupValue(Clean(candidate.Manufacturer) ?? Clean(candidate.Brand));
        return candidateBrand is null || string.Equals(item.NormalizedBrand, candidateBrand, StringComparison.OrdinalIgnoreCase);
    }

    private static int ServerPreferenceRank(ReplacementCandidate offer)
    {
        if (offer.IsPreferredSupplier && offer.HasCachedInventory)
        {
            return 0;
        }

        if (offer.HasCachedInventory)
        {
            return 1;
        }

        return offer.IsPreferredSupplier ? 2 : 3;
    }

    private static decimal? ReplacementRetail(CompanyInventoryItem item, IReadOnlyDictionary<Guid, ReplacementPrice>? replacementPrices)
    {
        return replacementPrices is not null &&
            replacementPrices.TryGetValue(item.Id, out var replacementPrice) &&
            replacementPrice.RetailPrice is not null
            ? replacementPrice.RetailPrice
            : item.RetailPrice;
    }

    private static string? ReplacementSupplierCode(CompanyInventoryItem item, IReadOnlyDictionary<Guid, ReplacementPrice>? replacementPrices)
    {
        return replacementPrices is not null &&
            replacementPrices.TryGetValue(item.Id, out var replacementPrice) &&
            replacementPrice.SupplierCode is not null
            ? replacementPrice.SupplierCode
            : item.SourceSupplierCode;
    }

    private static string? ReplacementSupplierSku(CompanyInventoryItem item, IReadOnlyDictionary<Guid, ReplacementPrice>? replacementPrices)
    {
        return replacementPrices is not null &&
            replacementPrices.TryGetValue(item.Id, out var replacementPrice) &&
            replacementPrice.SupplierSku is not null
            ? replacementPrice.SupplierSku
            : item.SourceSupplierSku;
    }

    private static decimal? InventoryCost(CompanyInventoryItem item, IReadOnlyDictionary<Guid, ReplacementPrice>? replacementPrices)
    {
        if (replacementPrices is not null &&
            replacementPrices.TryGetValue(item.Id, out var replacementPrice) &&
            replacementPrice.Cost is not null)
        {
            return replacementPrice.Cost;
        }

        if (item.AverageCost is > 0m)
        {
            return item.AverageCost;
        }

        if (item.DefaultCost is > 0m)
        {
            return item.DefaultCost;
        }

        if (item.LastCost is > 0m)
        {
            return item.LastCost;
        }

        return null;
    }

    private static Guid RequireLocationId(Guid? locationId)
    {
        return locationId is { } value && value != Guid.Empty
            ? value
            : throw new InvalidOperationException("Choose an inventory location.");
    }

    private static StockPolicy NormalizeStockPolicy(bool stockInStore, decimal? minQuantity, decimal? reorderQuantity)
    {
        var min = minQuantity;
        var reorder = reorderQuantity;
        if (stockInStore)
        {
            min ??= 1m;
            reorder ??= 1m;
        }

        if (stockInStore && (min is null || reorder is null || min <= 0m || reorder <= 0m))
        {
            throw new InvalidOperationException("Stocked in store items require minimum and reorder quantity greater than zero.");
        }

        var max = EffectiveMaxQuantity(min, reorder);
        return new StockPolicy(min, reorder, max);
    }

    private static void ApplyInventoryItemOverrides(CompanyInventoryItem item, string? assignUpc, decimal? salePrice)
    {
        var upc = Clean(assignUpc);
        if (!string.IsNullOrWhiteSpace(upc))
        {
            item.Upc = upc;
        }

        if (salePrice is not null)
        {
            item.SalePrice = salePrice;
        }
    }

    private static decimal? PositivePrice(decimal? value)
    {
        return value is > 0m ? value : null;
    }

    private static bool HasCachedInventory(string? warehouseAvailability)
    {
        if (string.IsNullOrWhiteSpace(warehouseAvailability))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(warehouseAvailability);
            return JsonHasInventory(document.RootElement);
        }
        catch (JsonException)
        {
            return warehouseAvailability.Contains("in stock", StringComparison.OrdinalIgnoreCase) ||
                warehouseAvailability.Contains("available", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool JsonHasInventory(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.Name.Contains("qty", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Contains("quantity", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Contains("available", StringComparison.OrdinalIgnoreCase) ||
                            property.Name.Contains("stock", StringComparison.OrdinalIgnoreCase)) &&
                        JsonElementIndicatesInventory(property.Value))
                    {
                        return true;
                    }

                    if (JsonHasInventory(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(JsonHasInventory);
            default:
                return JsonElementIndicatesInventory(element);
        }
    }

    private static bool JsonElementIndicatesInventory(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out var value) && value > 0m,
            JsonValueKind.String => InventoryStringIndicatesStock(element.GetString()),
            JsonValueKind.True => true,
            _ => false
        };
    }

    private static bool InventoryStringIndicatesStock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed > 0m
            : value.Contains("in stock", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("available", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("25+", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal? EffectiveMaxQuantity(decimal? minQuantity, decimal? reorderQuantity)
    {
        return minQuantity is null || reorderQuantity is null ? null : minQuantity + reorderQuantity;
    }

    private async Task<Guid> SelectSupplierProductCandidateAsync(Guid organizationId, IReadOnlyCollection<SupplierProductLookupCandidate> candidates, CancellationToken cancellationToken)
    {
        var distinct = candidates
            .GroupBy(x => x.SupplierProductId)
            .Select(x => x.First())
            .ToArray();
        if (distinct.Length == 1)
        {
            return distinct[0].SupplierProductId;
        }

        var preferences = await GetSupplierPurchasePreferencesAsync(organizationId, cancellationToken);
        var supplierProductIds = distinct.Select(x => x.SupplierProductId).ToArray();
        var companyPrices = await dbContext.CompanySupplierPrices.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && supplierProductIds.Contains(x.SupplierProductId))
            .ToDictionaryAsync(x => x.SupplierProductId, x => x.ActualDealerCost, cancellationToken);

        return distinct
            .Select(candidate =>
            {
                companyPrices.TryGetValue(candidate.SupplierProductId, out var actualCost);
                return new
                {
                    Candidate = candidate,
                    ActualCost = actualCost > 0m ? (decimal?)actualCost : null,
                    HasInventory = HasCachedInventory(candidate.WarehouseAvailability),
                    IsPreferredSupplier = preferences.IsPreferredSupplier(candidate.SupplierCode)
                };
            })
            .OrderBy(x => x.IsPreferredSupplier && x.HasInventory ? 0 : x.HasInventory ? 1 : x.IsPreferredSupplier ? 2 : 3)
            .ThenBy(x => x.ActualCost ?? decimal.MaxValue)
            .ThenBy(x => x.Candidate.SupplierCode)
            .ThenBy(x => x.Candidate.SupplierSku)
            .First()
            .Candidate
            .SupplierProductId;
    }

    private static bool EqualsIgnoreCase(string? value, string compareTo)
    {
        return !string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), compareTo, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSupplierTitle(string? brand, string? description, string? mfgPartNumber)
    {
        var title = string.Join(" ", new[] { Clean(brand), Clean(description) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(mfgPartNumber) && !title.Contains(mfgPartNumber, StringComparison.OrdinalIgnoreCase))
        {
            title = $"{title} - {mfgPartNumber}";
        }

        return string.IsNullOrWhiteSpace(title) ? Required(mfgPartNumber, "Supplier title") : title;
    }

    private static string? FirstImage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        return Clean(element.GetString());
                    }

                    if (element.ValueKind == JsonValueKind.Object &&
                        (element.TryGetProperty("url", out var url) || element.TryGetProperty("Url", out url)) &&
                        url.ValueKind == JsonValueKind.String)
                    {
                        return Clean(url.GetString());
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static string NormalizeHeader(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string? Value(IReadOnlyDictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys.Select(NormalizeHeader))
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static decimal? DecimalValue(IReadOnlyDictionary<string, string?> row, params string[] keys)
    {
        var value = Value(row, keys);
        if (value is null)
        {
            return null;
        }

        value = value.Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool BoolValue(IReadOnlyDictionary<string, string?> row, params string[] keys)
    {
        var value = Value(row, keys);
        return value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1");
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Required(string? value, string label)
    {
        return Clean(value) ?? throw new InvalidOperationException($"{label} is required.");
    }

    private static string? NormalizePartNumber(string? value)
    {
        var clean = Clean(value);
        return clean is null ? null : new string(clean.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string? NormalizeGroupValue(string? value)
    {
        if (Clean(value) is not { } clean)
        {
            return null;
        }

        var normalized = new string(clean.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return MakerAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }

    private sealed record SupplierProductLookupCandidate(
        Guid SupplierProductId,
        string SupplierCode,
        string SupplierSku,
        string? SourceSupplierProductId,
        string? SupplierPartNumber,
        string? ManufacturerPartNumber,
        string? NormalizedManufacturerPartNumber,
        string? WarehouseAvailability,
        string? Upc,
        string? GlobalManufacturerPartNumber,
        string? GlobalNormalizedManufacturerPartNumber);

    private sealed record InventoryItemIdentity(
        Guid ItemId,
        Guid? SupplierProductId,
        string? NormalizedManufacturerPartNumber,
        string? NormalizedBrand);

    private sealed record ReplacementCandidateRow(
        Guid SupplierProductId,
        string SupplierCode,
        string SupplierSku,
        string? Brand,
        string? Manufacturer,
        string? SupplierManufacturerPartNumber,
        string? SupplierNormalizedManufacturerPartNumber,
        string? GlobalManufacturerPartNumber,
        string? GlobalNormalizedManufacturerPartNumber,
        string? WarehouseAvailability);

    private sealed record ReplacementCandidate(
        Guid SupplierProductId,
        string SupplierCode,
        string SupplierSku,
        decimal? ActualCost,
        decimal? RetailPrice,
        decimal? DealerCost,
        bool HasCachedInventory,
        bool IsPreferredSupplier);

    private sealed record ReplacementPrice(
        decimal? Cost,
        decimal? RetailPrice,
        Guid? SupplierProductId,
        string? SupplierCode,
        string? SupplierSku)
    {
        public static ReplacementPrice Empty { get; } = new(null, null, null, null, null);
    }

    private sealed record StoredSupplierPurchasePreferences(
        string? PreferredSupplierCode,
        IReadOnlyDictionary<string, string>? PreferredWarehouseCodes);

    private sealed record SupplierPurchasePreferences(
        string? PreferredSupplierCode,
        IReadOnlyDictionary<string, string> PreferredWarehouseCodes)
    {
        public static SupplierPurchasePreferences Empty { get; } = new(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public static SupplierPurchasePreferences FromStored(StoredSupplierPurchasePreferences? stored)
        {
            var preferredSupplierCode = NormalizeSupplierCode(stored?.PreferredSupplierCode);
            var warehouseCodes = stored?.PreferredWarehouseCodes is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : stored.PreferredWarehouseCodes
                    .Where(x => NormalizeSupplierCode(x.Key) is not null && Clean(x.Value) is not null)
                    .ToDictionary(x => NormalizeSupplierCode(x.Key)!, x => Clean(x.Value)!, StringComparer.OrdinalIgnoreCase);
            return new SupplierPurchasePreferences(preferredSupplierCode, warehouseCodes);
        }

        public bool IsPreferredSupplier(string supplierCode)
        {
            return PreferredSupplierCode is not null &&
                string.Equals(PreferredSupplierCode, supplierCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeSupplierCode(string? value)
        {
            var clean = Clean(value)?.ToUpperInvariant();
            return clean is "WPS" or "TURN14" or "PU" ? clean : null;
        }
    }

    private sealed record StockPolicy(decimal? MinQuantity, decimal? ReorderQuantity, decimal? MaxQuantity);
}
