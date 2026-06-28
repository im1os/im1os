# iM1 OS AI Engineering Guide

Contributors must treat this repository as a long-lived platform, not as a short-term application.

Before making product, architecture, or workflow decisions, read `docs/vision/IM1OS_NORTH_STAR.md` and `docs/vision/IM1OS_PRODUCT_MANIFESTO.md`.

Use the established .NET 10 Clean Architecture structure, preserve `iM1os.*` namespaces, and keep business modules out of the foundation until explicitly approved. Every change must respect tenant isolation, security, auditability, testability, and maintainable boundaries.

IM1OS is an Operating System and Commerce Network for independent powersports businesses. It starts with service and parts operations. It is not a traditional Dealer Management System and must not drift toward a vehicle-sales-first product model.

Core product assumptions:

- The platform revolves around customer vehicle intake, work orders, diagnosis, estimates, parts search, supplier availability, parts ordering, receiving, repair completion, invoicing, and the customer portal.
- Parts operations are foundational. Vehicle sales, F&I, deal jackets, floor planning, and unit inventory are future optional modules only.
- The Intelligence Layer is a first-class architecture concept. Service, Parts, Purchase, Procurement, Supplier Promotion, Network, Shop, and Social and Market Intelligence are reusable decision capabilities, not isolated UI pages.
- Supplier Promotion Intelligence belongs under Procurement Intelligence. It optimizes supplier programs against demand, inventory, cash flow, shelf space, and purchase timing; it is not a generic deals page.
- Network Intelligence must be voluntary and tenant-safe. It may use anonymized aggregates or explicitly shared information, but it must never expose one tenant's confidential operational data to another tenant.
- Network Value Exchange rewards trusted network participation through iM Points as utility credits. Do not model points as cash, stored value, gift cards, or banking without explicit legal/accounting review.
- Financial Intelligence and Merchant Services are separate concepts. Merchant Services may move money; Financial Intelligence explains payment, settlement, profitability, and benchmark-safe financial behavior.
- The Commerce Network should be local-first and dealer-enabling. Do not make ecommerce platforms or marketplace pages the source of truth for parts identity, inventory, or customer ownership.
- Social and Market Intelligence is an early warning system for demand signals. Do not treat it as marketing automation, and do not build scraping before the event and data foundation is ready.
- The tenant is `Organization`. `Organization` is the security boundary.
- Users may belong to multiple organizations.
- Permissions are organization-specific.
- Location permissions exist inside an organization.
- Every tenant-owned table must contain `OrganizationId`.
- Most operational tables should also contain `LocationId`.
- UI terminology is context-specific. Internal code, APIs, database schema, permissions, services, and business logic use `Organization` and `OrganizationId`. Platform Admin screens may use Organization because they are internal iM1 control-plane surfaces. Customer-facing administration must present the same entity as Business and must not expose Tenant, Organization, or Platform terminology.
- iM1 UI is the reusable application framework. Pages and modules must consume iM1-owned UI primitives and wrappers. Do not import AG Grid, MUI, Bootstrap, charting libraries, or other third-party UI libraries directly outside the iM1 UI component boundary.

Do not add unrelated product assumptions, traditional DMS assumptions, or external platform dependencies.
