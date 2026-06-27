# Supplier Promotion Intelligence Engine Specification

Status: formal product specification.

The Supplier Promotion Intelligence Engine is a specialized engine under Procurement Intelligence.

Its purpose is to continuously analyze supplier promotions, rebates, BOGOs, bulk discounts, freight incentives, seasonal programs, clearance opportunities, and purchasing programs, then turn them into actionable purchasing recommendations.

This is not a coupon finder or a promotion list. It is an optimization engine that evaluates supplier offers against inventory levels, historical demand, future demand, open purchase orders, cash flow, shelf space, and profitability.

## Vision

Independent powersports shops cannot realistically track every supplier promotion while also running service, parts, inventory, employees, and customers.

IM1OS should do that work automatically.

The engine should function as a digital purchasing manager by answering:

- Which active promotions matter to this shop?
- Which promotions should be ignored?
- Which planned purchases should be adjusted?
- Which supplier should receive the order?
- Should the shop buy now, wait, combine, increase quantity, or switch suppliers?
- What is the expected savings or business outcome?
- Why is this recommendation worth trusting?

## Problem Statement

Suppliers distribute promotions through many disconnected channels:

- Monthly flyers
- PDF catalogs
- Email campaigns
- Sales representative emails
- Dealer portals
- Printed catalogs
- Supplier API feeds
- Program documents

Most shops never fully use these promotions because they are difficult to organize and harder to evaluate against real demand.

Promotions should become structured data, not static documents.

## Data Sources

The engine should eventually ingest:

- WPS monthly specials
- Parts Unlimited promotions
- Turn14 promotions
- OEM dealer programs
- MAP changes
- Rebate programs
- Seasonal buy programs
- Bulk purchase discounts
- Free freight programs
- Vendor closeouts
- Clearance inventory
- New product launch incentives
- Supplier representative offers

Supplier connectors should expose promotion ingestion as a capability when available. Manual entry or document-assisted import may be needed before supplier APIs are mature.

## Promotion Types

Supported promotion models should include:

- Buy one get one
- Buy X get Y
- Percentage discount
- Fixed dollar discount
- Free freight
- Brand incentive
- Volume discount
- Seasonal buy program
- Dealer rebate
- Vendor credit
- Early buy program
- Package deal
- Tiered discount
- Closeout or clearance offer
- MAP change

## Decision Factors

Promotion recommendations should consider:

- Historical sales
- Current inventory
- Quantity available
- Quantity allocated
- Reorder points
- Forecast demand
- Seasonal demand
- Upcoming race events
- Social and market intelligence signals
- Existing purchase orders
- Current supplier promotions
- Vendor lead times
- Vendor fill rates
- Cash flow
- Inventory turns
- Shelf space
- Product profitability
- Freight rules
- Supplier reliability
- Product lifecycle and discontinuation risk

## Recommendation Types

The engine should generate recommendations, not merely show active promotions.

Recommendation actions may include:

- Buy now.
- Wait until next week.
- Increase quantity.
- Decrease quantity.
- Combine with another order.
- Switch suppliers.
- Take advantage of rebate.
- Delay purchase until promotion begins.
- Buy before promotion expires.
- Replace planned purchase with promotional equivalent.
- Ignore the promotion because demand, margin, or cash flow does not justify it.

Every recommendation must include an explanation.

## Examples

### Buy X Get Y

Promotion:

```text
WPS: Buy 10 Shinko tires, receive 2 free.
```

IM1OS knows:

- Current tire inventory
- Historical Shinko tire sales
- Upcoming race season
- Existing purchase orders
- Expected 60-day demand

Recommendation:

```text
You are expected to sell 14 rear Shinko tires in the next 60 days. Purchasing 12 today qualifies for the Buy 10 Get 2 promotion and reduces effective unit cost by 16.7 percent.
```

### Threshold Discount

Promotion:

```text
Parts Unlimited: Spend 2000 on Alpinestars and receive an additional 5 percent discount.
```

Current cart:

```text
1865
```

Recommendation:

```text
Adding 135 in Alpinestars inventory unlocks an additional 5 percent discount across the order, resulting in estimated net savings of 100.
```

### Free Freight

Promotion:

```text
Turn14: Free freight over 1500.
```

Recommendation:

```text
Delay today's 1320 order until tomorrow and combine it with scheduled purchases to eliminate freight charges without affecting promised repair dates.
```

## Relationship To Procurement Intelligence

Supplier Promotion Intelligence is a specialized optimization capability inside Procurement Intelligence.

Procurement Intelligence answers:

```text
What should I stock over the next weeks, months, and seasons?
```

Supplier Promotion Intelligence answers:

```text
How should active supplier programs change the timing, supplier, quantity, or structure of planned purchases?
```

It consumes:

- Procurement demand forecasts.
- Parts Intelligence for canonical part identity.
- Purchase Intelligence for supplier, warehouse, freight, and real-time buying options.
- Social and Market Intelligence for emerging demand signals.
- Data Intelligence for historical sales, inventory turns, and benchmark-ready events.
- Supplier connectors for promotion, rebate, freight, and program data.

It produces:

- Promotion-aware purchasing recommendations.
- Cart adjustment recommendations.
- Promotion qualification opportunities.
- Promotion expiration warnings.
- Supplier switching recommendations.
- Promotion ignore recommendations.

## Recommendation Architecture Pattern

IM1OS is not only a system of record. It is becoming a system of recommendations.

Every Intelligence Layer engine should follow the same pattern:

1. Gather data from multiple sources.
2. Normalize and correlate the data.
3. Apply business rules, analytics, and optimization logic.
4. Produce a recommendation.
5. Explain why the recommendation was made.
6. Preserve the supporting evidence for audit, learning, and refinement.

This pattern applies to estimating labor, choosing a supplier, stocking inventory, using promotions, identifying market trends, and improving shop operations.

## Non-Goals For First Implementation

- Do not build promotion scraping before supplier connector boundaries exist.
- Do not auto-place strategic purchases without human approval.
- Do not treat promotions as good by default.
- Do not recommend purchases that ignore inventory turns, cash flow, shelf space, or demand.
- Do not hardcode supplier-specific promotion rules into the domain model.
- Do not build dashboards before structured promotion data and recommendation events exist.

## Success Criteria

The engine succeeds when IM1OS can explain:

- Which promotion is relevant.
- Which products, brands, suppliers, or purchase orders are affected.
- What action the shop should consider.
- How much savings or margin improvement is expected.
- What operational risk exists.
- How confident the recommendation is.
- Which data supports the recommendation.

The long-term goal is a digital purchasing manager for supplier programs and promotion optimization.
