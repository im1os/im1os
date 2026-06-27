# Service Intelligence Engine Specification

Status: formal product specification.

The Service Intelligence Engine is one of the core business engines of IM1OS. It is the foundation for the Digital Service Advisor.

Its purpose is not simply to write estimates or display technical specifications. Its purpose is to help repair shops accurately estimate, price, diagnose, and complete repairs.

## Purpose

Many independent powersports shops do not have experienced service writers or dealership-trained technicians.

The Service Intelligence Engine helps the user answer four questions:

- What needs to be repaired?
- What parts are required?
- How much labor should be charged?
- What technical information is required to complete the repair correctly?

## Inputs

The engine combines multiple sources of information:

- Vehicle year
- Vehicle make
- Vehicle model
- VIN, when available and permitted
- Customer complaint
- Technician findings
- OEM specifications
- Service procedures
- Labor guide
- Historical repair data
- Common failure data
- Shop labor rate

## Outputs

The engine should produce structured recommendations:

- Suggested labor operations
- Estimated labor time
- Flat rate hours
- Suggested customer labor charge
- Required parts
- Required fluids
- Required consumables
- Torque specifications
- Capacities
- Special tools
- Service notes
- Safety warnings
- Inspection recommendations
- Upsell opportunities

## Example Workflow

Customer request:

```text
Replace front brake pads.
```

Vehicle:

```text
2025 Honda CRF250R
```

The Service Intelligence Engine should suggest:

- Labor operation: Replace Front Brake Pads
- Suggested labor time: 0.6 hours
- Customer labor charge: 0.6 x shop labor rate
- Required parts: front brake pads
- Consumables: brake cleaner, brake fluid inspection/top-off if required
- Technical information: caliper torque, pad thickness specification, rotor thickness specification, brake fluid type, bleeding procedure if required
- Inspection recommendations: rotor condition, brake line condition, caliper slide pins, wheel bearings, front tire wear

The estimate should be built from structured labor and parts records, not from free-text notes.

## Labor Intelligence

Labor should not be hardcoded.

Each labor operation should support:

- Labor code
- Description
- Base labor hours
- Vehicle applicability
- Skill level
- Difficulty rating
- Typical completion time
- Flat rate time
- Shop adjustable time
- Required special tools
- Related labor operations

## Technical Knowledge

Each repair operation should expose:

- Torque specifications
- Fluid capacities
- Fluid types
- Wear limits
- Adjustment specifications
- OEM procedures
- Service bulletins
- Common technician notes

## Parts Integration

The Service Intelligence Engine must integrate directly with the Parts Engine.

Every labor operation should be able to define:

- Required parts
- Optional parts
- Recommended consumables
- Optional upgrade parts
- Alternative part choices

The Parts Engine determines canonical part identity, availability, allocation, and purchasing.

## Purchase Integration

Once required parts are identified, the Purchase Intelligence Engine determines:

- Best supplier
- Best warehouse
- Lowest total cost
- Fastest delivery
- Inventory allocation
- Purchase order recommendations

## Shop Learning

Future versions should learn from completed repairs.

Examples:

- Average technician completion time
- Frequently added parts
- Frequently missed parts
- Most common upsells
- Most common warranty repairs
- Most profitable repairs

This information should improve future estimate recommendations.

## Intelligence Layer

The Intelligence Layer contains reusable decision-making capabilities consumed by modules, apps, APIs, connectors, reporting, and future AI experiences.

IM1OS organizes these capabilities into three groups:

- Core Operational Engines: Identity, Customer, Vehicle, Work Order, Parts Intelligence, Purchase Intelligence, and Service Intelligence.
- Business Intelligence Engines: Procurement Intelligence, Supplier Promotion Intelligence, Network Intelligence, Network Value Exchange, Financial Intelligence, Shop Intelligence, and Social and Market Intelligence.
- Future Platform Engines: AI Assistant, Analytics and Benchmarking, Automation, and Customer Experience.

Foundational intelligence questions:

1. Parts Intelligence: what part is this?
2. Purchase Intelligence: where should I buy this part for this immediate need?
3. Procurement Intelligence: what should I stock, when, and how much?
4. Supplier Promotion Intelligence: how should supplier programs change what, when, where, and how much I buy?
5. Network Intelligence: what opportunities can participating shops identify together without exposing private tenant data?
6. Network Value Exchange: how should trusted contribution to the network be recognized and rewarded?
7. Financial Intelligence: what financial behavior, payment workflow, or profitability signal should the shop act on?
8. Service Intelligence: what should I do, what should I charge, and what information do I need?
9. Shop Intelligence: how can I run a better business?
10. Social and Market Intelligence: what will customers ask for next?

## Non-Goals For First Implementation

- Do not build an AI chatbot.
- Do not hardcode labor recommendations into UI pages.
- Do not create unstructured estimate text as the source of truth.
- Do not bypass the Parts Engine for required parts.
- Do not build dashboards before structured service data and events exist.

## Success Criteria

The Service Intelligence foundation succeeds when:

- Labor operations are structured business entities.
- Estimates can reference labor operations and required parts.
- Vehicle applicability can be modeled.
- Technical specifications can be linked to service operations.
- Actual completion data can be captured for future learning.
- Recommendations can be explained by structured inputs.
