# Global Catalog Architecture Specification

Status: foundational architecture specification.

The iM1 OS catalog architecture is built around one global source of product truth for the platform, while each tenant owns only its company-specific business data. Product identity, supplier mappings, vehicle fitment, and shared catalog enrichment belong to iM1. Inventory, pricing overrides, costs, purchasing history, sales history, accounting behavior, and operational notes remain isolated to the tenant.

This specification is the target architecture for future product, supplier, inventory, purchasing, sales, service, ecommerce, analytics, and AI work.

## Objective

Redesign the product architecture so product information exists only once across the platform. The system should eliminate duplicate product storage across tenants, centralize supplier catalogs and fitment data, reduce synchronization complexity, and establish a durable foundation for AI, analytics, ecommerce, and supplier integrations.

## Core Principles

### Global Data Belongs To iM1

There should only ever be one canonical record for shared catalog concepts:

- Products
- Vehicle database records, including year, make, model, and submodel
- Fitment
- Brands
- Manufacturers
- Categories
- Product attributes
- Images
- Specifications
- Cross references
- UPCs
- Hazmat information

### Company Data Belongs To The Tenant

Tenant-owned data is never shared across companies:

- Inventory
- Selling price
- Cost
- Vendor selection
- Bin locations
- Accounting codes
- Reorder levels
- Stock levels
- Purchase history
- Sales history
- Work order history
- Customer pricing
- Online visibility
- Internal notes

## Scale-Ready Physical Database Design

The logical catalog layers above must be implemented as a physical data model that can handle:

- Hundreds of thousands to millions of supplier item rows.
- Millions to tens of millions of source fitment rows.
- Many suppliers describing the same manufacturer part differently.
- Company-specific price sheets and supplier accounts.
- Company-owned inventory that is separate from supplier warehouse inventory.
- Repeated imports where source files can change without user action.

The correct separation is:

```text
Global catalog          = what the product is
Supplier catalog        = how each supplier sells it
Supplier inventory      = what each supplier currently has available
Company supplier price  = what this company pays this supplier
Company inventory       = what this shop physically owns
```

Supplier data should never overwrite company data. Company data should never be pushed into the global catalog.

### Supplier Source Files

Supplier downloads and API responses must be tracked independently from normalized catalog tables.

`supplier_import_files`

- `Id`
- `SupplierId`
- `ConnectorKey`
- `ImportRunId`
- `SourceUrl`
- `SourceName`
- `SourceType`
- `ContentType`
- `ContentLength`
- `ETag`
- `LastModifiedAtUtc`
- `DownloadedAtUtc`
- `Checksum`
- `StoragePath`
- `Status`

Purpose:

- Identify whether a source file changed before reprocessing.
- Preserve import lineage for every row produced by a file.
- Support replays without downloading the file again.
- Avoid relying on user action as the trigger for supplier file updates.

For WPS Master Item List, the `SourceUrl` is:

```text
https://data-depot.s3.us-west-2.amazonaws.com/v4/downloads/master-item-list/master-item-list.json
```

The import worker should perform `HEAD` first and compare `ETag`, `Last-Modified`, and `Content-Length`. If none changed since the last successful import, the worker can skip the expensive parse.

### Suppliers

`suppliers`

- `Id`
- `Name`
- `Code`
- `ConnectorKey`
- `IsActive`

Examples:

- `WPS`
- `PARTS_UNLIMITED`
- `TUCKER`
- `TURN14`
- `OEM_KTM`

### Supplier Connector Configuration

`supplier_connector_configurations`

- `Id`
- `SupplierId`
- `ConnectorKey`
- `DisplayName`
- `BaseApiUrl`
- `MasterFileUrl`
- `DealerAccountNumber`
- `Username`
- `ApiKey`
- `ApiSecretProtected`
- `AuthMode`
- `IsEnabled`
- `ImportMasterFileOnSchedule`
- `MasterFileImportMode`
- `LastConnectionTestAtUtc`
- `LastConnectionStatus`
- `LastConnectionMessage`

This is platform-owned configuration. It should be one row per platform supplier connector, not one row per tenant.

Company-specific supplier credentials belong in company supplier account tables, not here.

### Global Brands And Manufacturers

Free-text brand names from suppliers should eventually be normalized.

`global_brands`

- `Id`
- `Name`
- `NormalizedName`
- `ManufacturerId`
- `Status`

`global_manufacturers`

- `Id`
- `Name`
- `NormalizedName`
- `Status`

`supplier_brand_aliases`

- `Id`
- `SupplierId`
- `SourceBrandName`
- `NormalizedSourceBrandName`
- `GlobalBrandId`
- `Confidence`
- `Status`

Purpose:

- Prevent `NGK`, `N.G.K.`, and supplier-specific brand spellings from creating duplicate product identities.
- Make manufacturer part number matching safer by pairing it with normalized brand identity.

### Global Products

`global_products`

- `Id`
- `GlobalBrandId`
- `Brand`
- `ManufacturerId`
- `Manufacturer`
- `ManufacturerPartNumber`
- `NormalizedManufacturerPartNumber`
- `Description`
- `LongDescription`
- `CategoryId`
- `CategoryPath`
- `Upc`
- `Length`
- `Width`
- `Height`
- `Weight`
- `ImagesJson`
- `SpecificationsJson`
- `HazmatJson`
- `RegulatoryJson`
- `Status`
- `IsActive`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Important indexes:

- Unique or filtered index on `Upc` when populated and reliable.
- Index on `GlobalBrandId, NormalizedManufacturerPartNumber`.
- Index on `NormalizedManufacturerPartNumber`.
- Full-text/search index on description, brand, UPC, and part numbers.

Rule:

`ManufacturerPartNumber` is a match key, but it is not globally unique by itself. Use brand/manufacturer plus normalized manufacturer part number, then validate with UPC and supplier evidence.

### Supplier Products

`supplier_products`

- `Id`
- `SupplierId`
- `GlobalProductId`
- `SupplierSku`
- `SupplierDescription`
- `SupplierPartNumber`
- `ManufacturerPartNumber`
- `NormalizedManufacturerPartNumber`
- `Upc`
- `Brand`
- `NormalizedBrand`
- `SupplierStatus`
- `Packaging`
- `MinimumOrder`
- `CaseQuantity`
- `WarehouseAvailability`
- `SupplierImagesJson`
- `SourceDataJson`
- `LastImportFileId`
- `LastImportRunId`
- `LastSyncedAtUtc`
- `IsActive`

Important indexes:

- Unique index on `SupplierId, SupplierSku`.
- Index on `GlobalProductId`.
- Index on `SupplierId, NormalizedManufacturerPartNumber`.
- Index on `NormalizedManufacturerPartNumber`.
- Index on `SupplierId, Upc`.

Rule:

Every supplier item row must preserve the complete raw supplier payload in `SourceDataJson`. Typed columns are for searching, matching, joins, and reporting. Raw JSON is for source fidelity and future field recovery.

For WPS Master Item List:

```text
sku                         -> SupplierSku
name                        -> SupplierDescription
vendor_number               -> ManufacturerPartNumber
brand                       -> Brand
status                      -> SupplierStatus
upc                         -> Upc
primary_item_image          -> SupplierImagesJson
all original fields         -> SourceDataJson
```

### Product Matching

Supplier-product-to-global-product matching should be explicit and auditable.

`supplier_product_matches`

- `Id`
- `SupplierProductId`
- `GlobalProductId`
- `MatchType`
- `Confidence`
- `Status`
- `MatchedBy`
- `MatchedAtUtc`
- `ReviewedBy`
- `ReviewedAtUtc`
- `EvidenceJson`

Allowed statuses:

- `Proposed`
- `Accepted`
- `Rejected`
- `ManualReview`
- `Superseded`

Purpose:

- Avoid hiding match decisions in `supplier_products.GlobalProductId`.
- Preserve why a supplier SKU maps to a global product.
- Support rematching when supplier data improves.
- Support manual review and merge/split tools.

Matching order:

1. Existing accepted supplier product match.
2. Supplier SKU mapping.
3. UPC.
4. Brand plus normalized manufacturer part number.
5. Normalized manufacturer part number with review if ambiguous.
6. Fuzzy title/category match only as a low-confidence candidate.
7. Manual review.

### Product Cross References

Cross references should not be encoded as duplicate products.

`global_product_cross_references`

- `Id`
- `GlobalProductId`
- `RelatedGlobalProductId`
- `RelationshipType`
- `SourceSupplierId`
- `SourceDataJson`
- `Confidence`
- `Status`

Examples:

- `Supersedes`
- `SupersededBy`
- `Equivalent`
- `Alternate`
- `KitComponent`
- `Accessory`

### Supplier Pricing

Supplier-published pricing is global supplier data. It is not company-specific dealer pricing.

`supplier_price_snapshots`

- `Id`
- `SupplierProductId`
- `ImportRunId`
- `Currency`
- `Msrp`
- `Map`
- `AdvertisedPrice`
- `StandardDealerPrice`
- `CoreCharge`
- `EnvironmentalFee`
- `DropShipFee`
- `EffectiveDate`
- `ExpirationDate`
- `CapturedAtUtc`
- `SourceDataJson`

Important indexes:

- Index on `SupplierProductId, CapturedAtUtc DESC`.
- Index on `SupplierProductId, EffectiveDate, ExpirationDate`.

Rule:

Historical prices are append-only. Current price can be represented by a view/materialized view or by selecting the latest effective snapshot.

### Company Supplier Accounts

Company-specific supplier relationship data belongs here.

`company_supplier_accounts`

- `Id`
- `CompanyId`
- `SupplierId`
- `AccountNumber`
- `Username`
- `ApiKeyProtected`
- `ApiSecretProtected`
- `CredentialMode`
- `IsEnabled`
- `PreferredOrderMethod`
- `DefaultWarehousePreference`
- `LastConnectionTestAtUtc`
- `LastConnectionStatus`

Purpose:

- Support per-company supplier credentials.
- Support companies with different dealer accounts and price access.
- Keep platform connector credentials separate from tenant supplier accounts.

### Company Supplier Pricing

Actual dealer cost is company-specific and must not be stored as global truth.

`company_supplier_price_sheets`

- `Id`
- `CompanyId`
- `SupplierId`
- `SourceName`
- `SourceType`
- `UploadedByUserId`
- `ImportedAtUtc`
- `EffectiveDate`
- `ExpirationDate`
- `Status`
- `SourceFilePath`
- `SourceDataJson`

`company_supplier_prices`

- `Id`
- `CompanyId`
- `SupplierProductId`
- `CompanySupplierPriceSheetId`
- `Currency`
- `DealerCost`
- `ContractPrice`
- `DiscountPercent`
- `CoreCharge`
- `FreightEstimate`
- `EffectiveDate`
- `ExpirationDate`
- `CapturedAtUtc`
- `SourceDataJson`

Important indexes:

- Unique or filtered index on `CompanyId, SupplierProductId, EffectiveDate, CompanySupplierPriceSheetId`.
- Index on `CompanyId, SupplierProductId, EffectiveDate DESC`.

Rule:

Price comparison must use `company_supplier_prices` first. If no company-specific price exists, fall back to supplier-published pricing and mark the result as estimated.

### Supplier Inventory

Supplier warehouse inventory is supplier data and should be stored as snapshots or events, not as company stock.

`supplier_inventory_snapshots`

- `Id`
- `SupplierProductId`
- `SupplierId`
- `WarehouseCode`
- `WarehouseName`
- `QuantityAvailable`
- `QuantityDisplay`
- `AvailabilityStatus`
- `IsDropShipAvailable`
- `CapturedAtUtc`
- `ExpiresAtUtc`
- `SourceDataJson`

Important indexes:

- Index on `SupplierProductId, CapturedAtUtc DESC`.
- Index on `SupplierId, WarehouseCode, CapturedAtUtc DESC`.

Rule:

If a supplier caps inventory display, such as returning `25+`, store both a numeric comparable value and the supplier display value.

### Company Inventory

Company inventory is tenant-owned operational data. It is separate from supplier inventory.

`company_inventory_items`

- `Id`
- `CompanyId`
- `GlobalProductId`
- `DefaultSupplierProductId`
- `InternalSku`
- `Barcode`
- `DescriptionOverride`
- `InventoryMethod`
- `ValuationMethod`
- `IsStocked`
- `IsActive`

`company_inventory_locations`

- `Id`
- `CompanyId`
- `Name`
- `LocationType`
- `IsActive`

`company_inventory_bins`

- `Id`
- `CompanyId`
- `LocationId`
- `Code`
- `Description`
- `IsActive`

`company_inventory_balances`

- `Id`
- `CompanyId`
- `CompanyInventoryItemId`
- `LocationId`
- `BinId`
- `QuantityOnHand`
- `QuantityReserved`
- `QuantityAvailable`
- `QuantityOnOrder`
- `ReorderPoint`
- `ReorderQuantity`
- `MinQuantity`
- `MaxQuantity`
- `UpdatedAtUtc`

`company_inventory_transactions`

- `Id`
- `CompanyId`
- `CompanyInventoryItemId`
- `LocationId`
- `BinId`
- `TransactionType`
- `Quantity`
- `UnitCost`
- `ReferenceType`
- `ReferenceId`
- `OccurredAtUtc`
- `CreatedByUserId`
- `Notes`

`company_inventory_cost_layers`

- `Id`
- `CompanyId`
- `CompanyInventoryItemId`
- `QuantityRemaining`
- `UnitCost`
- `SourceTransactionId`
- `ReceivedAtUtc`

Rule:

Inventory balances are operationally convenient, but inventory history must come from immutable transactions. Quantity overwrites are not acceptable except as posted adjustment transactions.

### Vehicles

`global_vehicles`

- `Id`
- `Year`
- `Make`
- `Model`
- `Submodel`
- `Engine`
- `VinRange`
- `Market`
- `Notes`

Important indexes:

- Unique index on normalized year/make/model/submodel/engine/market.
- Search index on make/model/submodel/engine.

Supplier vehicle identifiers should not be placed directly on `global_vehicles`.

### Supplier Fitment Source Records

Fitment imports will be large. Preserve source rows before resolving them to canonical vehicles.

`supplier_fitment_records`

- `Id`
- `SupplierId`
- `SupplierProductId`
- `ImportRunId`
- `SourceVehicleId`
- `SourceYear`
- `SourceMake`
- `SourceModel`
- `SourceSubmodel`
- `SourceEngine`
- `SourcePosition`
- `SourceQuantity`
- `SourceNotes`
- `NormalizedVehicleKey`
- `CandidateGlobalVehicleId`
- `ResolutionStatus`
- `SourceDataJson`
- `ImportedAtUtc`

Important indexes:

- Index on `SupplierProductId`.
- Index on `SupplierId, SourceVehicleId`.
- Index on `NormalizedVehicleKey`.
- Index on `CandidateGlobalVehicleId`.
- Index on `ResolutionStatus`.

Expected statuses:

- `Unresolved`
- `Resolved`
- `Ambiguous`
- `Rejected`

Rule:

Never import supplier fitment directly into canonical fitment without source retention. Source fitment is evidence; canonical fitment is the resolved conclusion.

### Canonical Vehicle Fitment

`vehicle_fitments`

- `Id`
- `GlobalProductId`
- `GlobalVehicleId`
- `Quantity`
- `Position`
- `Notes`
- `Status`
- `Confidence`

`vehicle_fitment_sources`

- `Id`
- `VehicleFitmentId`
- `SupplierFitmentRecordId`
- `SupplierId`
- `EvidenceType`
- `Confidence`
- `Status`

Important indexes:

- Unique or filtered index on `GlobalProductId, GlobalVehicleId, Position`.
- Index on `GlobalVehicleId, GlobalProductId`.
- Index on `VehicleFitmentId`.

Purpose:

- Support millions of supplier fitment rows without polluting canonical fitment.
- Allow conflicting supplier fitment evidence.
- Allow review, correction, and source traceability.

### Product Attributes

Attributes need structure once imports move beyond the WPS master file.

`attribute_definitions`

- `Id`
- `Name`
- `NormalizedName`
- `DataType`
- `Unit`
- `CategoryId`
- `Status`

`global_product_attributes`

- `Id`
- `GlobalProductId`
- `AttributeDefinitionId`
- `ValueText`
- `ValueNumber`
- `ValueBoolean`
- `ValueJson`
- `SourceDataJson`

`supplier_product_attributes`

- `Id`
- `SupplierProductId`
- `AttributeDefinitionId`
- `SourceName`
- `ValueText`
- `ValueNumber`
- `ValueBoolean`
- `ValueJson`
- `SourceDataJson`

Rule:

Do not bury searchable attributes only inside JSON once they are known to drive fitment, filtering, ecommerce search, or reporting.

### Product Images And Resources

Image URLs in supplier files should be normalized separately from raw JSON when multiple images become available.

`product_media_assets`

- `Id`
- `GlobalProductId`
- `SupplierProductId`
- `SupplierId`
- `MediaType`
- `Url`
- `CdnUrl`
- `SortOrder`
- `AltText`
- `IsPrimary`
- `SourceDataJson`
- `Status`

Purpose:

- Support multiple supplier images.
- Deduplicate identical media.
- Allow image quality selection and ecommerce publishing rules.

### Search And Read Models

High-volume catalog search should not be built only from OLTP joins.

Recommended read models:

- `product_search_documents`
- `supplier_product_search_documents`
- `company_inventory_search_documents`

These can be materialized tables, database views, or an external search index. They should denormalize:

- Global product identity.
- Supplier SKUs.
- Manufacturer part numbers.
- UPCs.
- Brand aliases.
- Fitment summaries.
- Company on-hand inventory.
- Company-specific price availability.

### Partitioning And Retention

Tables likely to need partitioning or retention policy:

- `supplier_fitment_records`
- `supplier_inventory_snapshots`
- `supplier_price_snapshots`
- `company_supplier_prices`
- `company_inventory_transactions`
- `supplier_connector_import_runs`

Partition candidates:

- By supplier for supplier fitment.
- By captured/import month for snapshots and import history.
- By company for high-volume company operational tables if tenant growth requires it.

Retention:

- Keep canonical catalog rows indefinitely.
- Keep source import files long enough for audit and replay.
- Keep price and inventory snapshots according to reporting requirements.
- Never delete company inventory transactions that affect valuation or audit history.

### Price Comparison Query Shape

The price comparison workflow should use this order:

1. Resolve the requested part to `GlobalProductId`.
2. Find all active `supplier_products` for that `GlobalProductId`.
3. Join latest `supplier_inventory_snapshots` for availability.
4. Join latest effective `company_supplier_prices` for the requesting company.
5. Fall back to latest effective `supplier_price_snapshots` when company price is missing.
6. Include company on-hand inventory from `company_inventory_balances`.
7. Rank by total business cost, not only unit price.

Total business cost should consider:

- Actual company dealer cost.
- Supplier availability.
- Company on-hand quantity.
- Freight/drop-ship fee.
- Lead time.
- Minimum order quantity.
- Case quantity.
- Margin target.
- Service deadline.

## Catalog Layers

The catalog consists of five logical layers. These layers explain ownership boundaries. The scale-ready physical design above is the implementation target for high-volume imports.

### Layer 1: Global Vehicle Database

Vehicles are represented by one shared database. Vehicle definitions are not duplicated per tenant.

`Vehicle` contains:

- `VehicleId`
- `Year`
- `Make`
- `Model`
- `Submodel`
- `Engine`
- `VinRange`
- `Market`
- `Notes`

### Layer 2: Global Product Catalog

Each physical product has one shared product record. The global product contains only information intrinsic to the product.

`GlobalProduct` contains:

- `GlobalProductId`
- `Brand`
- `Manufacturer`
- `Description`
- `LongDescription`
- `Category`
- `Upc`
- `Dimensions`
- `Weight`
- `Images`
- `Specifications`
- `Status`

`GlobalProduct` must not contain supplier-specific pricing or company-specific information.

### Layer 3: Supplier Catalog

Supplier products map supplier-specific listings to global products. Multiple suppliers can sell the same global product.

`SupplierProduct` contains:

- `SupplierProductId`
- `SupplierId`
- `GlobalProductId`
- `SupplierSku`
- `SupplierDescription`
- `SupplierPartNumber`
- `SupplierStatus`
- `Packaging`
- `MinimumOrder`
- `CaseQuantity`
- `WarehouseAvailability`
- `SupplierImages`

Example:

```text
GlobalProduct: NGK BR8ES Spark Plug

Maps to:
  - WPS SKU
  - Parts Unlimited SKU
  - Turn14 SKU
  - OEM KTM SKU
```

Each supplier listing references the same `GlobalProductId`.

### Layer 4: Supplier Pricing

Supplier pricing is independent from product identity.

`SupplierPrice` contains:

- `SupplierPriceId`
- `SupplierProductId`
- `Msrp`
- `Map`
- `StandardDealerPrice`
- `EffectiveDate`
- `ExpirationDate`
- `LastUpdated`

This represents supplier-published pricing only. Actual dealer cost and negotiated pricing are company-specific and belong in company supplier pricing tables.

### Layer 5: Company Data

Each company stores only its business-specific product information. This table should remain small compared to the global catalog.

`CompanyProduct` contains:

- `CompanyProductId`
- `CompanyId`
- `GlobalProductId`
- `SellingPrice`
- `PriceLevel`
- `ActualCost`
- `PreferredSupplier`
- `InventoryMethod`
- `BinLocation`
- `ReorderPoint`
- `MinQty`
- `OnlineVisible`
- `PosVisible`
- `ServiceVisible`
- `AccountingCode`
- `TaxCategory`
- `InternalDescription`
- `InternalNotes`

## Company Supplier Pricing

Negotiated supplier pricing is company-specific and must be stored independently from published supplier pricing.

`CompanySupplierPrice` contains:

- `CompanyId`
- `SupplierProductId`
- `ActualCost`
- `ContractPrice`
- `LastRetrieved`
- `EffectiveDate`
- `PriceSource`

Example:

```text
Published dealer cost: $100

Dealer A actual cost: $91
Dealer B actual cost: $94
Dealer C actual cost: $87
```

The global product remains identical for all companies.

## Fitment

Fitment is global and shared across tenants.

`VehicleFitment` contains:

- `GlobalProductId`
- `VehicleId`
- `Quantity`
- `Position`
- `Notes`

Every company uses the same fitment relationship.

At scale, supplier fitment must first land in source fitment records and then be resolved into canonical fitment. The canonical `VehicleFitment` relationship is the conclusion; source fitment rows are the evidence.

## Inventory

Inventory remains company-owned operational data.

`Inventory` contains:

- `CompanyId`
- `GlobalProductId`
- `LocationId`
- `QtyOnHand`
- `QtyReserved`
- `QtyOnOrder`

Inventory changes should be represented through inventory transactions. Silent quantity overwrites are not acceptable for operational history.

## Purchasing

Purchase order lines reference `SupplierProduct` because purchasing is supplier-specific, while still retaining the global product identity.

Purchase order lines contain:

- `SupplierProductId`
- `GlobalProductId`
- `ActualCost`
- `ReceivedCost`

## Sales

Invoice lines reference `GlobalProduct`.

Invoice lines contain:

- `GlobalProductId`
- `CompanySellingPrice`
- `Quantity`
- `InventoryLocation`

## Service

Work order parts reference `GlobalProduct`.

Work order part lines contain:

- `GlobalProductId`
- `Quantity`
- `SellingPrice`
- `Technician`

## Search

All product searches should originate from the global catalog. A `CompanyProduct` record should be created only when tenant customization is required.

Examples that justify `CompanyProduct` creation:

- Company sets inventory behavior.
- Company changes selling price.
- Company changes accounting behavior.
- Company adds internal notes.

Until customization is required, tenant workflows should reference the global product directly. This minimizes storage and synchronization work.

## Supplier Connectors

Supplier connectors such as WPS, Parts Unlimited, Turn14, and OEM connectors import only into the global catalog. Supplier synchronization must not create or modify tenant catalogs.

Supplier import workflow:

```text
Supplier API
  -> SupplierProduct
  -> Global product matching
  -> Update existing product or create new product
  -> Update global fitment
  -> Update supplier pricing
```

## Product Matching

All supplier connectors must use a reusable product matching engine.

Matching order:

1. Supplier SKU mapping
2. UPC
3. Manufacturer part number
4. Brand and part number
5. Manual review queue

## Historical Pricing

Pricing history must never be overwritten. Price changes should create history records for audit, margin analysis, historical reporting, cost trends, and price change notifications.

Required history concepts:

- `SupplierPriceHistory`
- `CompanySupplierPriceHistory`

## Future AI Services

A global product catalog allows future AI services to operate against consistent product identity:

- Duplicate detection
- Supplier cross references
- Smart substitutions
- Inventory forecasting
- Demand forecasting
- Universal product search
- Automatic category suggestions
- Automatic attribute normalization
- Fitment anomaly detection

## Execution Plan

### Phase 1: Global Catalog Foundation

- Create global catalog schema.
- Create global vehicle schema.
- Create supplier product schema.
- Create supplier price schema.
- Create product matching service.
- Do not build UI in this phase.

### Phase 2: Supplier Import Refactor

- Modify the supplier import pipeline.
- Populate the global catalog from all supplier imports.
- Stop creating tenant catalog records during supplier synchronization.

### Phase 3: Company Product Overrides

- Implement `CompanyProduct`.
- Create company-specific overrides.
- Implement lazy creation of `CompanyProduct` records.

### Phase 4: Operational Refactor

- Refactor purchasing to reference `GlobalProductId` and `SupplierProductId` where appropriate.
- Refactor inventory to reference `GlobalProductId`.
- Refactor POS and sales to reference `GlobalProductId`.
- Refactor work orders and service parts to reference `GlobalProductId`.

### Phase 5: Global Catalog Administration

Build administrative tools for:

- Product merge
- Product split
- Duplicate review
- Product matching queue
- Fitment review
- Supplier mapping review

## Success Criteria

- One canonical global product record exists for every product in the platform.
- One canonical vehicle database exists for every tenant.
- Supplier catalogs synchronize only into the global catalog.
- Company catalogs contain only company-specific business information.
- Inventory, pricing, accounting, and history remain tenant-isolated.
- Future modules use `GlobalProductId` as the primary product reference.

This architecture is the permanent catalog foundation of iM1 OS.
