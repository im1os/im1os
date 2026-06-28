# Architecture

iM1 OS uses Clean Architecture with a modular monolith starting point. The API, web shell, and worker host depend on Application and Infrastructure. Domain remains persistence-agnostic.

The product architecture begins with service and parts operations for independent powersports businesses, then grows into an operating system and commerce network. It is not centered on vehicle sales or traditional dealership deal flow.

Primary workflow:

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

Every architectural decision should support this workflow before considering optional future modules.

Layers:

- `iM1os.Domain`: entities and domain concepts.
- `iM1os.Application`: contracts, DTOs, use-case boundaries, and service interfaces.
- `iM1os.Infrastructure`: EF Core, PostgreSQL, Redis cache setup, JWT creation, seed data, and external technical services.
- `iM1os.Api`: REST API, versioning, Swagger, authentication, authorization, exception handling, and health checks.
- `iM1os.Web`: initial dashboard shell.
- `iM1os.Workers`: background worker host.

Future modules should live behind application contracts and avoid leaking persistence or UI concerns across boundaries.

## Product Boundary

IM1 should be understood as three related products.

### IM1 Platform

IM1 Platform is the SaaS control plane above all tenants.

Scope:

- Tenant Manager
- Provisioning
- Billing and licensing
- Feature management
- Deployment management
- Tenant health
- Support and audited impersonation
- AI usage
- Platform marketplace administration
- Platform analytics

IM1 Platform is not part of any dealer tenant. It is IM1's business administration layer.

Platform-facing screens should carry the iM1os Operating System mark as the primary brand signal, using the supplied red, white, and black logo without recoloring it.

### IM1OS

IM1OS is the tenant operating system used by each shop.

Scope:

- Business administration
- Human Resources
- Customers
- Vehicles
- Work orders
- Parts
- Inventory
- Intelligence engines
- Customer portal

### IM1 Network

IM1 Network is the ecosystem layer.

Scope:

- Dealer marketplace
- Shared intelligence
- Rewards and contingency
- Procurement network
- Supplier promotions
- Benchmarking
- Social and Market Intelligence
- Future consortium capabilities

The Platform runs the SaaS. The OS runs the shop. The Network connects everyone.

## Product Architecture Layers

IM1OS has four product layers.

### Layer 1: Operations

The operational system of record:

- Identity
- Customers
- Vehicles
- Work orders
- Parts
- Inventory
- Invoices
- Payments

### Layer 2: Intelligence

The system of recommendations:

- Service Intelligence
- Parts Intelligence
- Purchase Intelligence
- Procurement Intelligence
- Supplier Promotion Intelligence
- Shop Intelligence
- Social and Market Intelligence
- Network Intelligence
- Financial Intelligence

### Layer 3: Commerce

The commerce network:

- Merchant Services
- Dealer Marketplace
- Dealer websites
- B2B commerce
- Supplier network
- Customer portal
- Mobile experiences
- Rewards and Network Value Exchange

### Layer 4: AI

AI consumes the data, events, recommendations, and permissions produced by the lower layers. AI does not own the data and must not replace the domain model.

## Tenancy

Organization is the primary tenant and security boundary within iM1 Platform. Every Organization represents a single customer business and owns all operational data, users, subscriptions, integrations, and locations. All operational records are scoped by `OrganizationId`.

Location represents a physical or operational site within an Organization, such as a storefront, service center, warehouse, mobile trailer, or remote office. Location-specific data additionally includes `LocationId`, enabling multi-location operations while maintaining a single tenant boundary.

The iM1 Platform is the global SaaS control plane responsible for provisioning Organizations, authentication, licensing, billing, monitoring, deployments, and platform-wide administration. It contains no customer operational data except that required to manage Organizations.

Platform Administration sits above `Organization`. Platform-level users and services may manage tenants, lifecycle, billing, feature access, health, support, and analytics, but tenant-owned business data remains protected by organization boundaries.

An organization owns:

- Locations
- Employees
- Customers
- Vehicles
- Work orders
- Parts inventory
- Purchase orders
- Invoices
- Reports

Employee is the tenant master record for people working for the company. Some employees have login accounts and some do not. Login accounts may belong to multiple organizations. Roles and permissions are scoped to an organization and attach to the employee's login access when enabled. Location permissions exist inside an organization for workflows that should be limited to one or more physical stores or service locations.

Every tenant-owned table must contain `OrganizationId`. Most operational tables should also contain `LocationId` because service, parts, receiving, repair, and invoicing workflows usually happen at a specific location.

## UI Terminology

iM1 Platform is a multi-tenant SaaS application, but customers should never be exposed to platform or tenancy terminology.

Internal architecture uses `Organization` as the canonical tenant entity. Use `OrganizationId` throughout the database, APIs, permissions, services, and business logic. All tenant isolation is enforced through `OrganizationId`.

Platform Administration is an internal control plane used only by iM1 administrators. Platform Admin screens may use the term Organization because they manage customer organizations across the platform.

Customer Administration must not expose the terms Tenant, Organization, or Platform. Present the Organization as Business throughout customer-facing UI.

Customer-facing examples include:

- Business Profile
- Business Settings
- Business Locations
- Employees
- Business Branding
- Business Hours
- Business Subscription

Customers should experience iM1 OS as software built for their business, not as one tenant within a larger SaaS platform.

This is a presentation-layer concern only. Do not create separate Business models or duplicate database entities. Business UI maps directly to the `Organization` entity.

## iM1 UI

iM1 UI is the reusable application framework for iM1 OS and IM1 Platform surfaces. It owns theme tokens, the application shell, core components, data grid behavior, service-layer UI patterns, and permission-aware actions.

Business modules should be composed from iM1 UI primitives rather than hand-built screen-specific UI. Third-party UI dependencies must be wrapped by iM1 UI and must not be imported directly by modules or pages.

The detailed iM1 UI contract lives at [docs/specs/IM1_UI_FRAMEWORK_SPEC.md](specs/IM1_UI_FRAMEWORK_SPEC.md).

## Core Product Areas

The foundation should prepare for these core areas:

- Service workflow
- Parts operations and procurement intelligence
- Supplier catalog and availability integration
- Purchase ordering and receiving
- Customer communication
- Customer portal
- Ecommerce integration
- Reporting across service and parts operations

The following are not core foundation areas:

- Vehicle sales
- F&I
- Deal jackets
- Floor planning
- Unit inventory

Those may be added later as optional modules only after the service and parts platform boundaries are stable.

## Parts Engine

The Parts Engine is a core platform capability, not a simple inventory module.

It is built around canonical manufacturer part identity. Supplier SKUs are mappings to manufacturer parts, not the primary identity. Supplier listings, local inventory, purchase orders, receiving, ecommerce availability, and work order parts requirements should all connect through the same canonical part model.

The Purchase Intelligence Engine recommends the best real-time purchasing decision for a specific work order or immediate part need by balancing cost, availability, freight, delivery speed, existing purchase orders, shop inventory, customer priority, technician schedule impact, vendor reliability, and dealer preferences.

Procurement Intelligence is separate. It recommends strategic stocking decisions across weeks, months, seasons, supplier promotions, inventory turns, market trends, race schedules, and benchmark data. Supplier Promotion Intelligence is a specialized optimization capability under Procurement Intelligence for rebates, BOGOs, bulk discounts, freight incentives, seasonal programs, and supplier purchasing opportunities.

See `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md` for the formal product specification.

## Intelligence Layer

The Intelligence Layer contains the reusable decision-making capabilities of IM1OS.

Competitors can copy screens and workflows. The defensible value of IM1OS is the accumulated domain data, decision models, supplier knowledge, service knowledge, and industry-specific intelligence that improves every shop using the platform.

Everything that helps a shop make a decision belongs in this layer.

Business Engines are reusable capabilities. Modules are user-facing surfaces that consume those engines.

### Core Operational Engines

- Identity Engine
- Customer Engine
- Vehicle Engine
- Work Order Engine
- Parts Intelligence Engine
- Purchase Intelligence Engine
- Service Intelligence Engine

### Business Intelligence Engines

- Procurement Intelligence Engine
- Supplier Promotion Intelligence Engine
- Network Intelligence Engine
- Network Value Exchange
- Financial Intelligence Engine
- Shop Intelligence Engine
- Social and Market Intelligence Engine

### Future Platform Engines

- AI Assistant Engine
- Analytics and Benchmarking Engine
- Automation Engine
- Customer Experience Engine

### Foundational Intelligence Questions

- Parts Intelligence: what part is this?
- Purchase Intelligence: where should I buy this part for this immediate need?
- Procurement Intelligence: what should I stock, when should I stock it, and how much should I buy?
- Supplier Promotion Intelligence: how should supplier programs change what, when, where, and how much I buy?
- Network Intelligence: what opportunities can participating shops identify together without exposing private tenant data?
- Network Value Exchange: how should trusted contribution to the network be recognized and rewarded?
- Financial Intelligence: what financial behavior, payment workflow, or profitability signal should the shop act on?
- Service Intelligence: what should I do, what should I charge, and what information do I need to complete the repair?
- Shop Intelligence: how can I run a better business?
- Social and Market Intelligence: what will customers ask for next?

Service Intelligence is the foundation for the Digital Service Advisor. It should combine vehicle context, customer complaints, technician findings, labor operations, required parts, technical specifications, shop labor rates, and historical repair data to create structured estimate and repair recommendations.

See `docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md`.

Procurement Intelligence is the foundation for strategic inventory purchasing. It should analyze historical sales, work order parts usage, special order frequency, inventory turns, supplier promotions, seasonal demand, regional trends, race schedules, vendor fill rates, lead times, and anonymized benchmarks to recommend what a shop should stock.

See `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`.

Supplier Promotion Intelligence is the promotion optimization engine inside Procurement Intelligence. It should transform supplier promotions into structured recommendations with expected savings, risk, confidence, and supporting evidence.

See `docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md`.

Network Intelligence is the tenant-safe collaboration and aggregation engine. It should use voluntary participation, anonymized aggregates, and explicitly shared information to create network-level demand, purchasing, inventory exchange, promotion, and market recommendations.

See `docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md`.

The Network Value Exchange is the participation and rewards layer for the operating network. It should use iM Points as network utility credits to reward trusted actions by shops, suppliers, technicians, and customers without creating cash equivalents or exposing private tenant data.

See `docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md`.

Financial Intelligence is the recommendation layer around estimates, approvals, invoices, payments, settlement, profitability, and benchmark-safe financial behavior. Merchant Services may move money; Financial Intelligence explains financial outcomes.

See `docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md`.

The IM1OS Commerce Network is the future local-first commerce layer for dealer marketplace, dealer websites, supplier network, merchant services, customer portal, mobile commerce, B2B, and rewards.

See `docs/specs/COMMERCE_NETWORK_SPEC.md`.

The IM1 Platform control plane manages tenants, provisioning, billing, licensing, feature access, health, support, deployments, and platform analytics across the SaaS.

See `docs/specs/IM1_PLATFORM_CONTROL_PLANE_SPEC.md`.

Social and Market Intelligence is the early warning system for market demand. It should turn public market signals, racing trends, supplier changes, industry news, and anonymized IM1OS data into recommendations before demand appears in sales history.

See `docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md`.

## Data Intelligence

Data is a product asset. IM1OS must capture structured operational data and emit immutable domain events from core business actions so the platform can support timeline history, audit logs, reporting, benchmarking, future data warehouse exports, and AI recommendations.

The operational database remains optimized for live application workflows. Analytics and benchmarking should evolve through domain events, event stores, reporting pipelines, and future warehouse models rather than by overloading transactional tables.

See `docs/specs/DATA_INTELLIGENCE_SCOPE.md`.

See `docs/DOMAIN_MODEL.md` for domain concepts and `docs/MODULE_DEFINITIONS.md` for module boundaries.
