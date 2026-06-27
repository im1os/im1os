# IM1OS Product Manifesto

IM1OS exists to empower independent powersports businesses through shared intelligence.

It should help shops move work through the building faster, with less confusion, fewer lost parts, clearer communication, and better customer trust.

This is not another traditional Dealer Management System. IM1OS is an Operating System and Commerce Network for the independent powersports industry.

The platform is built for the real daily rhythm of a shop:

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

Every product decision should help a shop get a customer back riding faster.

## Service First

The first responsibility of IM1OS is to support service work.

Customers bring vehicles in with problems, questions, symptoms, damage, maintenance needs, and incomplete information. The system must help the shop capture that reality quickly and turn it into clear work.

Intake, diagnosis, estimates, technician work, parts, approvals, repair, invoicing, and customer updates are one connected workflow.

## Parts Are The Heart

Parts operations are not a side feature. They are the center of the platform.

A service department cannot move if parts cannot be found, priced, ordered, received, tracked, allocated, and explained to the customer.

IM1OS should make parts work visible:

- What part is needed?
- Is it in local inventory?
- Which supplier has it?
- What is the cost?
- What is the ETA?
- Has it been ordered?
- Has it arrived?
- Which work order needs it?
- Who needs to know it arrived?

The Parts Engine is also the Commerce Engine for IM1OS. It should power service repairs first, then grow into the shared engine for ecommerce integrations, B2B wholesale, inventory replenishment, purchase ordering, and supplier connectors.

## Real Shop Workflows Win

The existing PHP/JavaScript system represents years of real operational learning. It is not disposable just because the technology is being replaced.

When rebuilding a workflow, first understand why it exists.

Preserve proven behavior unless there is a clear business reason to change it. Modernize the implementation and user experience, but do not casually redesign the workflow.

## Suppliers Are Plug-Ins

Supplier integrations must be abstract.

WPS may be the first connector, but it must never become the Parts Engine. Future suppliers, catalog sources, availability services, ecommerce systems, and ordering providers must be able to plug into the same foundation.

The domain should speak in IM1OS language:

- Supplier
- Supplier part
- Availability
- Catalog search
- Purchase order
- Receiving
- Vendor mapping

It should not speak in one vendor's API language.

## Mobile Where Work Happens

Technicians, service writers, parts staff, and intake staff do not always work at a desk.

IM1OS must respect the places where work happens:

- At the counter
- In the service bay
- Near the vehicle
- In parts storage
- At receiving
- During pickup

PIN-based workflows, barcode scanning, photos, videos, quick lookup, and focused mobile screens are not conveniences. They are part of the operating model.

## One Workflow, Many Surfaces

The same business workflow should work through multiple surfaces:

- Admin screens
- Mobile technician workspace
- Intake flow
- Customer portal
- Background jobs
- Supplier connectors
- Ecommerce integrations

Build the workflow once. Reuse it everywhere.

## Intelligence Is The Differentiator

IM1OS should help shops know what to do, what to charge, what to order, where to buy it, and how to run better.

The Intelligence Layer is where IM1OS turns operational data, supplier knowledge, service history, market signals, and shop context into better decisions.

Foundational intelligence questions:

- Parts Intelligence: what part is this?
- Purchase Intelligence: where should I buy this part for this immediate need?
- Procurement Intelligence: what should I stock, when should I stock it, and how much should I buy?
- Supplier Promotion Intelligence: how should supplier programs change what, when, where, and how much I buy?
- Network Intelligence: what opportunities can participating shops identify together without exposing private tenant data?
- Network Value Exchange: how should trusted contribution to the network be recognized and rewarded?
- Financial Intelligence: what financial behavior, payment workflow, or profitability signal should the shop act on?
- Service Intelligence: what should I do, what should I charge, and what information do I need to complete the repair?
- Shop Intelligence: how can I run a better business?
- Social and Market Intelligence: what will customers ask for next?

Business Engines are reusable capabilities. Modules are UI and workflow surfaces that consume those engines.

IM1OS is not only a system of record. It should become a system of recommendations: gather data, normalize it, apply business rules and analytics, produce a recommendation, explain why it was made, and preserve the evidence.

This is the Digital Service Advisor: not a chatbot, but a system that turns vehicle context, customer concern, technical knowledge, parts availability, shop history, and purchasing data into better operational decisions.

## Commerce Is The Network Surface

IM1OS should not become another generic ecommerce storefront.

The Commerce Network should connect dealer inventory, supplier availability, local pickup, shipment, installation appointments, merchant services, customer portal, rewards, and marketplace search.

The principle is local first: help the customer buy from or through participating independent dealers whenever practical.

## The Network Value Exchange

IM1OS should become a powersports operating network, not only a SaaS application.

Shops, suppliers, technicians, and customers can all create value for the network. The Network Value Exchange should recognize trusted contributions with iM Points as utility credits inside the ecosystem.

This is not a bank, not a cash-equivalent system, and not a generic loyalty program. Rewards must support the network flywheel while preserving tenant privacy, consent, auditability, and shop control.

## Data Is A Product Asset

Every work order, estimate, labor operation, part selection, supplier quote, purchase order, invoice, payment, technician note, approval, decline, and completed repair should improve the platform over time.

Collect structured data. Protect tenant privacy. Emit durable events. Build toward benchmarking and recommendations without exposing one shop's raw data to another.

## The Customer Should Never Be In The Dark

Customers should know what is happening with their vehicle.

IM1OS should make it easy for shops to communicate status, estimates, approvals, delays, parts arrivals, invoices, and pickup readiness without creating extra clerical work.

The customer portal is not an afterthought. It is part of the service experience.

## Tenant Isolation Is Product Integrity

IM1OS is a SaaS platform. Organization is the tenant and security boundary.

Every design must protect one organization's data from another. Permissions are organization-specific, and location permissions exist inside an organization.

Tenant isolation is not only a technical requirement. It is part of the trust shops place in the platform.

## Build For Decades

IM1OS should be understandable years from now.

Prefer business clarity over clever abstractions. Prefer durable workflows over short-term shortcuts. Prefer connector boundaries over hardcoded dependencies. Prefer maintainable modules over fast feature sprawl.

The goal is not to generate software quickly. The goal is to build a platform that can grow without losing its shape.

## Product North Star

Before adding a feature, changing a workflow, or introducing a dependency, ask:

Does this help an independent powersports shop move service and parts work from intake to completion with less friction and more trust?

If the answer is no, reconsider the design.
