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

## Operating System Standard

iM1 UI must behave as a single, consistent operating system rather than a collection of independent pages.

The user experience should be predictable. Every module should use the same navigation patterns, layouts, actions, dialogs, forms, grids, and workflows. A user who learns one area of iM1 should immediately understand every other area.

Consistency is more valuable than uniqueness. When adding a feature, first ask whether it can be built using an existing iM1 component. Only create a new component when the existing component set cannot express the workflow.

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

The application shell is permanent and should never be recreated by individual pages. Only the main content area changes when navigating between pages.

The shell owns:

- Header.
- Left navigation.
- Main content area.
- Notifications.
- User/account menu.
- Breadcrumbs.
- Status indicators.
- Authentication-context-specific branding.

Customer-facing shell copy must present iM1 OS as business software. Platform Admin may use internal platform language.

No page should implement its own navigation.

## Left Navigation

The left navigation is the primary navigation mechanism throughout iM1.

Rules:

- Modules appear in left navigation.
- Modules may contain expandable child navigation.
- Navigation behavior is identical everywhere.
- Icons, spacing, typography, and indentation are standardized.
- The currently selected item is clearly highlighted.
- Navigation remains visible while users work.
- Future support for favorites and recent items should be considered.

## Standard Page Layout

Every administrative page follows the same structure:

```text
Page Title
Breadcrumbs

Toolbar
-------------------------------------------------

Filters, optional

Grid / Dashboard / Form

Status Bar / Pagination
```

Pages should not invent their own layout.

## Core Components

Initial component contract:

- `IM1Page`: page header, eyebrow, title, description, actions, and content spacing.
- `IM1Toolbar`: search, filters, view selectors, export, and primary actions.
- `IM1Button`: primary, secondary, quiet, danger, and icon-capable actions.
- `IM1Card`: repeated item/card surface only, not nested page sections.
- `IM1Dialog`: modal confirmation, forms, and destructive action confirmation.
- `IM1Form`: labels, validation, field groups, checkboxes, selects, and submit rows.
- `IM1DataGrid`: standard tabular workflow component.
- `IM1Tabs`: standard tabbed section navigation.
- `IM1Lookup`: future lookup and picker framework.
- `IM1Notification`: future notification and toast framework.

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
