# iM1 OS

**The Operating System and Commerce Network for Independent Powersports Businesses**

iM1 OS is a modern .NET 10 cloud platform foundation for independent powersports service, parts, intelligence, and commerce operations. This repository establishes the permanent architecture for a platform centered on shop workflow, parts operations, supplier integration, customer communication, ecommerce integration, identity, multi-tenancy, configuration, auditability, health checks, background processing, and an initial dashboard shell.

The core product is not a traditional Dealer Management System and is not centered on vehicle sales, F&I, deal jackets, floor planning, or unit inventory. Those capabilities may become optional modules later, but they are not part of the foundation.

The foundation workflow is:

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

The tenant is `Organization`. `Organization` is the security boundary. Users may belong to multiple organizations, permissions are organization-specific, and location permissions exist inside an organization.

Domain: `im1os.com`

Start with [docs/vision/IM1OS_NORTH_STAR.md](docs/vision/IM1OS_NORTH_STAR.md) and [docs/vision/IM1OS_PRODUCT_MANIFESTO.md](docs/vision/IM1OS_PRODUCT_MANIFESTO.md) before making product or architecture decisions.

The Parts Engine product specification lives at [docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md](docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md).

The IM1 Platform control plane scope lives at [docs/specs/IM1_PLATFORM_CONTROL_PLANE_SPEC.md](docs/specs/IM1_PLATFORM_CONTROL_PLANE_SPEC.md).

The Tenant Identity Experience scope lives at [docs/specs/TENANT_IDENTITY_EXPERIENCE_SPEC.md](docs/specs/TENANT_IDENTITY_EXPERIENCE_SPEC.md).

The Business Administration Foundation scope lives at [docs/specs/BUSINESS_ADMINISTRATION_FOUNDATION_SPEC.md](docs/specs/BUSINESS_ADMINISTRATION_FOUNDATION_SPEC.md).

The Intelligence Layer and data foundation are formal product scope:

- [docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md](docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md)
- [docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/COMMERCE_NETWORK_SPEC.md](docs/specs/COMMERCE_NETWORK_SPEC.md)
- [docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md](docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md)
- [docs/specs/DATA_INTELLIGENCE_SCOPE.md](docs/specs/DATA_INTELLIGENCE_SCOPE.md)

## Solution Layout

```text
iM1os.sln
/src
  iM1os.Api
  iM1os.Application
  iM1os.Domain
  iM1os.Infrastructure
  iM1os.Web
  iM1os.Workers
/tests
  iM1os.Tests
/docs
/deploy
```

## Local Commands

```powershell
dotnet restore .\iM1os.sln
dotnet build .\iM1os.sln
dotnet test .\iM1os.sln
dotnet run --project .\src\iM1os.Api\iM1os.Api.csproj
dotnet run --project .\src\iM1os.Web\iM1os.Web.csproj
```

The default PostgreSQL and Redis connection strings are development placeholders in `appsettings.json`.
