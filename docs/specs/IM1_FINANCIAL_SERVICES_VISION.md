# iM1 Financial Services Vision

Status: product vision and architecture direction.

iM1 Financial Services is not a payment gateway. It is the financial operating layer of iM1.

The objective is to let a business manage every financial interaction from one integrated platform: payments, invoices, subscriptions, financing, merchant services, deposits, banking, settlement, and reconciliation.

The merchant should not need to understand processors, gateway accounts, tokenization, PCI scope, or payment terminal provisioning. They enable iM1 Payments and begin accepting payments.

## Guiding Principles

- Financial services are optional.
- Financial services are integrated throughout iM1.
- Users should not leave iM1 to manage payments.
- Payment providers are infrastructure, not user-facing products.
- Hardware should be interchangeable.
- Every financial transaction should originate from an existing iM1 business process.
- iM1 stores gateway tokens and financial records, never raw card data.

## Architecture

```text
iM1 OS
  |
  v
Financial Services Module
  |
  +-- Payment Engine
  |     |
  |     +-- Gateway Drivers
  |           |
  |           +-- NMI
  |           +-- Authorize.net
  |           +-- Stripe
  |           +-- Future Providers
  |
  +-- Banking Engine
  |     |
  |     +-- ACH Providers
  |
  +-- Billing Engine
        |
        +-- Subscription Engine
```

Every payment request in iM1 flows through the Financial Services Engine. Product modules should not communicate directly with a payment gateway.

## Payment Abstraction Layer

No user-facing module should know which gateway is being used. Modules call the iM1 payment service with business context such as invoice, work order, customer, amount, tender type, and desired capture behavior.

The Payment Engine determines:

- Gateway driver.
- Merchant account.
- Terminal or device.
- Card-present or card-not-present flow.
- Token or stored wallet method.
- ACH flow.
- Retry and error handling.
- Receipt generation.
- Transaction recording.

This keeps iM1 gateway-independent and allows hardware/provider changes without rewriting service, invoicing, ecommerce, or customer portal workflows.

## Merchant Onboarding

Every iM1 company can enable Financial Services from company settings.

The onboarding wizard collects:

- Business information.
- Owner information.
- EIN or tax identity.
- Bank account details.
- Identity verification.
- Expected processing volume.
- Required merchant documents.

Once approved, iM1 provisions the merchant account, generates gateway credentials, assigns terminals when applicable, and marks the company ready to accept payments.

The merchant-facing status should be simple:

```text
Status: Active
```

## Product Surfaces

Financial Services has two distinct surfaces.

### Platform: iM1 Internal

Platform Financial Services is where iM1 manages the merchant services business.

Scope:

- Merchant applications.
- Active merchants.
- Underwriting queue.
- Risk monitoring.
- Processor management.
- Gateway providers.
- Residual reporting.
- Settlement monitoring.
- Chargeback management.
- Hardware catalog.
- Device inventory.
- Shipping and fulfillment.
- Pricing plans.
- Merchant support.
- Provider configuration.

Tenant users never see this workspace.

### Company: Payments & Finance

Company Payments & Finance is where each tenant sees its own financial operations.

Scope:

- Dashboard.
- Merchant account.
- Transactions.
- Payments.
- Customer wallet.
- Payment links.
- Terminals.
- ACH.
- Subscriptions.
- Financial ledger.
- Deposits.
- Statements.
- Reports.
- Settings.

POS payments, work order deposits, invoice payments, ecommerce checkout, and customer portal payments stay in their owning business modules. Those modules call the shared payment service; Payments & Finance records the financial result.

## Company Payments & Finance Modules

### Merchant Account

Manages the company's own payment processing account: status, processing profile, limits, banking information, underwriting status, settlement schedule, and merchant documents.

### Terminal Management

Manages payment hardware: register terminal, activate, deactivate, assign employee/register, firmware status, health monitoring, and remote configuration.

Supported hardware should include smart POS devices, countertop terminals, customer PIN pads, mobile readers, and Tap to Pay devices.

### Transaction Center

Provides complete transaction history: sales, refunds, voids, partial refunds, offline transactions, declines, chargebacks, disputes, and receipts.

Search dimensions should include customer, work order, invoice, employee, terminal, date, and card last four.

### Customer Wallet

Every customer can have a secure wallet for saved cards, ACH accounts, preferred payment method, tokenized methods, and automatic billing authorization.

Cards are never stored inside iM1. Only gateway-issued or vault-issued tokens are stored.

### Subscription Billing

Supports memberships, service plans, storage fees, maintenance programs, software subscriptions, and recurring billing.

### ACH Processing

Supports customer ACH, vendor ACH, bank verification, recurring ACH, and ACH refunds.

### Payment Links

Generates secure links for estimates, deposits, invoices, event registrations, and parts orders.

### Virtual Terminal

Accepts remote or card-not-present payments inside iM1 for phone orders, mail orders, customer service, and remote invoice/deposit collection.

### Merchant Dashboard

Shows today's sales, pending settlements, batch status, declines, chargebacks, average ticket, monthly volume, fees, and net deposits.

### Financial Ledger

Records immutable operational financial entries for every meaningful money movement or attempted movement.

Examples include payment authorized, payment captured, refund issued, chargeback created, ACH received, ACH sent, store credit created, gift card redeemed, financing disbursed, deposit settled, and subscription renewed.

Merchant dashboard, reporting, commissions, accounting sync, QuickBooks integration, analytics, reconciliation, and future financial intelligence should read from the ledger rather than querying provider responses directly.

### Hardware Store

Lets businesses purchase certified hardware directly through iM1. Devices should ship preconfigured when possible.

## Future Modules

- Financing.
- Buy Now Pay Later.
- Floorplan financing.
- Business lending.
- Insurance payments.
- Warranty claims.
- Supplier payments.
- Payroll.
- Accounting.

## Revenue Model

Financial Services can become its own iM1 business unit.

Revenue sources include merchant processing residuals, gateway services, hardware sales, ACH processing, subscription billing, financing commissions, payment links, premium Financial Services modules, and value-added services.

The model grows with customer transaction volume, aligning iM1 success with customer success.

## Long-Term Objective

Every dollar entering or leaving a customer's business should be initiated, processed, recorded, reconciled, and reported inside iM1.

That includes invoice payments, parts purchases, repair financing, payroll, supplier payments, deposits, ecommerce, in-store sales, and mobile/event sales.

Financial Services is not just another module. It is the financial backbone that connects every module of iM1 into one business operating system.

## First Implementation Direction

The first implementation is iM1 Payments backed by an NMI gateway driver.

The product boundary is:

- Company users see iM1 Payments and iM1 Financial Services.
- Platform/admin tooling may see provider health and gateway configuration.
- Application modules call iM1 payment abstractions, not provider SDKs or endpoints.
- The NMI driver remains infrastructure.
- Card entry uses hosted/tokenized fields.
- iM1 persists transaction records, gateway identifiers, status, authorization metadata, and safe card descriptors only.
