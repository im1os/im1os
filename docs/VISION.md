# iM1 OS Vision

Read `docs/vision/IM1OS_PRODUCT_MANIFESTO.md` as the non-technical north star for this product.

iM1 OS is the Operating System and Commerce Network for independent powersports businesses.

The platform starts by helping shops efficiently intake customer vehicles, diagnose repairs, build estimates, search supplier catalogs, manage shop inventory, order parts, receive parts, complete repairs, invoice customers, and provide a modern customer portal.

Mission: empowering independent powersports businesses through shared intelligence.

Parts operations are the heart of IM1OS. The product should optimize how shops find parts, check supplier availability, manage inventory, create purchase orders, receive parts, connect parts to work orders, and keep customers informed.

The long-term differentiator is the IM1OS Intelligence Layer. It should help shops make better service, parts, purchasing, procurement, shop management, and market-readiness decisions. Social and Market Intelligence belongs in this layer as an early warning system for demand, not as a marketing feature.

Network Intelligence extends that layer across participating shops using voluntary, tenant-safe aggregation and explicitly shared inventory signals so independent businesses can gain network-level purchasing and inventory advantages without exposing private operational data.

The Network Value Exchange turns trusted participation into ecosystem value through iM Points, utility credits, supplier-sponsored incentives, knowledge rewards, data contribution rewards, and optional inventory exchange rewards. It must not become banking, cash issuance, or a privacy leak.

Financial Intelligence and the Commerce Network extend IM1OS beyond operational software. Merchant Services, marketplace search, dealer websites, supplier network, customer portal, mobile commerce, and rewards should feed the Intelligence Layer while preserving dealer participation, tenant privacy, and customer trust.

The core workflow is:

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

This repository is the foundation only. Business modules will be added behind clear module boundaries, shared authentication, organization-based tenant isolation, audit logging, configuration, feature flags, APIs, and background processing.

The product experience is workspace-first. iM1 OS must not become a collection of CRUD screens. Each major screen should present the user with identity, operational status, important context, and next actions before exposing edit forms. The formal UI/UX scope is documented in `docs/specs/IM1_UI_FRAMEWORK_SPEC.md`.

IM1OS is not a traditional Dealer Management System. Vehicle sales, F&I, deal jackets, floor planning, and unit inventory are not foundation concerns. They may become future optional modules, but they must not drive the platform architecture.
