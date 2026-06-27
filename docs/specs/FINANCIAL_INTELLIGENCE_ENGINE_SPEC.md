# Financial Intelligence Engine Specification

Status: formal product specification.

The Financial Intelligence Engine turns estimate, approval, invoice, payment, settlement, profitability, and benchmark data into business recommendations for independent powersports shops.

It is not simply payment processing.

It is the intelligence layer around money movement and financial outcomes.

## Purpose

The engine should help shops understand:

- How quickly estimates become approvals.
- How quickly invoices become payments.
- Which payment methods improve collection speed.
- Which repair categories drive profitability.
- Which customers may need financing options.
- Which service workflows create payment delays.
- How the shop compares to tenant-safe benchmarks.

## Merchant Services Relationship

iM Merchant Services is a future commerce capability.

Merchant Services may process or orchestrate:

- Estimate deposits.
- Invoice payments.
- Card-present payments.
- Card-not-present payments.
- Financing handoff.
- Settlement reporting.
- Refunds and adjustments.

The Financial Intelligence Engine consumes permitted payment and settlement data to generate insights. Merchant Services moves money; Financial Intelligence explains financial behavior and recommends better decisions.

## Example Insights

```text
Shops similar to yours collect payment 2.3 days faster when estimates are approved digitally.
```

```text
Customers spending over 750 commonly choose financing when it is offered before estimate approval.
```

```text
Diagnostic work has higher margin but lower approval speed than maintenance packages.
```

## Data Sources

Potential inputs:

- Estimates
- Estimate approvals
- Deposits
- Invoices
- Payments
- Refunds
- Settlement batches
- Payment method
- Financing handoff
- Labor margin
- Parts margin
- Fees
- Taxes
- Discounts
- Customer payment timing
- Tenant-safe benchmarks

## Relationship To Other Engines

Financial Intelligence consumes:

- Data Intelligence for events, privacy, aggregation, and benchmarking.
- Shop Intelligence for operational performance context.
- Service Intelligence for repair category and labor context.
- Parts Intelligence for parts margin context.
- Network Intelligence for tenant-safe benchmark context.

Financial Intelligence produces:

- Collection speed insights.
- Profitability insights.
- Payment method recommendations.
- Financing opportunity signals.
- Deposit and approval workflow recommendations.
- Benchmark-safe financial comparisons.

## Non-Goals For First Implementation

- Do not build merchant services before payment, compliance, accounting, and security boundaries are designed.
- Do not store card data unless the platform has the required compliance posture.
- Do not expose tenant financial data to other tenants.
- Do not treat payment processing fees as the primary strategic reason for Merchant Services.
- Do not make financial recommendations without explaining the supporting data.

## Success Criteria

The engine succeeds when IM1OS can explain:

- What financial behavior changed.
- Which workflow, customer segment, repair category, or payment method is affected.
- What action the shop should consider.
- What financial outcome is expected.
- How confident the recommendation is.
- Which permitted data supports the recommendation.
