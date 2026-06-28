# Database

iM1 OS uses PostgreSQL through Entity Framework Core migrations. The initial schema is `platform`.

Foundation tables include organizations, locations, employee-aware login accounts, organization memberships, roles, permissions, account-role links, role-permission links, location permission links, feature flags, application settings, and audit logs.

The tenant is `Organization`. `Organization` is the security boundary for SaaS isolation.

Operational data belongs to an organization. Most service and parts operations also happen at a specific location inside the organization.

Rules:

- Every schema change must be represented by an EF Core migration.
- Tenant-owned data must include `OrganizationId` unless it is explicitly global platform data.
- Most operational tables should include `LocationId`, especially service, parts inventory, receiving, repair, invoice, and reporting data.
- Employee is the company worker master record. Login accounts may belong to multiple organizations.
- Permissions are organization-specific.
- Location permissions exist inside an organization.
- Tenant-owned unique constraints must include `OrganizationId` unless the value is intentionally globally unique.
- Use indexes for tenant lookups, unique normalized identity fields, and audit queries.
- Do not bypass migrations for production schema changes.

Core operational tables should be designed around customer intake, vehicles, work orders, diagnosis, estimates, parts search, supplier availability, purchase orders, receiving, repair, invoicing, and customer portal workflows.
