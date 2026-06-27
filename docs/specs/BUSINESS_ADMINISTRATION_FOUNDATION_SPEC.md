# Business Administration Foundation Specification

Status: Sprint 2 implementation scope.

Business Administration is the tenant-level operating system configuration area for an independent powersports business. It is intentionally separate from Platform Administration and from later operational modules.

## Definition Of Done

A new tenant owner can receive an invitation, activate the account, log in, complete onboarding, configure the business, create locations, create employees, assign roles, configure labor rates, and reach a dashboard showing that the business is ready to begin operations.

## Implemented Scope

- Owner-only Business Administration workspace inside the tenant application.
- Business Profile fields for legal identity, DBA, logo, website, phone, email, tax ID, address, time zone, language, currency, date format, and time format.
- Multiple location configuration with address, phone, time zone, default labor rate, default tax region, and status.
- Employee invitation foundation backed by tenant users, organization memberships, and organization-scoped roles.
- Flexible role and permission display using existing role and permission tables instead of hardcoded authorization types.
- Labor configuration for default, diagnostic, emergency, weekend, environmental fee, and shop supplies percentage.
- Tax configuration for a default rate and future regional overrides.
- Notification preferences for email, SMS, future push, customer, and technician notifications.
- Supplier, merchant, accounting, notification, and future connector placeholders.
- Audit log and tenant timeline records for administrative changes.
- Business Dashboard readiness message: "Your business is now ready to begin operations."

## Architecture Notes

- `Organization` remains the tenant security boundary.
- `BusinessConfiguration` stores tenant-wide operational defaults consumed by future modules.
- `Location` carries shop-level defaults that future work orders, inventory, and reporting can consume.
- Employees are tenant users plus organization memberships and roles, not a separate identity store.
- Only the Owner role can access Business Administration in this sprint.
- Future custom roles are supported by the existing role and permission model.

## Out Of Scope

- Customers
- Vehicles
- Work Orders
- Inventory
- Parts
- Supplier APIs
- Merchant APIs
- Marketplace
- AI
- Repair Orders
- Purchasing
