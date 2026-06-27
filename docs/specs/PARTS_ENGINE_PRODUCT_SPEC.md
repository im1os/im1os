# Parts Engine Product Specification

Status: formal product specification.

The Parts Engine is the heart of IM1OS. It is not simply an inventory system. It is the procurement and commerce foundation that every service, repair, estimate, purchase order, receiving, inventory, ecommerce, and supplier workflow will consume.

The long-term objective is to help independent powersports businesses purchase smarter, complete repairs faster, reduce inventory costs, improve technician efficiency, and increase profitability.

The Parts Engine participates in the broader IM1OS Intelligence Layer. Social and Market Intelligence may eventually identify emerging product demand, common failure trends, supplier launches, promotions, and discontinuations, but those signals should feed Parts, Purchase, and Procurement Intelligence through structured events and recommendations rather than hardcoded assumptions.

## Purpose

The Parts Engine helps shops make better parts decisions by balancing:

- Part identity
- Local inventory
- Supplier availability
- Supplier cost
- Freight and handling
- Estimated delivery
- Existing purchase orders
- Work order urgency
- Technician and labor schedule impact
- Vendor reliability
- Dealer preferences

The engine should optimize for lowest total business cost, not simply lowest purchase price.

## Product Position

The Parts Engine is also the Commerce Engine for IM1OS.

Today it powers service repairs. Over time the same engine should support:

- Service estimates
- Work order parts allocation
- Supplier purchasing
- Inventory replenishment
- Receiving
- Ecommerce integrations
- Shopify, WooCommerce, Wix, and BigCommerce integrations
- B2B wholesale
- Supplier connectors
- Intelligence-assisted purchasing and procurement

## Core Design Principles

### Part Identity First

Every physical part must have one canonical identity.

Supplier SKUs are not the identity. Supplier SKUs are mappings to the canonical part.

Preferred identity hierarchy:

1. Manufacturer Part Number
2. UPC
3. Brand
4. Internal IM1OS Part ID
5. Supplier SKUs

One manufacturer part may exist at many suppliers.

Example:

```text
Manufacturer Part Number: 79013001044

Maps to:
  - WPS SKU
  - Parts Unlimited SKU
  - Turn14 SKU
  - OEM dealer SKU
  - Local inventory item
  - Future supplier listings
```

All supplier records point back to one canonical manufacturer part.

### Manufacturer Data Is Permanent

Manufacturer part data forms the durable master record.

Manufacturer Part contains:

- Manufacturer part number
- UPC
- Brand
- Description
- Category
- Images
- Weight
- Dimensions
- Hazmat flag
- MAP
- MSRP
- Superseded by
- Replaces
- Cross references
- OEM information

### Supplier Data Is Independent And Transient

Each supplier may describe, price, stock, and ship the same part differently.

Supplier Listing contains:

- Supplier
- Supplier SKU
- Supplier cost
- Supplier MSRP
- Warehouse inventory
- Warehouse locations
- Availability
- Estimated delivery
- Freight rules
- Vendor notes
- Supplier images
- Supplier description
- Last refreshed timestamp

Supplier records must be allowed to expire, refresh, disappear, or conflict without damaging the manufacturer part master record.

### Local Inventory Is Operational Truth

Local shop inventory must track:

- Quantity on hand
- Quantity allocated
- Quantity available
- Bin location
- Minimum quantity
- Maximum quantity
- Last cost
- Average cost
- Last purchase date
- Inventory transactions

Inventory changes must be represented as transactions. Silent quantity overwrites are not acceptable in the IM1OS domain model.

### Supplier Abstraction Is Mandatory

Business logic must never depend directly on WPS, Parts Unlimited, Turn14, or any individual supplier.

Each supplier implements common connector contracts.

WPS is the first connector. WPS is not the Parts Engine.

## Purchase Intelligence Engine

The Purchase Intelligence Engine recommends the best real-time purchasing decision for a part or group of parts needed for an immediate workflow, usually a work order, estimate, or active replenishment need.

It should answer:

- Which supplier should we buy from?
- Which warehouse should fulfill it?
- Which shipping method should we use?
- Should we combine this with an existing purchase order?
- Should we wait to reach a free freight threshold?
- Is faster delivery worth a higher part or freight cost?
- What is the reason for the recommendation?
- How confident is the recommendation?

### Inputs

Recommendation inputs include:

- Manufacturer part number
- Supplier pricing
- Supplier availability
- Warehouse location
- Freight cost
- Estimated delivery
- Existing purchase orders
- Local shop inventory
- Customer priority
- Work order due date
- Technician schedule
- Shop labor schedule
- Vendor reliability
- Vendor fill rate
- Dealer preferences

### Outputs

Recommendation output includes:

- Recommended supplier
- Recommended warehouse
- Recommended shipping method
- Estimated arrival
- Reason for recommendation
- Confidence score
- Score breakdown

## Decision Matrix

The recommendation score should account for multiple weighted factors.

### Cost

Compare supplier cost and normal purchasing cost.

### Delivery Speed

Prefer faster delivery when it protects promised completion dates, technician utilization, or customer priority.

### Total Landed Cost

Calculate total cost including:

- Part cost
- Freight
- Handling
- Fees
- Discounts
- Free freight effects

### Existing Purchase Orders

Recommend combining purchases when an existing order can reduce freight or operational effort without delaying a repair.

### Free Freight Thresholds

Recommend adding or delaying purchases when reaching a free freight threshold lowers total cost without harming the work order timeline.

### Vendor Reliability

Use historical supplier performance:

- Fill rate
- Backorders
- Returns
- Accuracy
- Delivery performance

### Technician Utilization

Waiting parts can block a lift, stall a technician, and delay revenue.

A part arriving one day sooner may save more labor dollars than it costs in freight.

### Customer Priority

Different priority contexts may change score weighting:

- VIP customer
- Warranty work
- Race support
- Emergency repair
- Rush priority
- Normal priority
- Hold priority

## Time Is Money

The engine should optimize for lowest total cost.

Lowest total cost is not always lowest purchase price.

Example:

Saving twelve dollars on a part that delays a repair by four days is usually a poor business decision.

The Parts Engine must understand operational impact, not just catalog price.

## Future AI Opportunities

Purchase Intelligence and Procurement Intelligence will both support AI-assisted recommendations, but they solve different problems. Purchase Intelligence supports immediate buying decisions. Procurement Intelligence supports strategic stocking decisions.

Examples:

- "Order these bearings from WPS because they ship today."
- "Purchase plastics from Parts Unlimited because they can be combined with tomorrow's shipment."
- "Delay this order until tomorrow to qualify for free freight without affecting the promised completion date."

The AI layer should explain why each recommendation was made. The explanation should be grounded in the score breakdown and source data used by the deterministic engine.

## Procurement Intelligence Engine

Procurement Intelligence is separate from Purchase Intelligence.

Purchase Intelligence is transactional and immediate: where should I buy this part for this work order?

Procurement Intelligence is strategic: what should I stock over the next weeks, months, and seasons?

See `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`.

## Required Domain Capabilities

The Parts Domain must support:

- Canonical manufacturer parts
- Manufacturer part identifiers
- Supplier listings
- Supplier SKU mappings
- Supplier availability snapshots
- Local inventory
- Inventory allocations
- Inventory adjustments
- Inventory transactions
- Purchase orders
- Purchase order lines
- Receiving events
- Barcode support
- SKU search
- UPC search
- Vendor mapping
- Purchase recommendations
- Strategic procurement recommendations
- Supplier connector abstractions

## Required Application Services

The first application layer should expose reusable services for:

- Part identity resolution
- Part search
- Supplier listing management
- Supplier availability lookup
- Local inventory lookup
- Inventory adjustment
- Inventory allocation
- Purchase order creation
- Purchase order submission
- Receiving
- Barcode lookup
- Vendor mapping
- Purchase recommendation scoring

## Required Connector Abstractions

Supplier connectors should support common capabilities where available:

- Catalog search
- Manufacturer part lookup
- Supplier SKU lookup
- UPC lookup
- Availability lookup
- Warehouse availability lookup
- Price lookup
- Purchase order/cart submission
- Order status lookup
- Shipment/tracking lookup
- Receiving status lookup

Connectors may have partial capability. The engine must handle unsupported capabilities explicitly.

## Non-Goals For First Implementation

Do not build UI pages in the first Parts Domain milestone.

Do not hardcode WPS into domain entities or domain services.

Do not build AI behavior before deterministic scoring exists.

Do not make ecommerce platforms the core model. Ecommerce integrations consume the Commerce Engine; they do not define it.

Do not treat supplier SKU as canonical part identity.

## Success Criteria

The first Parts Domain milestone succeeds when:

- A manufacturer part can be represented once and mapped to many supplier listings.
- Supplier listing data can be refreshed independently of manufacturer part data.
- Local inventory can track on-hand, allocated, and available quantities.
- Inventory changes are recorded as transactions.
- Purchase orders and receiving can be represented without depending on WPS.
- Supplier connectors are abstract enough for WPS, Parts Unlimited, Turn14, OEM suppliers, and future providers.
- Purchase recommendation interfaces exist and can explain a score, even if the first scoring implementation is simple.
- Service, intake, technician, estimate, ecommerce, and reporting modules can consume the same Parts Engine contracts.
