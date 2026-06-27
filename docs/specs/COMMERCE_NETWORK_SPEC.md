# IM1OS Commerce Network Specification

Status: formal product specification.

The IM1OS Commerce Network is the future commerce layer for independent powersports businesses.

It is not another generic ecommerce storefront.

It is a local-first commerce network powered by the Parts Engine, dealer inventory, supplier availability, customer portal, merchant services, marketplace search, and network intelligence.

## Purpose

The Commerce Network should help customers find, buy, ship, pick up, or install powersports parts and accessories through participating independent shops.

It should help dealers sell inventory, source parts, capture installation work, and participate in online commerce without each shop rebuilding its own product catalog from scratch.

## Local First

The marketplace should prefer local options when they create customer value.

Example:

```text
Available 6 miles away.
Pickup today.
Support your local dealer.
```

Local-first does not mean local-only. The network should support:

- Local pickup.
- Dealer shipment.
- Supplier drop ship.
- Install appointment.
- Cross-shop inventory sourcing.

## Shared Catalog

The Parts Engine provides the catalog foundation.

Shared catalog capabilities:

- Manufacturer part identity.
- Supplier mappings.
- Images.
- Pricing.
- Availability.
- Fitment.
- Cross references.
- Supersessions.

Dealers should not have to rebuild duplicate product catalogs to participate.

## Unified Inventory Search

A customer searching for a part should eventually see options across:

- Local dealer inventory.
- Nearby participating dealer inventory.
- Supplier inventory.
- Drop-ship availability.
- Install appointment availability.

Example:

```text
Twin Air Filter

Dealer A: In stock, pickup today.
Dealer B: Ships today.
Supplier: Drop ship.
```

## Dealer Collaboration

The Commerce Network should let different dealer models participate:

- Small dealer with limited inventory can sell through drop ship or supplier availability.
- Stocking dealer can gain additional exposure.
- Service-focused shop can convert product demand into install appointments.
- Network inventory can reduce delays and obsolete stock when explicitly shared.

## Customer Portal Integration

Commerce should connect to service.

Examples:

- Customer buys brake pads and chooses install.
- Install appointment is created.
- Vehicle and fitment context are captured.
- Dealer receives parts demand and service opportunity.

## Relationship To Other Engines

The Commerce Network consumes:

- Parts Intelligence for catalog identity and fitment.
- Purchase Intelligence for sourcing decisions.
- Procurement Intelligence for inventory strategy.
- Supplier Promotion Intelligence for supplier offers.
- Network Intelligence for optional shared inventory and network demand.
- Financial Intelligence for payment and profitability insights.
- Network Value Exchange for rewards and incentives.

The Commerce Network produces:

- Online demand signals.
- Dealer exposure opportunities.
- Install appointment opportunities.
- Payment and conversion events.
- Marketplace search events.
- Inventory availability signals.

## Non-Goals For First Implementation

- Do not build a marketplace before the Parts Engine and tenant inventory model are stable.
- Do not make ecommerce platforms the source of truth for part identity.
- Do not expose dealer inventory to the network without explicit participation settings.
- Do not compete with participating dealers by default; the network should route commerce through them where practical.
- Do not treat dealer websites as the core product. The commerce network is the core product.

## Success Criteria

The Commerce Network succeeds when IM1OS can:

- Show customers accurate local, network, supplier, ship, pickup, and install options.
- Let participating dealers sell without rebuilding the catalog.
- Convert product demand into service work.
- Improve inventory turns across the network.
- Preserve dealer control of inventory, pricing, participation, and customer relationship.
- Feed commerce behavior back into the Intelligence Layer.
