# Foundation Architecture Proposal

Status: proposed for review before implementation.

This proposal establishes the first IM1OS foundation build after reviewing the legacy PHP/JavaScript workflows. It does not add user-facing features. It defines the domain and module boundaries future Service & Parts modules will depend on.

## Guiding Rule

Preserve proven workflows from the legacy system unless a specific change is approved.

The foundation should model business behavior first:

- Intake creates a work order quickly.
- Technicians work from assigned work orders.
- Parts search combines local inventory, fitment, SKU/barcode lookup, and supplier availability.
- Estimates, approvals, deposits, repair, receiving, and invoicing are stateful workflows.
- Customer communication is part of the work order lifecycle.

## Proposed Foundation Modules

### Platform

Owns SaaS infrastructure.

Entities:

- Organization
- Location
- LoginAccount
- OrganizationMembership
- Employee
- Role
- Permission
- LocationPermission
- FeatureFlag
- ApplicationSetting
- AuditEvent

Rules:

- `Organization` is the tenant and security boundary.
- Every tenant-owned entity has `OrganizationId`.
- Most operational entities have `LocationId`.
- Employee is the company worker master record. Login accounts may belong to multiple organizations.
- Permissions are organization-specific.
- Location permissions exist inside an organization.

### Service Core

Owns customer service workflow.

Entities:

- Customer
- CustomerVehicle
- VehicleIntake
- WorkOrder
- WorkOrderStage
- WorkOrderPriority
- TechnicianAssignment
- Diagnosis
- Estimate
- EstimateLine
- EstimateApproval
- RepairActivity
- Invoice
- Payment
- CustomerPortalAccount
- PortalNote
- Document
- Photo

Rules:

- Work order stage values should initially mirror the legacy stages.
- Stage changes are audit events.
- Stage changes may produce customer communication events.
- `completed` consumes eligible parts from inventory.
- `ready` can trigger final invoice behavior through an invoice connector.
- Estimate acceptance can move work orders to `scheduled` or `awaiting-deposit`.

### Parts Engine

Owns parts operations, canonical part identity, procurement intelligence, and supplier abstraction.

Entities:

- ManufacturerPart
- PartIdentifier
- Supplier
- SupplierConnector
- SupplierListing
- SupplierAvailability
- SupplierCatalogSearch
- InventoryItem
- InventoryTransaction
- InventoryContainer
- PhysicalCount
- PurchaseOrder
- PurchaseOrderLine
- SupplierOrder
- ReceivingEvent
- Barcode
- VendorMapping
- PurchaseRecommendation
- ProcurementRecommendation

Rules:

- Manufacturer part number is the preferred canonical identity.
- Supplier SKUs are mappings to a manufacturer part, not primary identity.
- The Parts Engine must not depend on WPS.
- WPS is one implementation of a supplier connector.
- Local inventory and supplier availability are separate.
- SKU and barcode are first-class identifiers.
- Supplier part numbers and local SKUs must be mappable.
- Inventory changes must create transactions.
- Receiving must be linkable to purchase order lines and work order parts.
- Parts received events can notify assigned technicians.
- Purchase recommendations should optimize for total business cost, not just lowest purchase price.
- Procurement recommendations should optimize strategic inventory investment across weeks, months, seasons, promotions, turns, and market demand.

See `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md` and `docs/PARTS_DOMAIN_IMPLEMENTATION_PROPOSAL.md`.
See `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md` for strategic inventory planning scope.

### Communication

Owns customer and internal communication records.

Entities:

- CommunicationThread
- CommunicationMessage
- NotificationEvent
- NotificationTemplate

Rules:

- Customer work order updates should be durable records.
- Automatic messages should be skippable when a user changes a stage.
- Media links can be included in customer messages.
- Portal messages belong to the work order thread.

### Connectors

Owns external system boundaries.

Connector families:

- Supplier catalog and ordering connectors
- Payment and invoice connectors
- SMS connectors
- Ecommerce connectors
- Fitment data connectors

Rules:

- Connectors map external data into domain contracts.
- Domain entities must not reference WPS, Square, WooCommerce, Textbelt, or WordPress types.
- Connector payloads and external ids are stored in connector state tables.
- Connector failures should not corrupt core work order state.

## First Domain Implementation Scope

Recommended first implementation is domain-only. No pages.

Create domain entities and value objects for:

- Organization
- Location
- Employee
- Customer
- CustomerVehicle
- WorkOrder
- WorkOrderStage
- WorkOrderPriority
- TechnicianAssignment
- Estimate
- EstimateLine
- ManufacturerPart
- PartIdentifier
- SupplierListing
- InventoryItem
- InventoryTransaction
- PurchaseOrder
- PurchaseOrderLine
- ReceivingEvent
- Document
- AuditEvent

Create enums/value objects for:

- Work order stage
- Work order priority
- Contact preference
- Line item type
- Estimate approval status
- Deposit terms
- Inventory transaction type
- Supplier connector key
- Supplier capability
- Purchase recommendation reason

Do not implement:

- Pages
- Supplier API calls
- Square API calls
- WPS API calls
- Customer portal UI
- Technician UI
- Intake UI

## State Model To Preserve First

Work order stages:

```text
intake
diagnosis
awaiting-approval
awaiting-deposit
declined
parts-ordered
scheduled
in-progress
ready
completed
lost-abandoned
closed
```

Estimate approval statuses:

```text
not_sent
pending
accepted
declined
```

Deposit terms:

```text
none
50_50
25_75
```

Line item types:

```text
labor
parts
diagnostics
fees
other
```

## Key Invariants

- Tenant-owned records require `OrganizationId`.
- Operational records usually require `LocationId`.
- A work order belongs to one organization and usually one location.
- A work order belongs to one customer and may reference one customer vehicle.
- Technician assignment split must total 100 percent when technician assignments exist.
- Labor lines are not taxable by default.
- Tax exempt customers require a tax exempt number.
- Completed work orders fulfill eligible, non-declined parts.
- Inventory quantity changes are represented as transactions, not silent overwrites.
- Supplier availability never replaces local inventory.
- External connector ids are not primary business ids.
- Optimistic concurrency is required on work orders.
- Supplier SKUs are not canonical part identity.
- Purchase recommendations must include reasons and confidence.

## Parts Engine Contract Shape

The first interface layer should be connector-neutral.

Suggested application contracts:

- `ISupplierCatalogConnector`
- `ISupplierAvailabilityConnector`
- `ISupplierOrderingConnector`
- `IPartSearchService`
- `IInventoryService`
- `IPurchaseOrderService`
- `IReceivingService`
- `IBarcodeLookupService`
- `IPurchaseRecommendationService`
- `IProcurementRecommendationService`
- `IVendorMappingService`

Supplier connector capabilities:

- Search catalog by query, SKU, supplier part number, barcode, or fitment context.
- Fetch availability for one or more supplier parts.
- Create supplier cart or supplier order from purchase order lines.
- Fetch supplier order status.
- Fetch receiving/shipping/tracking status where supported.

WPS implementation should be named as a connector, not embedded in domain language.

## Review Questions Before Coding

- Approve preserving the legacy work order stages exactly for the first pass?
- Should `Special Order` remain a kind of work order, or become a purchase-order-first workflow?
- Should PIN login be modeled as an employee authentication method in the foundation?
- Should Customer Portal authentication remain SMS-code-first?
- Should Square invoice/deposit behavior be modeled as generic invoice/payment connector behavior now, with Square later?
- Are inventory containers part of the foundation Parts Engine, or a later physical-count module?
