# Module Guidelines

Do not implement business modules in the foundation phase.

IM1OS modules must support the Operating System and Commerce Network vision. Core modules should still follow the phased operational path from customer intake through work order, diagnosis, estimate, parts search, supplier availability, purchase ordering, receiving, repair, invoice, and customer portal before commerce and network modules are implemented.

See `docs/MODULE_DEFINITIONS.md` for the current module families and boundaries.

Before implementing any module, produce a short architecture proposal for review. Use `docs/LEGACY_FUNCTIONAL_SPEC.md` as the functional reference and preserve proven legacy workflows unless a change is explicitly approved.

When modules are introduced, each module should define:

- Domain concepts and invariants.
- Application commands, queries, DTOs, and permissions.
- Persistence mappings and migrations.
- REST API surface and versioning.
- UI entry points.
- Background jobs, if needed.
- Audit events.

Modules must consume shared identity, tenancy, settings, feature flags, and audit logging instead of creating duplicates.

## Core Module Families

Foundation-aligned modules include:

- Organizations, locations, employees, memberships, roles, and permissions.
- Customers and customer communication.
- Customer vehicles and vehicle intake.
- Work orders, diagnosis, estimates, repair workflow, and service history.
- Parts search, supplier catalogs, supplier availability, and supplier ordering.
- Shop parts inventory, purchase orders, receiving, and parts allocation.
- Invoices and payment workflow boundaries.
- Customer portal.
- Ecommerce integration.
- Service and parts reporting.

## Optional Future Modules

The following are optional future modules and must not be treated as foundation requirements:

- Vehicle sales.
- Unit inventory.
- F&I.
- Deal jackets.
- Floor planning.

## Tenancy Rules

The tenant is `Organization`. `Organization` is the security boundary.

Every tenant-owned module table must contain `OrganizationId`. Most operational tables should also contain `LocationId`. Permissions must be organization-specific, and modules that operate at a physical shop location must respect location permissions inside the organization.
