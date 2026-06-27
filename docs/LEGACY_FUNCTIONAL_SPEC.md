# Legacy Functional Specification

This document captures the current PHP/JavaScript implementation as the functional reference for rebuilding IM1OS. The current application is treated as the workflow specification. Rebuild work must preserve proven workflow behavior unless a change is explicitly approved.

## Source Systems Reviewed

Primary workflow source:

- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\indiemoto_workorder`

Supporting parts and inventory sources:

- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\indiemoto-woocommerce-physical-inventory`
- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\indiemoto-flash-inventory-scanner`
- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\IndieMoto_WooCommerce_Inventory_and_Meta_Manager`
- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\indiemoto-wps-bo-api`
- `C:\Backup\IndieMoto\IndieMoto\wp-content\plugins\wps-api-search`

The first-pass review focused on Vehicle Intake, Technician Workspace, Customer Portal, Work Order, Estimate, Parts Search, Inventory, Purchase Orders, and Receiving.

## Core Legacy Concepts

The legacy work order plugin stores one work order as WordPress post type `im_workorder`.

Major data groups:

- Customer: `_imwo_customer`
- Vehicle: `_imwo_vehicle`
- Work order summary: `_imwo_summary`
- Estimate and repair line items: `_imwo_line_items`
- Media: `_imwo_media`
- Customer SMS log: `_imwo_sms_log`
- Customer SMS thread: `_imwo_sms_thread`
- Estimate approval: `_imwo_approval`
- Square invoice/customer state: `_imwo_square`
- WPS order state: `_imwo_wps`
- Work order version for optimistic locking: `_imwo_version`
- Parts received notification event history: `_imwo_parts_received_sms_events`

## Work Order Stages

Legacy stage values:

- `intake`: Intake / Check-in
- `diagnosis`: Diagnosis
- `awaiting-approval`: Awaiting Approval
- `awaiting-deposit`: Awaiting Deposit / Payment
- `declined`: Declined / Not Approved
- `parts-ordered`: Parts Ordered
- `scheduled`: Scheduled
- `in-progress`: In Progress
- `ready`: Ready for Pickup
- `completed`: Completed
- `lost-abandoned`: Lost / Abandoned
- `closed`: Closed / Archived

Important behavior:

- Stage changes can trigger automatic customer SMS unless the user skips the message.
- Moving to `ready` records a ready timestamp.
- Moving to `ready` can auto-send the final balance invoice when deposit terms apply and Square is configured.
- Moving to `completed` auto-fulfills non-declined parts lines and reduces inventory.
- Moving to `completed` sends a review-request SMS instead of a normal portal status update.

## Priority and Contact Terms

Priority values:

- `normal`
- `rush`
- `hold`

Contact preference values:

- `phone`
- `text`
- `email`

Line item types:

- `labor`
- `parts`
- `diagnostics`
- `fees`
- `other`

## Vehicle Intake Workflow

Entry points:

- PWA route `/intake/`
- Shortcode `imwo_intake_mobile`
- Submit action `imwo_front_intake_submit`

Current workflow:

1. Staff member enters a 4-digit PIN.
2. PIN resolves to an employee user.
3. Staff searches customer by name, phone, or email.
4. Customer can be selected from Square search results or created from intake fields.
5. If a new customer is created without Square customer selection, the phone number must have been searched first.
6. Tax exempt customers require a tax exempt number.
7. Existing customer vehicles are loaded when enough customer identity is available.
8. Staff can select a saved vehicle or create a new vehicle.
9. Vehicle Type/Year/Make/Model dropdowns use WPS fitment data when available.
10. Vehicle selection can be bypassed for special cases.
11. Intake captures requested service as customer complaint/notes.
12. Intake supports photo and video upload.
13. Work order is created with `WO-{postId}`. Special orders use `SO-{postId}`.
14. Intake date is set to current date.
15. The intake employee becomes advisor when possible.
16. A welcome SMS is sent to the customer when SMS is configured.
17. The user is redirected to an intake print view for label/work order printing.

Functional rules:

- Intake is PIN-gated, not normal WordPress login-gated.
- Phone search before new customer creation prevents accidental duplicates.
- Tax exempt number is mandatory when tax exempt is enabled.
- Intake print is immediate, but printing is not auto-triggered because mobile/iPad users must choose a printer.
- Vehicle fitment lookup should be cache-first and should not block work order creation if unavailable.

## Technician Workspace Workflow

Entry points:

- PWA route `/tech/`
- Shortcode `imwo_tech_estimate_mobile`
- Submit action `imwo_front_tech_estimate_submit`

Current workflow:

1. Technician enters a 4-digit PIN.
2. PIN grants access to assigned work orders and product tools.
3. Technician can assign themselves as lead technician.
4. Technician records diagnosis, service notes, and parts notes.
5. Technician manages line items for labor, parts, diagnostics, fees, and other charges.
6. Technician can search products by name or SKU.
7. Product search is fitment-aware when vehicle Year/Make/Model or WPS vehicle id exists.
8. Product search can search all products or labor-only.
9. SKU lookup supports barcode/scanner-style entry.
10. Technician can add photos/videos and remove media.
11. Technician can send estimates, deposit invoices, balance invoices, and SMS messages.
12. Saves use optimistic locking through `_imwo_version`.

Functional rules:

- A version mismatch must stop the save and force reload rather than overwrite another user's changes.
- Technician split across assigned technicians must total 100 percent when assignments exist.
- Labor lines are not taxable by default.
- Declined line items remain visible but should not drive fulfillment.
- Completed work orders auto-fulfill eligible parts.

## Customer Portal Workflow

Entry points:

- Shortcode `imwo_customer_portal`
- Portal action `imwo_portal_action`
- Portal cookie `imwo_portal`

Current workflow:

1. Customer enters phone number.
2. Customer must consent to receive SMS.
3. System sends a portal code by SMS.
4. Customer enters code.
5. If multiple profiles match the phone, customer chooses a profile.
6. Customer can view active work order status, vehicle/customer context, media, purchases/receipts, invoices, SMS thread, and service history.
7. Customer can add portal notes/messages.
8. Admin users can preview the portal.

Functional rules:

- Portal authentication is phone/SMS-code based.
- Portal profiles may come from work orders, Square customers, or WooCommerce customers.
- Portal access must only expose work orders linked to the authenticated phone/profile.
- Customer-facing media links are intentional; uploaded photos/videos are visible to customers.

## Estimate and Approval Workflow

Estimate state is stored in `_imwo_approval`.

Approval statuses:

- `not_sent`
- `pending`
- `accepted`
- `declined`

Deposit terms:

- `none`
- `50_50`
- `25_75`

Current behavior:

- Approval links use generated tokens.
- Admin can override approval status.
- Setting approval back to pending reactivates the approval link.
- Accepted work orders move to `scheduled` if no deposit is due or deposit is already paid.
- Accepted work orders move to `awaiting-deposit` if deposit terms apply and the deposit has not been paid.
- Declines capture a decline reason.
- Payment terms auto-sync from deposit terms unless the user customized payment terms.

## Parts Search Workflow

Current behavior:

- Product search requires staff/product-tool access.
- Search terms shorter than 3 characters return no results unless wildcard/labor mode applies.
- Exact SKU search takes a fast path.
- Search results include product id, name, SKU, price, local stock, labor flag, and cached supplier inventory when available.
- Fitment-aware search filters products by WPS vehicle id or Year/Make.
- Labor is universal and bypasses fitment filtering.
- Supplier inventory is loaded lazily for up to 20 SKUs at a time.

Functional rules:

- Parts search must support exact SKU and broad text search.
- Parts search must support fitment-constrained and search-all modes.
- Supplier availability is supplemental and must not be hardwired to WPS.
- Local inventory and supplier availability are separate concepts.

## Inventory Workflow

Legacy inventory depends heavily on WooCommerce products and stock fields.

Current behavior:

- Local stock is stored as product stock.
- Second inventory/mobile stock is supported.
- Product barcode is stored as product meta `barcode`.
- Products can have reorder quantity, stock minimum, stock-in-store flag, MAP, supplier product id, and custom meta display fields.
- Physical inventory update supports SKU/barcode search, stock count update, reorder fields, stock-in-store flag, and label printing.
- Flash inventory scanner supports inventory containers, scanner input, batch scans, scope validation, inventory update, zeroing unscanned scoped items, and inventory logs.

Functional rules:

- Barcode and SKU must both be first-class lookup keys.
- Inventory updates must be auditable.
- Physical count workflows can overwrite stock counts.
- Container/scoped counts may intentionally zero unscanned items inside the selected scope.
- Stocked-in-store requires reorder quantity and stock minimum.

## Purchase Orders and Receiving

Current behavior exists in two forms:

- Work order line items can be pushed to a workorder order queue.
- WPS order/cart APIs can create supplier carts from work order parts.
- Order status and receiving status can be fetched from WPS/backorder APIs.
- Parts received events notify the lead technician and optionally admin.

Functional rules:

- Purchase/order workflow must be supplier-abstract.
- WPS is only the first connector.
- Receiving a part should be able to notify the assigned technician.
- Received notifications must be de-duplicated by event key.
- Purchase/receiving status must be linkable back to work order parts.

## Permissions and Access

Legacy access modes:

- WordPress admin capabilities such as `edit_post`, `edit_posts`, and `manage_options`.
- Front staff PIN for intake/tech/mobile workflows.
- Employee identity from WordPress users with role `employee` or meta `imtc_is_employee = 1`.
- Product tools available when tech or intake PIN has authenticated.
- Work order assignment checks use lead technician and up to two additional technician assignments.

IM1OS replacement rules:

- Replace WordPress capabilities with organization-specific permissions.
- Preserve PIN-based staff workflow as a supported authentication mode.
- Keep employee identity separate from user login identity.
- Scope all access to `OrganizationId`; operational access must also consider `LocationId`.

## Entities Extracted

Core entities:

- Organization
- Location
- User
- Employee
- Organization membership
- Role
- Permission
- Customer
- Customer portal profile
- Customer vehicle
- Work order
- Work order stage
- Work order priority
- Technician assignment
- Diagnosis
- Estimate
- Estimate approval
- Estimate line
- Part
- Supplier part
- Inventory item
- Inventory transaction
- Inventory container
- Physical count
- Purchase order
- Purchase order line
- Supplier order
- Receiving event
- Invoice
- Payment
- SMS message
- Portal note
- Document
- Photo/video media
- Audit event

## Open Questions

- Which legacy screens are still current and actively used versus abandoned experiments?
- Should special orders remain a separate work order type or become a purchase/order workflow inside Parts Operations?
- Which Square behaviors are required in IM1OS foundation versus connector-specific invoice/payment behavior?
- Which inventory concepts map to physical shop locations versus mobile/secondary locations?
- Which supplier order statuses must be normalized across future suppliers?
