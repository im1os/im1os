# iM1 UI Framework Specification

Status: foundation architecture scope.

iM1 UI is the reusable application framework for iM1 OS and IM1 Platform surfaces. Business modules should be composed from iM1 UI primitives instead of hand-built screen-specific UI.

## Purpose

iM1 UI owns the product's presentation contract:

- Theme and design tokens.
- Application shell.
- Core components.
- Data grid behavior.
- Service and interaction patterns.
- Permission-aware actions.
- Customer-facing terminology rules.

This keeps iM1 OS maintainable as it grows from foundation screens into service, parts, commerce, intelligence, and network modules.

## Architectural Rule

Nothing outside the iM1 UI component boundary may import third-party UI libraries directly.

Pages and modules must consume iM1-owned components and primitives:

```text
IM1Button
IM1Card
IM1Page
IM1Toolbar
IM1Dialog
IM1Form
IM1DataGrid
```

If the frontend later moves to React or another SPA stack, modules should import only iM1 components:

```ts
import { IM1DataGrid } from "@/components/DataGrid";
import { IM1Button } from "@/components/Button";
import { IM1Dialog } from "@/components/Dialog";
```

AG Grid, MUI, Bootstrap, icon libraries, charting libraries, and similar dependencies must be wrapped by iM1 UI. Replacing an underlying vendor should require updates inside the wrapper layer, not across business modules.

The current web host is ASP.NET Core MVC/Razor. Until a dedicated SPA package exists, Razor views should consume iM1 UI CSS classes, shared scripts, partials, or future tag helpers instead of direct third-party UI dependencies.

## Theme And Design Tokens

iM1 UI defines tokens for:

- Color roles: canvas, surface, border, text, muted text, action, danger, success, warning, focus.
- Typography: font family, display, title, body, label, table, and caption scales.
- Spacing: predictable 4px-based spacing steps.
- Radius: restrained application UI radii.
- Shadows: minimal elevation for dialogs and overlays only.
- Density: enterprise application controls optimized for scanning and repeated use.
- Light and dark support through token overrides.

Raw color values should live in the token layer. Components and pages should consume semantic tokens.

## Application Shell

The shell owns:

- Header.
- Sidebar or responsive navigation.
- Breadcrumbs.
- Page title and action area.
- Content width and responsive behavior.
- Authentication-context-specific branding.

Customer-facing shell copy must present iM1 OS as business software. Platform Admin may use internal platform language.

## Core Components

Initial component contract:

- `IM1Page`: page header, eyebrow, title, description, actions, and content spacing.
- `IM1Toolbar`: search, filters, view selectors, export, and primary actions.
- `IM1Button`: primary, secondary, quiet, danger, and icon-capable actions.
- `IM1Card`: repeated item/card surface only, not nested page sections.
- `IM1Dialog`: modal confirmation, forms, and destructive action confirmation.
- `IM1Form`: labels, validation, field groups, checkboxes, selects, and submit rows.
- `IM1DataGrid`: standard tabular workflow component.

## IM1DataGrid

IM1DataGrid should be the only path to AG Grid Community.

Required capabilities:

- Standard toolbar.
- Search and filter slots.
- Paging.
- Column sorting.
- Export.
- Saved views.
- Empty/loading/error states.
- Permission-aware row and bulk actions.
- Consistent density and responsive overflow.

Modules should provide column definitions, row data, query contracts, permissions, and action callbacks. They should not directly configure AG Grid outside the wrapper.

## Service Layer

iM1 UI should include standard client-side service patterns:

- API client wrapper.
- Authentication and authorization helpers.
- Error normalization.
- Notification/toast behavior.
- Loading and saving states.
- Empty state handling.
- Form validation display.

Service helpers must not bypass server-side authorization, tenant isolation, audit logging, or business rules.

## Business Module Usage

Business Administration and future modules should be implemented by composing iM1 UI primitives.

The Business UI maps directly to the internal `Organization` entity but presents that entity as Business. This remains a presentation-layer concern and does not create duplicate domain models.

## Adoption Plan

1. Establish tokens, shell primitives, core component CSS/classes, and scripting conventions in the MVC web host.
2. Refactor existing MVC views to use iM1 UI primitives.
3. Introduce IM1DataGrid as the only supported grid abstraction.
4. Add framework-level permission-aware actions and saved view contracts.
5. If a SPA frontend is introduced, create `@/components/*` wrappers that preserve the iM1 UI contract.
