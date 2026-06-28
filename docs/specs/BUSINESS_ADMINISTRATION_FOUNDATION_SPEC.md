# Business Administration Foundation Specification

Status: Sprint 2 implementation scope.

Business Administration is the tenant-level operating system configuration area for an independent powersports business. It is intentionally separate from Platform Administration. Human Resources begins inside Business Administration and grows into a core operating module.

## Definition Of Done

A new tenant owner can receive an invitation, activate the account, log in, complete onboarding, configure the business, create locations, create employees, assign roles, configure labor rates, and reach a dashboard showing that the business is ready to begin operations.

## Implemented Scope

- Owner-only Business Administration workspace inside the tenant application.
- Business Profile fields for legal identity, DBA, logo, website, phone, email, tax ID, address, time zone, language, currency, date format, and time format.
- Multiple location configuration with address, phone, time zone, default labor rate, default tax region, and status.
- Employee invitation foundation backed by employee records, optional login accounts, organization memberships, and organization-scoped roles.
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
- Employee is the tenant master record for people working for the company. Login accounts are optional access records attached to employees.
- Do not model company workers as "Users" in business-facing scope. Some employees clock in, hold documents, receive assets, or require OSHA records without ever logging in.
- Only the Owner role can access Business Administration in this sprint.
- Future custom roles are supported by the existing role and permission model.
- Human Resources scope is governed by `docs/specs/HUMAN_RESOURCES_SCOPE.md`.

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
- Full HR workflows beyond employee invitation and role assignment
