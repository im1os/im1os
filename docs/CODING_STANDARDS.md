# Coding Standards

Use .NET 10, nullable reference types, async APIs, constructor injection, and clear names. Keep business rules out of controllers and views. Prefer application services over shared static helpers.

Guidelines:

- Domain must not depend on infrastructure.
- Application defines contracts and use-case models.
- Infrastructure implements technical concerns.
- API endpoints return DTOs, not EF entities.
- Logging must be meaningful and must not include secrets.
- Tests should cover tenant isolation, identity flows, permissions, and settings behavior as they mature.
- New business code must align to the Operating System and Commerce Network vision, while preserving the phased priority of service and parts operations first.
- Do not introduce vehicle-sales-first, F&I, deal jacket, floor planning, or unit-inventory assumptions into foundation code.
- Tenant-owned application flows must carry organization context explicitly.
- Location-scoped workflows must carry location context explicitly.
