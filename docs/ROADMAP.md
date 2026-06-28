# Roadmap

Phase 1 establishes the SaaS platform foundation and the service and parts operating system: authentication, authorization, employee-aware login accounts, organization memberships, organization-scoped roles and permissions, locations, location permissions, multi-tenancy, settings, feature flags, audit logging, health checks, logging, API versioning, workers, and documentation.

Phase 1A should define and begin the IM1 Platform control plane only where needed for onboarding and operating tenants: Tenant Manager scope, tenant lifecycle states, provisioning state, plan/entitlement model, tenant health indicators, and platform audit requirements.

Phase 2 should add the iM1 UI framework and module infrastructure before additional business screens grow: theme tokens, application shell, core UI components, IM1DataGrid, service-layer UI patterns, module discovery conventions, permissions per module, migration ownership, API grouping, UI navigation extension points, audit event conventions, background job conventions, and tenant-isolation test requirements.

Phase 3 should begin core service workflow modules only after module standards, security reviews, and database ownership rules are accepted. The first business modules should support customer intake, vehicles, work orders, diagnosis, estimates, and customer communication.

Phase 4 should deepen parts operations: parts search, supplier catalog integration, supplier availability, shop inventory, purchase orders, receiving, parts allocation to work orders, and ecommerce integration.

Phase 5 should complete operational flow: repair completion, invoicing, customer portal workflows, service and parts reporting, and cross-location operational visibility.

Phase 6 should add commerce foundations only after operations and intelligence are stable: merchant-services boundaries, payment event capture, dealer storefront boundaries, marketplace search contracts, local-first commerce rules, and Commerce Network participation settings.

Phase 7 should grow network effects: benchmark-safe shared intelligence, optional inventory exchange, Network Value Exchange, supplier partnerships, sponsored campaigns, and marketplace expansion.

Optional future modules may include vehicle sales, unit inventory, F&I, deal jackets, and floor planning. These are not core platform phases and must not shape the foundation.

See `docs/DOMAIN_MODEL.md` and `docs/MODULE_DEFINITIONS.md` before designing or implementing business modules.

Human Resources is core iM1 OS scope. Employee is a master record whether or not login access exists; do not design company worker workflows around "Users." See `docs/specs/HUMAN_RESOURCES_SCOPE.md`.

The iM1 UI framework is now a foundation prerequisite for module UI. See `docs/specs/IM1_UI_FRAMEWORK_SPEC.md`. Business modules should compose iM1 UI primitives and must not directly consume third-party UI libraries.

Before rebuilding a module, study the legacy implementation and update `docs/LEGACY_FUNCTIONAL_SPEC.md`. Architecture proposals should be captured in `docs/FOUNDATION_ARCHITECTURE_PROPOSAL.md` or a module-specific proposal before implementation.

The Parts Engine has its own formal product specification at `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md`. The first Parts Domain implementation should follow `docs/PARTS_DOMAIN_IMPLEMENTATION_PROPOSAL.md` after review.

Service Intelligence and Data Intelligence are now foundation scope. See `docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md` and `docs/specs/DATA_INTELLIGENCE_SCOPE.md`. The immediate build requirement is domain/event readiness, not dashboards or AI features.

Procurement Intelligence is separate from per-work-order Purchase Intelligence. See `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`. Treat these as Business Engines consumed by modules and surfaces, not as UI modules themselves.

Supplier Promotion Intelligence is a specialized engine under Procurement Intelligence. See `docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md`. Do not build promotion scraping or automated purchasing first; make supplier programs structured, explainable, and recommendation-ready.

Network Intelligence is a voluntary, tenant-safe business intelligence engine. See `docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md`. Do not build shared purchasing, supplier negotiation, or inventory exchange workflows before participation settings, anonymization, aggregation, audit, and explicit-sharing rules are mature.

The Network Value Exchange is the future participation and rewards layer for the operating network. See `docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md`. Do not implement iM Points, supplier-funded incentives, customer rewards, or redemption workflows before legal, tax, accounting, fraud, privacy, and tenant-consent controls are designed.

Financial Intelligence and the Commerce Network are long-term platform scope. See `docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md` and `docs/specs/COMMERCE_NETWORK_SPEC.md`. Do not build merchant services, dealer marketplace, or payment-intelligence workflows before service, parts, inventory, invoice, payment event, compliance, and dealer participation boundaries are accepted.

IM1 Platform is the control plane above tenants. See `docs/specs/IM1_PLATFORM_CONTROL_PLANE_SPEC.md`. Do not build billing automation, support impersonation, deployment automation, or platform marketplace administration before tenant lifecycle, audit, permission, and legal controls are accepted.

Social and Market Intelligence is part of the long-term Intelligence Layer. See `docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md`. Do not build social scraping, market dashboards, or automated market recommendations in the first implementation; make the architecture event-ready so these signals can later feed Procurement, Parts, Service, and Shop Intelligence.
