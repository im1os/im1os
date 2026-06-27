# Tenant Identity Experience Specification

Status: Sprint 2 implementation scope.

The Tenant Identity Experience establishes how a newly provisioned owner activates and enters their IM1OS tenant for the first time.

## Definition Of Done

A Platform Administrator provisions a tenant.

The owner receives an invitation.

The owner activates their account.

The owner signs into IM1OS.

The owner completes onboarding.

The owner lands on the Business Dashboard.

The tenant is now ready for Business Administration to be implemented.

## Implemented Scope

- Tenant owner invitations with hashed activation tokens.
- Owner account activation and password setup.
- Tenant email and password login.
- Remember Me via persistent cookie authentication.
- Session timeout via cookie expiration and sliding renewal.
- Account lockout after repeated failed attempts.
- Password reset request and completion records.
- Email verification at owner activation.
- Active organization resolution in authenticated tenant claims.
- Role and permission claims in tenant session context.
- Owner-only onboarding wizard.
- Business dashboard after onboarding.
- Basic tenant user profile and password change.
- Tenant identity events for invitation, activation, login, logout, password, and onboarding milestones.
- Future MFA architecture fields on tenant users.

## Out Of Scope

- Customer, vehicle, work order, parts, inventory, supplier integration, merchant processing, AI, and marketplace modules.
- Full SMTP/email provider integration. The current sender contract records the flow and can be backed by a real provider.
- Real supplier and merchant connections. Onboarding includes placeholders only.
