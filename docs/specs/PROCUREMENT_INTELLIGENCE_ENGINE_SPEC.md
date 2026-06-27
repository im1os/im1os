# Procurement Intelligence Engine Specification

Status: formal product specification.

Procurement Intelligence is a strategic Business Engine for IM1OS. It is different from Purchase Intelligence.

Purchase Intelligence answers a real-time work order question:

```text
I need this part. Where should I buy it?
```

Procurement Intelligence answers a strategic business question:

```text
What should I stock over the next weeks, months, and seasons?
```

Supplier Promotion Intelligence is a specialized engine inside Procurement Intelligence. It answers:

```text
How should active supplier programs change the timing, supplier, quantity, or structure of planned purchases?
```

See `docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md`.

Network Intelligence is a related Business Intelligence Engine that can provide tenant-safe aggregated demand, supplier negotiation, regional inventory, and optional inventory exchange signals.

See `docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md`.

## Purpose

The Procurement Intelligence Engine helps shop owners make strategic inventory purchasing decisions.

Unlike the Purchase Intelligence Engine, which optimizes purchasing for an individual work order, Procurement Intelligence optimizes inventory investment across weeks, months, seasons, brands, categories, suppliers, and market conditions.

Its goal is to ensure the right products are in stock at the right time while minimizing excess inventory and maximizing profitability.

## Business Questions

Procurement Intelligence should answer:

- What products should I stock?
- How many should I stock?
- When should I buy them?
- Which supplier offers the best seasonal value?
- Which supplier promotion changes the best purchasing decision?
- Which products are trending upward?
- Which products are becoming obsolete?
- Which supplier promotions should I take advantage of?
- Which inventory is tying up cash?
- Which inventory turns the fastest?
- Which products are frequently special ordered but should be stocked?
- Which warehouse trends matter?
- Which brands are increasing?
- What seasonal demand is coming?
- Which products should I discontinue?

## Data Sources

The engine should analyze:

- Historical shop sales
- Work order history
- Parts usage
- Special order history
- Parts quoted and declined
- Supplier pricing history
- Supplier promotions
- Supplier rebates, BOGOs, bulk discounts, freight incentives, and seasonal programs
- Inventory turns
- Seasonal demand
- Vehicle population trends
- Racing schedules
- Regional trends
- Social and market intelligence signals
- Weather patterns, future
- Anonymous platform benchmarks
- Tenant-safe network demand signals
- Optional network inventory exchange signals
- Vendor fill rates
- Supplier lead times
- Backorder history

## Recommendations

Examples:

- Increase motocross tire inventory before racing season.
- Purchase batteries before winter demand increases.
- Reduce slow-moving apparel sizes.
- Stock more medium black helmets based on historical demand.
- Take advantage of current supplier promotions before expiration.
- Add enough qualifying inventory to unlock a supplier discount when demand justifies it.
- Add frequently special-ordered parts to regular inventory.
- Increase oil filter inventory before spring riding season.
- Discontinue products with poor turns and low margin.
- Buy before expected price increases when inventory carrying cost is justified.

## Predictive Inventory

Traditional inventory systems use fixed minimum and maximum quantities.

Procurement Intelligence should evolve toward recommendation-based inventory:

```text
Buy: 12
Reason: Spring inventory increase
Expected sales: 11
Confidence: 92%
```

The recommendation should include:

- Recommended quantity
- Recommended purchase date
- Expected demand
- Expected gross margin
- Expected stock-out reduction
- Expected inventory carrying cost
- Confidence score
- Explanation

## Supplier Promotions

Supplier promotions should be evaluated against expected demand, inventory carrying cost, and cash flow.

Example:

```text
Supplier promotion: Tires 10% off, ends Friday
Expected demand: 87 rear tires over 60 days
Recommendation: Purchase 90 now
Expected savings: 1480
```

## Market Pricing

The engine should track historical supplier cost trends.

Example:

```text
DID Chains
2022 average cost: 82
2023 average cost: 89
2024 average cost: 96
```

Recommendation:

```text
Supplier promotions below 90 represent buying opportunities.
```

## Benchmarking

Procurement Intelligence may use anonymized platform data when privacy rules allow.

Example:

```text
Shops in Texas average 48 rear tires in stock.
You stock 17.
Recommendation: You are likely understocked.
```

Benchmarks must follow tenant privacy, sample-size, and anonymization rules in `docs/specs/DATA_INTELLIGENCE_SCOPE.md`.

## External Signals

Future signals may include:

- Weather patterns
- Racing calendars
- Regional events
- Local motocross, enduro, and off-road schedules
- OEM release cycles
- Supplier availability changes
- Supplier promotional calendars

Example:

```text
Red River race weekend expected demand:
- Rear tires
- Oil filters
- Brake pads
- Air filters
```

## Relationship To Other Business Engines

Procurement Intelligence consumes:

- Parts Intelligence for canonical part identity.
- Purchase Intelligence for real-time supplier purchasing options.
- Data Intelligence for history, aggregation, and benchmarks.
- Shop Intelligence for business performance context.
- Social and Market Intelligence for emerging demand, product trend, supplier promotion, discontinuation, and common failure signals.
- Supplier Promotion Intelligence for structured promotion opportunities and promotion-aware purchase adjustments.
- Network Intelligence for tenant-safe demand, regional inventory, supplier negotiation, and optional inventory exchange signals.

Procurement Intelligence produces:

- Inventory stocking recommendations.
- Replenishment recommendations.
- Promotion-buy recommendations.
- Promotion qualification recommendations.
- Discontinuation recommendations.
- Seasonal purchasing plans.

## Non-Goals For First Implementation

- Do not automate strategic purchasing without human approval.
- Do not expose one tenant's raw inventory or sales data to another tenant.
- Do not hardcode racing/weather/seasonality assumptions into transactional purchasing.
- Do not replace min/max inventory immediately; first make the data model recommendation-ready.
- Do not build dashboards before structured events and inventory data exist.

## Success Criteria

Procurement Intelligence succeeds when IM1OS can explain:

- What should be stocked.
- Why it should be stocked.
- When to buy it.
- How many to buy.
- What business outcome is expected.
- How confident the platform is.

The long-term goal is a digital inventory manager for independent powersports businesses.
