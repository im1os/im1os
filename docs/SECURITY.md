# Security

Security is a platform concern in iM1 OS. Authentication uses JWT bearer tokens. Authorization is role and permission based. Tenant isolation is enforced through organization-scoped data models and EF Core query filters.

The tenant is `Organization`. `Organization` is the security boundary. Users may belong to multiple organizations, but every organization context must have its own membership, role, and permission evaluation.

Location permissions exist inside an organization. A user who has access to one location must not automatically gain access to every service, parts, receiving, repair, invoice, or report workflow in the organization unless their organization permissions allow it.

Requirements:

- Never commit production secrets.
- Rotate the development JWT signing key before deployment.
- Store passwords only as one-way hashes.
- Keep audit trails for security-sensitive changes.
- Validate all external input.
- Review every new module for tenant isolation and permission coverage.
- Require `OrganizationId` on every tenant-owned table.
- Require `LocationId` on operational tables when the data belongs to a physical shop location.
- Test cross-organization access attempts for every service and parts module.
