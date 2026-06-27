# Parts Domain Implementation Proposal

Status: proposed for review before implementation.

This proposal translates `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md` into the first build milestone for the IM1OS Parts Domain. This is not a UI milestone.

## Goal

Implement the reusable Parts Domain foundation that future modules consume.

The Parts Domain should model canonical part identity, supplier listings, local inventory, purchase ordering, receiving, inventory transactions, and purchase recommendation interfaces.

## Scope

Build domain and application foundation for:

- Canonical manufacturer parts
- Supplier listings mapped to manufacturer parts
- Local inventory
- Inventory transactions
- Purchase orders
- Receiving
- Barcode and SKU lookup
- Vendor mapping
- Purchase Intelligence Engine interfaces
- Supplier connector abstractions

Do not build:

- Intake UI
- Technician UI
- Customer portal UI
- Supplier API implementation
- WPS connector implementation
- Ecommerce connector implementation
- AI recommendation generation

## Proposed Domain Entities

### ManufacturerPart

Canonical identity for a physical part.

Core fields:

- `Id`
- `OrganizationId` when tenant-owned or curated locally
- `ManufacturerPartNumber`
- `Upc`
- `Brand`
- `Description`
- `Category`
- `Weight`
- `Dimensions`
- `IsHazmat`
- `MapPrice`
- `Msrp`
- `SupersededByPartId`
- `ReplacesPartId`
- `OemInformation`

Review point: decide whether global curated manufacturer parts are platform-owned while local overrides are organization-owned.

### PartIdentifier

Alternate identifiers for a manufacturer part.

Types:

- Manufacturer part number
- UPC
- Barcode
- Internal SKU
- Supplier SKU
- OEM reference
- Cross reference

### Supplier

Represents an external source of catalog, availability, pricing, and ordering data.

Examples:

- WPS
- Parts Unlimited
- Turn14
- OEM dealer source
- Local/manual vendor

### SupplierListing

Supplier-specific listing for a manufacturer part.

Core fields:

- `SupplierId`
- `ManufacturerPartId`
- `SupplierSku`
- `SupplierCost`
- `SupplierMsrp`
- `Description`
- `ImageUrl`
- `VendorNotes`
- `LastRefreshedAt`

### SupplierAvailabilitySnapshot

Transient availability and warehouse data.

Core fields:

- `SupplierListingId`
- `WarehouseCode`
- `WarehouseName`
- `QuantityAvailable`
- `EstimatedDeliveryDate`
- `FreightEstimate`
- `SnapshotAt`
- `ExpiresAt`

### InventoryItem

Local stock record for one organization/location.

Core fields:

- `OrganizationId`
- `LocationId`
- `ManufacturerPartId`
- `BinLocation`
- `QuantityOnHand`
- `QuantityAllocated`
- `QuantityAvailable`
- `MinimumQuantity`
- `MaximumQuantity`
- `LastCost`
- `AverageCost`
- `LastPurchaseDate`

### InventoryTransaction

Auditable inventory movement.

Types:

- Adjustment
- Receipt
- Allocation
- Deallocation
- Consumption
- Return
- Transfer
- Physical count

### PurchaseOrder

Organization/location purchase document.

Core fields:

- `OrganizationId`
- `LocationId`
- `SupplierId`
- `Status`
- `OrderNumber`
- `ExternalOrderId`
- `CreatedByEmployeeId`
- `SubmittedAt`
- `ExpectedAt`

### PurchaseOrderLine

Supplier purchase line.

Core fields:

- `PurchaseOrderId`
- `ManufacturerPartId`
- `SupplierListingId`
- `WorkOrderId`
- `QuantityOrdered`
- `QuantityReceived`
- `UnitCost`
- `ExpectedAt`

### ReceivingEvent

Records inbound parts receipt.

Core fields:

- `OrganizationId`
- `LocationId`
- `PurchaseOrderId`
- `PurchaseOrderLineId`
- `ManufacturerPartId`
- `QuantityReceived`
- `ReceivedByEmployeeId`
- `ReceivedAt`
- `Condition`
- `Notes`

## Proposed Value Objects And Enums

- PartNumber
- UPC
- Barcode
- Money
- Dimensions
- Quantity
- SupplierConnectorKey
- SupplierCapability
- PurchaseOrderStatus
- ReceivingStatus
- InventoryTransactionType
- PurchaseRecommendationScore
- PurchaseRecommendationReason

## Proposed Application Contracts

- `IPartIdentityService`
- `IPartSearchService`
- `ISupplierListingService`
- `ISupplierAvailabilityService`
- `IInventoryService`
- `IInventoryAllocationService`
- `IPurchaseOrderService`
- `IReceivingService`
- `IBarcodeLookupService`
- `IVendorMappingService`
- `IPurchaseRecommendationService`

## Proposed Supplier Connector Contracts

- `ISupplierCatalogConnector`
- `ISupplierAvailabilityConnector`
- `ISupplierOrderingConnector`
- `ISupplierOrderStatusConnector`
- `ISupplierShipmentConnector`

Connectors must return connector-neutral DTOs. Domain entities must not reference WPS, Square, WooCommerce, WordPress, Textbelt, Parts Unlimited, Turn14, Shopify, Wix, or BigCommerce types.

## Domain Events

Initial events:

- `ManufacturerPartCreated`
- `SupplierListingLinked`
- `SupplierAvailabilityRefreshed`
- `InventoryAdjusted`
- `InventoryAllocated`
- `InventoryConsumed`
- `PurchaseOrderCreated`
- `PurchaseOrderSubmitted`
- `PurchaseOrderLineReceived`
- `PartReceivedForWorkOrder`
- `PurchaseRecommendationGenerated`

## Repository Interfaces

Initial repository contracts:

- `IManufacturerPartRepository`
- `ISupplierRepository`
- `ISupplierListingRepository`
- `IInventoryItemRepository`
- `IInventoryTransactionRepository`
- `IPurchaseOrderRepository`
- `IReceivingRepository`

## Database/Migration Notes

The first migration should create a dedicated parts schema or clearly grouped parts tables.

Every tenant-owned table must include `OrganizationId`.

Operational tables should include `LocationId`.

Indexes should support:

- Manufacturer part number lookup
- UPC lookup
- Barcode lookup
- Supplier SKU lookup
- Organization/location inventory lookup
- Purchase order status lookup
- Receiving by purchase order
- Work order parts fulfillment

## Test Coverage

Unit tests should cover:

- Manufacturer part identity resolution
- Multiple supplier listings mapped to one manufacturer part
- Supplier SKU not treated as canonical identity
- Inventory on-hand, allocated, and available calculations
- Inventory transaction creation on adjustments
- Purchase order line receiving
- Receiving creates inventory transaction
- Supplier connector contracts do not leak supplier-specific types
- Recommendation score includes reason and confidence

## Review Questions

- Should `ManufacturerPart` be global platform data, organization-owned data, or global with organization overrides?
- Should local shop SKU be modeled as `PartIdentifier`, `InventoryItem` field, or both?
- Should barcode be independent from UPC, or should UPC be one barcode type?
- Should inventory allocation to a work order live in Parts Domain or Service Core?
- Should Purchase Intelligence scoring be implemented as deterministic rules first, with AI layered later?
- Should ecommerce inventory availability consume `QuantityAvailable` directly or use a separate publishable availability policy?
