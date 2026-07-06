# Module And Business Engine Definitions

IM1OS is an Operating System and Commerce Network for independent powersports businesses. Modules should be defined around the customer service lifecycle, parts operations, intelligence, and future commerce network boundaries.

The foundation must not assume a traditional Dealer Management System centered on unit sales.

Business Engines are reusable capabilities. Modules are user-facing or integration-facing surfaces that consume those engines.

The Intelligence Layer contains decision-making capabilities. It includes core operational intelligence such as Parts, Purchase, and Service Intelligence; business intelligence such as Procurement, Supplier Promotion, Network, Network Value Exchange, Financial, Shop, and Social and Market Intelligence; and future platform intelligence such as AI Assistant, Analytics and Benchmarking, Automation, and Customer Experience.

## Product Boundary

IM1 Platform is the SaaS control plane above tenants. IM1OS is the tenant operating system used by each shop. IM1 Network is the ecosystem layer connecting participating shops, suppliers, marketplace, rewards, and shared intelligence.

Do not mix Platform Administration with tenant Business Administration.

## IM1 Platform Modules

### Tenant Manager

Owns platform-level tenant lifecycle visibility and administration.

Scope:

- Tenant list
- Tenant status
- Plan
- Version
- Login account count
- Location count
- Last login
- Health
- Tenant detail
- Subscription visibility
- Usage visibility
- Module enablement visibility
- Supplier connection status
- Merchant account status
- Support ticket visibility
- Logs
- AI usage

### Provisioning

Owns automated creation and activation of new tenants.

Scope:

- Organization creation
- Initial database records
- Storage setup
- Subscription assignment
- Initial owner login account creation
- Trial generation
- Module enablement
- Welcome communication
- Provisioning audit trail

### Billing And Licensing

Owns platform subscription, invoice, trial, plan, module, and usage billing boundaries.

Scope:

- Recurring billing
- Trials
- Coupons
- Promotions
- Usage billing
- Module billing
- Merchant billing
- Plan changes
- Suspension and reactivation

### Platform Feature Management

Owns platform-level module and capability access.

Scope:

- Module enablement
- Feature flags
- Entitlements
- Rollout groups
- Beta tenant assignment
- License enforcement inputs

### Platform Health And Monitoring

Owns tenant-level operational health visibility.

Scope:

- Database health
- API health
- Storage health
- Background jobs
- Email
- SMS
- Payments
- Supplier sync
- Merchant connectivity

### Platform Support

Owns support workflows and safe tenant impersonation.

Scope:

- Support tickets
- Tenant logs
- Read-only support access
- Impersonation approval
- Impersonation audit
- Support access history

### Platform Analytics

Owns IM1 business analytics.

Scope:

- Tenants
- MRR
- ARR
- Churn
- Retention
- Login accounts
- Transactions
- Supplier orders
- Merchant volume
- AI usage
- Module adoption

Formal specification:

- `docs/specs/IM1_PLATFORM_CONTROL_PLANE_SPEC.md`

## Core Platform Modules

### Organizations and Tenancy

Owns tenant identity and access boundaries.

Scope:

- Organizations
- Locations
- Login account memberships
- Employees
- Organization roles
- Organization permissions
- Location permissions
- Organization settings

Tenancy:

- `Organization` is the tenant.
- `Organization` is the security boundary.
- Login accounts may belong to multiple organizations through memberships.
- Permissions are organization-specific.
- Location permissions exist inside an organization.

### Human Resources

Owns employee master records and workforce operations.

Scope:

- Employees
- Optional employee login accounts
- Time clock
- Schedules
- Time off
- Payroll integration
- Commissions
- Certifications
- Employee documents
- Issued assets
- OSHA and safety
- Performance history
- Employee activity timeline

Human Resources is a core iM1 OS module. Employee is the master record for people working for the company whether or not they have login access. Authentication, roles, permissions, MFA, password credentials, and sessions attach to an employee only when login is enabled.

Formal specification:

- `docs/specs/HUMAN_RESOURCES_SCOPE.md`

### Customers

Owns customer records and customer-facing communication preferences.

Scope:

- Customer profiles
- Contact methods
- Communication preferences
- Portal access
- Customer service history

### Vehicles

Owns customer vehicles being serviced.

Scope:

- Customer-owned powersports vehicles
- VIN or serial number
- Year, make, model, trim
- Mileage or hours
- Vehicle service history

This is not unit inventory for vehicle sales.

### Vehicle Intake

Owns the start of the service workflow.

Scope:

- Customer concern
- Intake inspection
- Vehicle condition
- Photos and attachments
- Requested services
- Intake location

### Work Orders

Owns active service work.

Scope:

- Work order lifecycle
- Service lines
- Technician assignments
- Diagnosis records
- Estimate links
- Parts requirements
- Customer approvals
- Repair status

### Estimates

Owns proposed work before customer approval.

Scope:

- Labor lines
- Parts lines
- Fees and taxes
- Discounts
- Approval state
- Customer-facing estimate review

### Service Intelligence

Owns structured labor operations, technical knowledge, estimate recommendations, and repair guidance.

Scope:

- Labor operations
- Labor guide data
- Vehicle applicability
- Technical specifications
- Required parts and consumables
- Inspection recommendations
- Suggested estimate lines
- Historical repair learning inputs

Formal specification:

- `docs/specs/SERVICE_INTELLIGENCE_ENGINE_SPEC.md`

### Parts Operations

Owns canonical parts, internal parts workflows, inventory, and procurement intelligence.

Scope:

- Manufacturer part records
- Part identifiers
- Supplier listings
- Vendor mappings
- Shop inventory
- Stock levels
- Bins and storage locations
- Reorder points
- Inventory transactions
- Parts allocation to work orders
- Returns and cores
- Purchase recommendations
- Strategic procurement recommendations

Formal specification:

- `docs/specs/PARTS_ENGINE_PRODUCT_SPEC.md`
- `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`

### Purchase Intelligence

Owns real-time transactional purchasing recommendations.

Scope:

- Supplier choice for immediate part needs
- Warehouse selection
- Shipping method recommendation
- Landed cost comparison
- Existing purchase order combination
- Work order due date impact
- Customer priority impact

Purchase Intelligence is usually per work order, estimate, or immediate purchasing need.

### Procurement Intelligence

Owns strategic inventory purchasing recommendations.

Scope:

- Stocking recommendations
- Seasonal demand analysis
- Supplier promotion analysis
- Inventory turns
- Slow-moving inventory identification
- Frequently special-ordered parts that should be stocked
- Strategic recommended quantity
- Recommended purchase date
- Benchmark-informed inventory guidance
- Supplier promotion optimization

Procurement Intelligence helps run the business. It is not the same as Purchase Intelligence.

Formal specification:

- `docs/specs/PROCUREMENT_INTELLIGENCE_ENGINE_SPEC.md`

### Supplier Promotion Intelligence

Owns promotion-aware purchasing optimization inside Procurement Intelligence.

Scope:

- Supplier promotions
- Rebates
- BOGOs
- Bulk discounts
- Free freight programs
- Seasonal buy programs
- Dealer credits
- Promotion eligibility
- Cart adjustment recommendations
- Promotion-aware purchase timing
- Expected savings and confidence explanations

Supplier Promotion Intelligence is not a deals page. It is a recommendation engine that evaluates supplier programs against inventory, demand, cash flow, shelf space, purchase orders, and profitability.

Formal specification:

- `docs/specs/SUPPLIER_PROMOTION_INTELLIGENCE_ENGINE_SPEC.md`

### Social And Market Intelligence

Owns external market, supplier, racing, industry, and anonymized platform trend signals.

Scope:

- Public and partner-approved market signals
- Product, brand, and vehicle trend detection
- Racing and regional demand signals
- Supplier launch, promotion, backorder, and discontinuation signals
- Common failure trend signals
- Market recommendation confidence and evidence
- Inputs to Procurement, Parts, Service, and Shop Intelligence

Social and Market Intelligence is not a marketing module and is not first-implementation social scraping. It is the long-term early warning system for what shops may need to stock, inspect, sell, or explain next.

Formal specification:

- `docs/specs/SOCIAL_MARKET_INTELLIGENCE_ENGINE_SPEC.md`

### Network Intelligence

Owns voluntary, tenant-safe network-level purchasing, inventory, and market intelligence.

Scope:

- Network participation settings
- Tenant-safe aggregation rules
- Network demand forecasting
- Supplier negotiation support signals
- Optional inventory exchange visibility
- Regional inventory intelligence
- Network-aware promotion optimization
- Network market intelligence
- Privacy evidence for recommendations

Network Intelligence must never expose raw tenant operational data. It may use anonymized aggregates, benchmark-safe cohorts, or explicitly shared information depending on the feature.

Formal specification:

- `docs/specs/NETWORK_INTELLIGENCE_ENGINE_SPEC.md`

### Network Value Exchange

Owns participation rewards, iM Points, and utility-credit rules for the operating network.

Scope:

- iM Points
- Contribution rules
- Supplier-sponsored incentive campaigns
- Community knowledge rewards
- Data contribution rewards
- Optional inventory exchange rewards
- Customer reward boundaries
- Redemption options
- Fraud controls
- Reward audit history

The Network Value Exchange is not a bank, not a cash-equivalent system, and not a generic loyalty program. It must reward trusted network value without exposing tenant data or weakening shop control of customer relationships.

Formal specification:

- `docs/specs/NETWORK_VALUE_EXCHANGE_SPEC.md`

### Supplier Integration

Owns external supplier search, availability, and ordering workflows.

Scope:

- Supplier records
- Supplier connector capabilities
- Catalog search
- Supplier availability
- Supplier pricing
- Warehouse availability
- Estimated delivery
- Substitute or superseded parts
- Purchase order submission
- Supplier order status
- Shipment/tracking status

Supplier integrations must consume Parts Engine contracts. Business logic must not depend directly on WPS, Parts Unlimited, Turn14, or any individual supplier.

### Purchasing and Receiving

Owns parts procurement and inbound inventory movement.

Scope:

- Purchase orders
- Purchase order lines
- Receiving events
- Partial receipts
- Backorders
- Inventory updates
- Work order parts fulfillment

### Repair

Owns execution of approved service work.

Scope:

- Technician workflow
- Labor tracking
- Parts consumption
- Completion notes
- Quality checks
- Customer status updates

### Invoicing

Owns billable service and parts output.

Scope:

- Invoice lifecycle
- Labor charges
- Parts charges
- Taxes and fees
- Payments boundary
- Customer receipts

### Financial Services / Payments & Finance

Owns the financial operating layer for money movement inside iM1.

Scope:

- Platform merchant applications and underwriting
- Platform merchant portfolio, risk, provider, residual, settlement, chargeback, hardware, fulfillment, pricing, and support operations
- Company merchant account status
- Payment engine
- Gateway driver abstraction
- Terminal management
- Transaction center
- Customer wallet tokens
- Payment links
- ACH processing
- Subscription billing
- Settlement visibility
- Refunds, voids, disputes, and chargebacks
- Operational financial ledger
- Company deposits, statements, reports, and settings
- Certified payment hardware

Financial Services is not a gateway. Gateway providers are infrastructure drivers behind iM1 Payments. Product modules should call iM1 payment abstractions and should not integrate directly with NMI, Authorize.net, Stripe, or future providers. Tenant-facing navigation should be named Payments & Finance. Platform navigation remains Financial Services because it represents iM1's internal merchant services business.

Business workflow payment actions stay in the owning modules:

- POS collects payment inside Sales/POS.
- Work orders collect deposits or pickup payments inside Service.
- Invoices collect payment inside Invoicing.
- Ecommerce checkout stays inside Ecommerce.
- Customer portal payment stays inside Customer Portal.

Formal specification:

- `docs/specs/IM1_FINANCIAL_SERVICES_VISION.md`

### Financial Intelligence

Owns payment, settlement, profitability, and benchmark-safe financial recommendations.

Scope:

- Estimate approval timing insights
- Deposit behavior
- Invoice payment behavior
- Payment method recommendations
- Settlement reporting inputs
- Financing opportunity signals
- Labor and parts margin insights
- Collection speed benchmarks
- Profitability recommendations

Financial Intelligence is not payment processing. Merchant Services may move money; Financial Intelligence explains financial behavior and recommends better workflows.

Formal specification:

- `docs/specs/FINANCIAL_INTELLIGENCE_ENGINE_SPEC.md`

### Customer Portal

Owns the customer-facing experience.

Scope:

- Estimate review
- Estimate approval
- Repair status
- Customer messages
- Invoice review
- Payment handoff
- Service history

### Ecommerce Integration

Owns integration boundaries for online parts and service-related commerce.

Scope:

- Product publishing boundaries
- Inventory availability boundaries
- Order import boundaries
- Customer communication handoff

Ecommerce integrations consume the Parts Engine as the IM1OS Commerce Engine. Ecommerce platforms do not define canonical part identity, inventory truth, or supplier purchasing rules.

### Commerce Network

Owns local-first marketplace, dealer storefront, supplier network, merchant services boundary, customer portal commerce, mobile commerce, B2B, and rewards integration.

Scope:

- Marketplace search
- Dealer storefront boundaries
- Shared catalog publishing
- Local pickup
- Dealer shipment
- Supplier drop ship
- Install appointment conversion
- Commerce availability results
- Merchant services boundary
- Dealer participation settings
- Network Value Exchange integration

The Commerce Network is not another generic storefront and should not compete with participating dealers by default. It should route commerce through local and participating dealers where practical.

Formal specification:

- `docs/specs/COMMERCE_NETWORK_SPEC.md`

### Reporting

Owns operational visibility across service and parts.

Scope:

- Service performance
- Work order aging
- Parts demand
- Inventory value
- Purchase order status
- Receiving status
- Invoice status
- Cross-location reporting

### Data Intelligence

Owns analytics readiness, structured event capture, tenant-safe aggregation rules, and future benchmarking foundations.

Scope:

- Domain events
- Timeline history
- Structured service/labor/parts data capture
- Analytics export boundaries
- Tenant-safe anonymization rules
- Benchmarking readiness
- Future AI/OpenAI data preparation

Formal specification:

- `docs/specs/DATA_INTELLIGENCE_SCOPE.md`

## Optional Future Modules

These modules are explicitly not foundation modules:

- Vehicle sales
- Unit inventory
- F&I
- Deal jackets
- Floor planning

They may be considered later only after the service and parts platform is stable.

## Module Data Rules

Every tenant-owned module table must contain `OrganizationId`.

Most operational module tables should contain `LocationId`, especially tables related to intake, work orders, parts inventory, purchase orders, receiving, repair, invoicing, and reporting.

Module permissions must be organization-specific. Location-scoped module operations must also respect location permissions.
