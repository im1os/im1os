# IM1OS Discovery Blueprint

Status: living discovery document.

This is not Architecture 1.0.

This document captures the current product vision, known decisions, open questions, working assumptions, and discovery tasks so IM1OS can keep moving without pretending the long-term architecture is fully answered.

## 1. Confirmed Product Direction

IM1OS is not a traditional Dealer Management System.

IM1OS is a Service, Parts, Intelligence, and Commerce Operating System for independent powersports businesses.

The broader product scope is split into three products:

- IM1 Platform: the SaaS control plane for IM1's business.
- IM1OS: the tenant operating system for each shop's business.
- IM1 Network: the ecosystem layer connecting shops, suppliers, commerce, rewards, and shared intelligence.

The foundation is:

- Service operations.
- Parts operations.
- Supplier integrations.
- Estimating.
- Purchasing intelligence.
- Customer experience.

The first build priority remains service and parts operations. The long-term opportunity is a broader operating network, but that network must be built on reliable operational data and real shop workflows.

Platform Administration exists above the tenant. It is not the same thing as tenant Business Administration.

## 2. Confirmed Core Workflow

```text
Customer
  -> Vehicle Intake
  -> Work Order
  -> Service Intelligence
  -> Estimate
  -> Parts Intelligence
  -> Purchase Intelligence
  -> Approval
  -> Ordering
  -> Repair
  -> Invoice
  -> Payment
  -> Customer Portal
```

This workflow is the current product spine. Future engines and commerce capabilities should support this path, not distract from it.

## 3. Confirmed Business Engines

Current confirmed engines:

- Work Order Engine
- Parts Intelligence Engine
- Service Intelligence Engine
- Purchase Intelligence Engine
- Procurement Intelligence Engine
- Supplier Promotion Intelligence Engine
- Social and Market Intelligence Engine
- Network Intelligence Engine
- Financial / Merchant Services Engine
- Commerce / Online Store Engine
- Rewards / Contingency Engine

Confirmed platform control-plane capabilities:

- Tenant Manager
- Provisioning
- Billing and Licensing
- Feature Management
- Deployment Management
- Tenant Health
- Support and Audited Impersonation
- AI Usage Management
- Platform Marketplace Administration
- Platform Analytics

Working clarification:

- Some of these engines are immediate foundation concerns.
- Some are long-term platform capabilities.
- Not every engine needs its own first-pass implementation now.
- Engines are reusable decision or workflow capabilities, not necessarily UI modules.

## 4. Known Architecture Decisions

Confirmed architecture decisions:

- Tenant is `Organization`.
- `Organization` can have multiple `Locations`.
- Users can belong to multiple `Organizations`.
- Permissions are organization-specific.
- Location permissions exist inside an organization.
- Tenant-owned data must include `OrganizationId`.
- Operational records usually include `LocationId`.
- Start with shared database/shared schema.
- Domain events are required.
- Data must be structured for future analytics and AI.
- The legacy PHP/JavaScript implementation is a functional reference for proven workflow behavior.
- Supplier integrations must be abstracted. WPS can be first, but WPS must not define the Parts Engine.
- Canonical part identity should be independent of supplier SKU where possible.
- AI consumes structured data, events, permissions, and engine outputs. AI does not own the domain model.
- IM1 Platform is above `Organization`; it manages tenants and platform lifecycle.
- Tenant Business Administration lives inside each `Organization`.
- Platform Administration and tenant administration must not be conflated.

## 5. Open Questions

### Product Questions

- Should IM1 Platform be built before additional tenant modules, or should the current Parts Engine milestone continue while Platform Administration is discovered?
- Should IM1 Platform be a separate web app, separate API area, or module in the same deployment?
- What is the minimum viable Tenant Manager for the first paid tenant?
- Which workflow should become the first production-quality module after foundation: Intake, Work Orders, Parts, or Technician Workspace?
- Which existing legacy workflows are mandatory for day-one parity?
- Which legacy workflows can be simplified without losing proven business behavior?
- What is the minimum viable Customer Portal for the first release?
- How much of the Technician Workspace should be mobile-first from the start?
- Should estimates be service-line driven, labor-operation driven, or both?
- How should job stages be standardized across shops while still allowing shop-specific configuration?

### Business Questions

- What tenant lifecycle states are required: trial, active, suspended, canceled, archived?
- Which platform metrics are required before launch: MRR, ARR, churn, retention, usage, support load, or module adoption?
- Which plan and module licensing model should the platform support first?
- What is the first ideal customer profile: single-location independent repair shop, parts-heavy shop, multi-location shop, or ecommerce-enabled shop?
- What is the pricing model for core operations?
- Which capabilities belong in base subscription vs premium modules?
- Which intelligence engines create enough value to justify early monetization?
- What business metrics define success for the first release?

### Legal And Compliance Questions

- What legal and operational rules govern platform support impersonation?
- What audit visibility should tenants have into platform support access?
- What data-sharing consent model is required for benchmarking, Network Intelligence, and Network Value Exchange?
- What minimum aggregation thresholds are needed before showing benchmark or network insights?
- What legal review is required before iM Points or utility credits are introduced?
- Could any reward, rebate, or credit model be interpreted as stored value, gift card, banking, money transmission, or taxable compensation?
- What terms are required for supplier-sponsored campaigns?
- What customer reward rules are needed so shops retain customer relationship control?

### Supplier Questions

- Which supplier should be integrated first and why?
- What supplier APIs support catalog search, availability, pricing, ordering, promotions, and order status?
- Which supplier data can be cached, stored, transformed, or displayed under contract?
- How should supplier promotions be ingested before APIs are mature?
- How should conflicting supplier part descriptions, images, pricing, and fitment data be reconciled?
- What supplier terms affect marketplace, drop-ship, or dealer-visible inventory features?

### Payment And Merchant Services Questions

- Which billing provider should be used for recurring platform billing?
- Which payment provider or merchant-services model is realistic for the first commerce phase?
- What compliance obligations apply if IM1OS stores payment metadata but not card data?
- What payment events are safe and useful to capture now?
- How should deposits, partial payments, refunds, financing handoff, and settlement batches be modeled?
- What accounting exports or integrations will shops require?
- How should Financial Intelligence stay separate from payment processing?

### Marketplace And Commerce Questions

- Is the first commerce surface dealer storefronts, marketplace search, customer portal commerce, or ecommerce integration?
- How should local-first ranking work?
- What inventory must be explicitly shared before it can appear in marketplace or network search?
- How should dealer pricing, supplier pricing, shipping, pickup, and install options be shown together?
- Who owns the customer relationship when marketplace demand is routed to a participating shop?
- How should returns, cancellations, taxes, and fulfillment responsibilities work?

### Rewards And Contingency Questions

- What actions should earn iM Points first?
- Who can earn points: organizations, employees, technicians, customers, suppliers, or all of them?
- What can points be redeemed for without creating legal or accounting problems?
- How are technical contributions verified before rewards are issued?
- How are fraud, abuse, duplicate submissions, and low-quality contributions handled?
- How should IndieMoto Contingency experience inform this design?

### Data And AI Questions

- What platform-level AI usage should be tracked for cost, limits, billing, and support?
- Which domain events are mandatory in the first foundation build?
- What event payload standards are required?
- What data should be structured now for future AI without overbuilding analytics?
- What data can be anonymized safely?
- What data must never leave tenant boundaries?
- How should tenants opt into or out of data contribution programs?
- When does IM1OS need a separate analytics store or warehouse?
- What explanations must accompany AI or intelligence recommendations?

## 6. Assumptions

Current working assumptions:

- Independent powersports shops have more urgent pain in service and parts operations than in unit sales.
- Proven legacy workflows should be preserved unless there is a clear business reason to change them.
- Multi-tenancy must be designed from the beginning.
- A shared database/shared schema is acceptable for the initial SaaS architecture if tenant isolation is enforced rigorously.
- Domain events are the safest foundation for audit, timeline, reporting, analytics, and future AI.
- Structured operational data is more valuable than free text alone.
- Supplier abstraction is required before adding multiple supplier integrations.
- The Parts Engine should become the commerce catalog foundation.
- Purchase Intelligence and Procurement Intelligence are separate problems.
- Merchant Services are strategically valuable because payment data can improve intelligence, not only because of processing revenue.
- Network Intelligence and Network Value Exchange require explicit consent and privacy controls before launch.
- Commerce and marketplace features should be local-first and dealer-enabling.
- AI should be introduced after the data model and event foundation can explain recommendations.
- IM1 will need a platform control plane before scaling beyond early tenants.
- Tenant Manager is likely the first production IM1 Platform module, but its exact timing relative to core tenant modules needs validation.
- Platform support impersonation is valuable but cannot be implemented safely without audit, permissions, and access-history controls.

These assumptions should be validated. They are not final architecture decisions.

## 7. Discovery Tasks

Prioritized discovery backlog:

1. Map the legacy Vehicle Intake workflow in detail.
2. Map the legacy Work Order and stage-transition workflow.
3. Map the legacy Technician Workspace workflow.
4. Map the legacy Estimate approval workflow, including customer portal behavior.
5. Map the legacy Parts Search, Inventory, Purchase Order, and Receiving workflows.
6. Define the first-pass Work Order domain model and state machine.
7. Define the first-pass Parts domain model around canonical part identity, supplier listings, inventory, purchase orders, and receiving.
8. Define the first-pass Estimate and Estimate Line model.
9. Define the domain event naming, payload, correlation, and retention conventions.
10. Identify first supplier integration scope and contractual constraints.
11. Identify first payment event requirements without committing to merchant services.
12. Define tenant data-sharing consent concepts for future benchmarking and Network Intelligence.
13. Define minimum audit and timeline requirements for operational workflows.
14. Define first release module boundaries and permissions.
15. Validate whether shared database/shared schema remains sufficient for expected early scale.
16. Create architecture proposals before implementing each major module.
17. Defer marketplace, rewards, merchant services, and network collaboration implementation until required controls are understood.
18. Define IM1 Platform control-plane boundaries.
19. Define Tenant Manager minimum viable scope.
20. Define tenant lifecycle states, plan model, and feature/module entitlement model.
21. Define support impersonation audit and permission requirements.
22. Define platform health metrics and tenant health indicators.

## 8. Build-Now vs Decide-Later

### Build Now

Safe foundation work:

- Organization, Location, User, Membership, Role, and Permission foundation.
- Organization-scoped authorization.
- Location-aware operational permissions.
- Shared database/shared schema with strict tenant filters.
- Audit and domain event foundation.
- Structured Work Order, Customer, Customer Vehicle, Estimate, Parts, Inventory, Purchase Order, Receiving, Invoice, and Payment placeholders where needed for core workflow.
- Domain event conventions for important business actions.
- Legacy workflow documentation.
- Module boundary proposals before implementation.
- Parts Engine abstractions that do not depend on one supplier.
- Event-ready data model for future analytics and AI.
- Basic platform model concepts for tenant status, plan, provisioning state, feature entitlements, and tenant health if needed for onboarding.

### Decide Later

Do not lock yet:

- Full marketplace architecture.
- Merchant-services provider and payment-processing architecture.
- iM Points legal, tax, and accounting treatment.
- Rewards redemption catalog.
- Supplier-sponsored campaign mechanics.
- Network inventory exchange rules.
- Shared purchasing commitments.
- AI model/provider strategy beyond replaceable integration boundaries.
- Data warehouse, data lake, or streaming infrastructure.
- Multi-database or schema-per-tenant strategy.
- Customer rewards across participating shops.
- Dealer marketplace ownership and fulfillment rules.
- Full IM1 Platform application structure.
- Billing provider and subscription automation.
- Support impersonation implementation.
- Staged deployment automation.
- Platform marketplace administration.

## 9. Immediate Next Engineering Step

Recommended smallest safe foundation:

Build the operational and event foundation needed by every future module.

Immediate engineering scope:

- Confirm tenant context and organization-scoped authorization are solid.
- Keep shared database/shared schema.
- Ensure every tenant-owned entity has `OrganizationId`.
- Ensure operational entities are designed to support `LocationId`.
- Formalize domain event conventions.
- Preserve domain event recording for future audit, timeline, analytics, AI, and network intelligence.
- Start with the core service and parts domain objects: Customer, Customer Vehicle, Work Order, Work Order Stage, Estimate, Estimate Line, Manufacturer Part, Supplier Listing, Inventory Item, Inventory Transaction, Purchase Order, Purchase Order Line, Receiving, Invoice, Payment, Document, Photo, and Audit Event.
- Before building UI pages, produce a short architecture proposal for the first module selected from legacy workflow discovery.

This lets development move forward responsibly without requiring final answers for commerce, marketplace, merchant services, rewards, network intelligence, or AI.
