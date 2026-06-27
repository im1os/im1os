# Domain Model

IM1OS is modeled around service and parts operations first, then grows into an operating system and commerce network for independent powersports businesses. The domain should support the core workflow before introducing commerce and network modules.

The legacy PHP/JavaScript implementation is the functional specification for workflow behavior. See `docs/LEGACY_FUNCTIONAL_SPEC.md` and `docs/FOUNDATION_ARCHITECTURE_PROPOSAL.md` before implementing domain entities.

The Parts Engine is governed by `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md`. Parts domain implementation should also follow `docs/PARTS_DOMAIN_IMPLEMENTATION_PROPOSAL.md` until that proposal is accepted or superseded.

Service Intelligence is governed by `docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md`. Data Intelligence and analytics readiness are governed by `docs/specs/DATA_INTELLIGENCE_SCOPE.md`.

Procurement Intelligence is governed by `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`. Supplier Promotion Intelligence is governed by `docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md`. Network Intelligence is governed by `docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md`. Network Value Exchange is governed by `docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md`. Financial Intelligence is governed by `docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md`. Commerce Network is governed by `docs/specs/COMMERCE_NETWORK_SPEC.md`. Social and Market Intelligence is governed by `docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md`. Purchase Intelligence is transactional and per-work-order; Procurement Intelligence is strategic and inventory-planning oriented; Supplier Promotion Intelligence optimizes supplier programs and promotion-aware purchasing; Network Intelligence creates tenant-safe aggregate and explicitly shared opportunities across participating shops; Network Value Exchange rewards trusted participation with non-cash utility credits; Financial Intelligence explains payment, settlement, profitability, and benchmark-safe financial behavior; Commerce Network connects local-first dealer commerce, supplier availability, marketplace search, merchant services, and customer portal flows; Social and Market Intelligence is market-signal and trend oriented.

```text
Customer
  -> Vehicle Intake
  -> Work Order
  -> Diagnosis
  -> Estimate
  -> Parts Search
  -> Supplier Availability
  -> Order Parts
  -> Receive Parts
  -> Repair
  -> Invoice
  -> Customer Portal
```

## Tenancy

The tenant is `Organization`. `Organization` is the security boundary.

An organization contains:

- Locations
- Employees
- Customers
- Vehicles
- Work orders
- Parts inventory
- Purchase orders
- Invoices
- Reports

Users may belong to multiple organizations. Permissions are organization-specific. Location permissions exist inside an organization.

Every tenant-owned table must contain `OrganizationId`. Most operational tables should also contain `LocationId`.

## Core Domain Areas

### Organization

Represents a subscribed business tenant. It owns operational data and controls access boundaries.

Related concepts:

- Location
- Employee
- User membership
- Organization role
- Organization permission
- Location permission
- Organization settings

### Customer

Represents the person or business receiving service. Customers belong to an organization and may be associated with one or more vehicles.

Related concepts:

- Contact information
- Communication preferences
- Portal access
- Service history
- Invoice history

### Vehicle

Represents a customer-owned powersports vehicle being serviced. Vehicles are not unit inventory by default.

Related concepts:

- VIN or serial number
- Year, make, model, trim
- Mileage or hours
- Customer ownership
- Service history

### Vehicle Intake

Captures the start of a service visit.

Related concepts:

- Customer concern
- Vehicle condition
- Intake notes
- Photos or attachments
- Requested services
- Assigned location

### Work Order

Represents the active service record for a vehicle. Work orders connect customer concerns, diagnosis, estimates, parts, labor, repair progress, and invoicing.

Related concepts:

- Work order status
- Service lines
- Labor operations
- Technician assignment
- Parts requirements
- Customer approvals
- Audit history

### Diagnosis

Represents findings from inspection or troubleshooting.

Related concepts:

- Technician notes
- Recommended repairs
- Required parts
- Labor estimates
- Customer-facing explanation

### Estimate

Represents proposed work before customer approval.

Related concepts:

- Labor lines
- Parts lines
- Fees
- Taxes
- Discounts
- Approval status
- Expiration

### Labor Operation

Represents structured service work that can be estimated, assigned, completed, analyzed, and improved over time.

Related concepts:

- Labor code
- Service category
- Vehicle applicability
- Base labor hours
- Flat rate hours
- Shop-adjusted hours
- Skill level
- Difficulty rating
- Required parts
- Required tools
- Technical specifications
- Actual technician time
- Approval and decline outcomes

### Service Intelligence

Represents the Digital Service Advisor capability.

Related concepts:

- Customer complaint interpretation
- Suggested labor operations
- Required parts and consumables
- Technical specifications
- Inspection recommendations
- Estimate recommendation
- Pricing recommendation
- Historical repair learning

### Parts Operations

Parts operations are foundational to IM1OS.

The canonical part identity is the manufacturer part, not a supplier SKU. Supplier listings, supplier SKUs, local inventory, work order parts, purchase orders, receiving, and ecommerce availability should map back to a manufacturer part whenever possible.

Related concepts:

- Manufacturer part
- Part identifier
- Supplier catalog result
- Supplier listing
- Supplier availability
- Inventory item
- Inventory transaction
- Bin/location
- Reorder point
- Purchase order
- Receiving
- Parts allocation to work orders
- Returns or cores
- Purchase recommendation
- Procurement recommendation

### Supplier Integration

Represents external supplier catalog, pricing, availability, and ordering workflows.

Related concepts:

- Supplier
- Supplier connector
- Catalog search
- Availability response
- Price
- Warehouse availability
- Estimated delivery
- Substitute or superseded part
- Purchase order submission
- Order status
- Shipment/tracking status

### Procurement Intelligence

Represents strategic inventory purchasing guidance.

Related concepts:

- Inventory turn analysis
- Seasonal demand
- Supplier promotions
- Strategic stocking recommendation
- Supplier promotion
- Promotion rule
- Promotion eligibility
- Promotion recommendation
- Expected savings
- Recommended quantity
- Recommended purchase date
- Expected demand
- Expected margin
- Stock-out risk
- Slow-moving inventory
- Discontinuation recommendation
- Benchmark comparison
- Recommendation confidence

### Social And Market Intelligence

Represents early demand and market-signal intelligence.

Related concepts:

- Market signal
- Trend detection
- Product mention
- Brand mention
- Vehicle popularity signal
- Racing event signal
- Industry news signal
- Supplier launch or promotion signal
- Supplier discontinuation signal
- Common failure signal
- Market recommendation
- Recommendation confidence
- Supporting evidence

### Network Intelligence

Represents voluntary, tenant-safe network-level intelligence.

Related concepts:

- Network participation setting
- Data sharing consent
- Aggregation cohort
- Minimum sample size rule
- Network demand forecast
- Regional inventory signal
- Network purchasing opportunity
- Supplier negotiation signal
- Shared inventory listing
- Inventory exchange opportunity
- Network recommendation
- Privacy evidence

### Network Value Exchange

Represents participation rewards and utility credits inside the IM1OS operating network.

Related concepts:

- Participant
- Contribution event
- Contribution rule
- iM Points ledger
- Utility credit
- Redemption option
- Supplier-sponsored campaign
- Knowledge contribution
- Technical contribution verification
- Data contribution consent
- Inventory contribution reward
- Fraud review
- Reward audit event

### Financial Intelligence

Represents payment, settlement, profitability, and financial recommendation capabilities.

Related concepts:

- Estimate approval timing
- Deposit
- Invoice payment
- Payment method
- Settlement batch
- Refund
- Financing handoff
- Collection speed
- Labor margin
- Parts margin
- Profitability signal
- Financial benchmark
- Payment recommendation

### Commerce Network

Represents local-first commerce across participating dealers, customers, suppliers, and the customer portal.

Related concepts:

- Marketplace search
- Dealer storefront
- Shared catalog listing
- Local pickup option
- Dealer shipment option
- Supplier drop-ship option
- Install appointment option
- Commerce availability result
- Commerce order
- Merchant services transaction
- Dealer participation setting

### Repair

Represents approved service execution.

Related concepts:

- Technician workflow
- Labor tracking
- Parts consumption
- Quality check
- Completion notes
- Customer notification

### Invoice

Represents the billable result of completed work.

Related concepts:

- Invoice lines
- Labor charges
- Parts charges
- Taxes
- Payments
- Balance
- Customer receipt

### Customer Portal

Provides the customer-facing experience.

Related concepts:

- Estimate review and approval
- Repair status
- Messages
- Invoice review
- Payment handoff
- Service history

### Domain Event

Represents an immutable record of an important business action.

Related concepts:

- Organization
- Location
- Entity type
- Entity id
- Event type
- Actor user
- Timestamp
- Structured payload
- Correlation id
- Source module

Domain events power timeline history, audit logs, reporting, benchmarking, future data warehouse exports, and AI recommendations.

## Explicit Non-Core Areas

The following are not foundation domain areas:

- Vehicle sales
- Unit inventory
- F&I
- Deal jackets
- Floor planning

These may become optional modules later, but they must not drive the core domain model.
